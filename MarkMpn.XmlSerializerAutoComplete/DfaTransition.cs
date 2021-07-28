using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;

namespace MarkMpn.XmlSerializerAutoComplete
{
    abstract class DfaTransition
    {
        public DfaState Next { get; set; }

        public abstract bool Accept(ParserState parser);
    }

    class IgnoreProcessingInstructionTransition : DfaTransition
    {
        public override bool Accept(ParserState parser)
        {
            if (!(parser.Node is PartialXmlProcessingInstruction))
                return false;

            return true;
        }
    }

    class IgnoreTextTransition : DfaTransition
    {
        public override bool Accept(ParserState parser)
        {
            if (!(parser.Node is PartialXmlText))
                return false;

            return true;
        }
    }

    abstract class ElementBasedTransition : DfaTransition
    {
        public string ElementName { get; set; }
    }

    abstract class PolymorphicElementBasedTransition : ElementBasedTransition
    {
        public string TypeName { get; set; }
    }

    class CreateInstanceTransition : PolymorphicElementBasedTransition
    {
        private bool _createdProperties;

        public Type Type { get; set; }

        public Dictionary<string, PropertyOrField> AttributeProperties { get; } = new Dictionary<string, PropertyOrField>();

        public PropertyOrField Property { get; set; }

        public override bool Accept(ParserState parser)
        {
            if (!(parser.Node is PartialXmlElement startElement))
                return false;
            
            if (startElement.Name != ElementName)
                return false;

            startElement.Attributes.TryGetValue("xsi:type", out var typeName);
            if (typeName != TypeName)
                return false;

            if (!_createdProperties)
            {
                BuildProperties(this);
                _createdProperties = true;
            }

            var obj = Activator.CreateInstance(Type);

            foreach (var attrProp in AttributeProperties)
            {
                if (startElement.Attributes.TryGetValue(attrProp.Key, out var attrValue) &&
                    XmlTypeConverter.TryChangeType(attrProp.Value.Type, attrValue, out var value))
                {
                    attrProp.Value.SetValue(obj, value);
                }
            }

            if (Property != null)
            {
                var parent = parser.DeserializedStack.Peek();

                if (Property.Type.IsArray)
                {
                    var array = (Array)Property.GetValue(parent);

                    if (array == null)
                    {
                        array = Array.CreateInstance(Property.Type.GetElementType(), 1);
                    }
                    else
                    {
                        var existing = array;
                        array = Array.CreateInstance(Property.Type.GetElementType(), existing.Length + 1);
                        existing.CopyTo(array, 0);
                    }

                    array.SetValue(obj, array.Length - 1);
                    Property.SetValue(parent, array);
                }
                else
                {
                    Property.SetValue(parent, obj);
                }
            }

            parser.DeserializedStack.Push(obj);

            return true;
        }

        private void BuildProperties(CreateInstanceTransition create)
        {
            var hasTextProperty = false;

            foreach (var member in create.Type.GetMembers(BindingFlags.Public | BindingFlags.Instance).Where(m => m is PropertyInfo || m is FieldInfo))
            {
                // Check if the property should be ignored for serialization
                var ignore = member.GetCustomAttribute<XmlIgnoreAttribute>();

                if (ignore != null)
                    continue;

                PropertyOrField p;

                if (member is PropertyInfo prop)
                {
                    if (!prop.CanRead || !prop.CanWrite)
                        continue;

                    p = new PropertyOrField(prop);
                }
                else
                {
                    p = new PropertyOrField((FieldInfo)member);
                }

                // Check if the property should be serialized as an attribute instead of the default element
                var attr = member.GetCustomAttribute<XmlAttributeAttribute>();
                if (attr != null)
                {
                    var attrName = attr.AttributeName ?? member.Name;
                    create.AttributeProperties[attrName] = p;
                    continue;
                }

                if (p.Type.IsArray)
                {
                    var elementType = p.Type.GetElementType();

                    // Arrays can be serialized with a single container element (default) or with a flat list of elements per item
                    var arrayElements = member.GetCustomAttributes<XmlElementAttribute>().ToList();

                    if (arrayElements.Count == 0)
                    {
                        // Handle the array start element
                        var array = member.GetCustomAttribute<XmlArrayAttribute>();
                        var arrayName = array?.ElementName ?? member.Name;
                        var arrayState = new DfaState();
                        create.Next.Transitions.Add(new CreateArrayTransition
                        {
                            ElementName = arrayName,
                            ElementType = elementType,
                            Property = p,
                            Next = arrayState
                        });

                        // Ignore text nodes within the array
                        arrayState.Transitions.Add(new IgnoreTextTransition { Next = arrayState });

                        // Check if we've got specified array item elements
                        var arrayItems = member.GetCustomAttributes<XmlArrayItemAttribute>().ToList();

                        if (arrayItems.Count > 0)
                        {
                            foreach (var arrayItem in arrayItems)
                            {
                                var itemType = arrayItem.Type ?? elementType;
                                var itemName = arrayItem.ElementName ?? itemType.Name;

                                AddPolymorphicElement(arrayState, itemName, p, itemType);
                            }
                        }
                        else
                        {
                            AddPolymorphicElement(arrayState, elementType.Name, p, elementType);
                        }

                        // Handle the array end element
                        arrayState.Transitions.Add(new EndPropertyTransition
                        {
                            ElementName = arrayName,
                            Next = create.Next
                        });
                    }
                    else
                    {
                        foreach (var arrayElement in arrayElements)
                        {
                            var itemType = arrayElement.Type ?? elementType;
                            var itemName = arrayElement.ElementName ?? itemType.Name;

                            AddPolymorphicElement(create.Next, itemName, p, itemType);
                        }
                    }
                }
                else if (IsSimpleType(p.Type))
                {
                    var textAttr = member.GetCustomAttribute<XmlTextAttribute>();

                    if (textAttr != null)
                    {
                        create.Next.Transitions.Add(new SetPropertyTransition
                        {
                            Property = p,
                            Next = create.Next
                        });

                        hasTextProperty = true;
                    }
                    else
                    {
                        var element = member.GetCustomAttribute<XmlElementAttribute>();

                        var propState = new DfaState();
                        create.Next.Transitions.Add(new StartPropertyTransition
                        {
                            ElementName = element?.ElementName ?? member.Name,
                            Next = propState
                        });
                        propState.Transitions.Add(new SetPropertyTransition
                        {
                            Property = p,
                            Next = propState
                        });
                        propState.Transitions.Add(new EndPropertyTransition
                        {
                            ElementName = element?.ElementName ?? member.Name,
                            Next = create.Next
                        });
                    }
                }
                else
                {
                    var element = member.GetCustomAttribute<XmlElementAttribute>();
                    AddPolymorphicElement(create.Next, element?.ElementName ?? member.Name, p, null);
                }
            }

            if (!hasTextProperty)
                create.Next.Transitions.Add(new IgnoreTextTransition { Next = create.Next });
        }

        private void AddPolymorphicElement(DfaState from, string elementName, PropertyOrField property, Type type)
        {
            type = type ?? property.Type;

            if (!type.IsInterface && !type.IsAbstract)
            {
                var subCreate = new CreateInstanceTransition
                {
                    ElementName = elementName,
                    Property = property,
                    Type = type,
                    Next = new DfaState()
                };

                subCreate.Next.Transitions.Add(new EndInstanceTransition
                {
                    ElementName = subCreate.ElementName,
                    Next = from
                });
                from.Transitions.Add(subCreate);
            }

            var includes = type.GetCustomAttributes<XmlIncludeAttribute>().ToList();

            foreach (var include in includes)
            {
                var includedType = include.Type;

                var subCreate = new CreateInstanceTransition
                {
                    ElementName = elementName,
                    TypeName = includedType.Name,
                    Property = property,
                    Type = includedType,
                    Next = new DfaState()
                };

                subCreate.Next.Transitions.Add(new EndInstanceTransition
                {
                    ElementName = subCreate.ElementName,
                    Next = from
                });
                from.Transitions.Add(subCreate);
            }
        }

        private bool IsSimpleType(Type type)
        {
            return type.IsEnum ||
                type.IsPrimitive ||
                type == typeof(string);
        }
    }

    class CreateArrayTransition : ElementBasedTransition
    {
        public Type ElementType { get; set; }

        public PropertyOrField Property { get; set; }

        public override bool Accept(ParserState parser)
        {
            if (!(parser.Node is PartialXmlElement startElement))
                return false;

            if (startElement.Name != ElementName)
                return false;

            var array = Array.CreateInstance(ElementType, 0);
            var parent = parser.DeserializedStack.Peek();
            Property.SetValue(parent, array);

            return true;
        }
    }

    abstract class EndElementBasedTransition : DfaTransition
    {
        public string ElementName { get; set; }
    }

    class EndInstanceTransition : EndElementBasedTransition
    {
        public override bool Accept(ParserState parser)
        {
            if (!(parser.Node is PartialXmlEndElement endElement))
                return false;

            if (endElement.Name != ElementName)
                return false;

            parser.DeserializedStack.Pop();
            return true;
        }
    }

    class StartPropertyTransition : ElementBasedTransition
    {
        public string ElementName { get; set; }

        public override bool Accept(ParserState parser)
        {
            if (!(parser.Node is PartialXmlElement startElement))
                return false;

            if (startElement.Name != ElementName)
                return false;

            return true;
        }
    }

    class SetPropertyTransition : DfaTransition
    {
        public PropertyOrField Property { get; set; }

        public override bool Accept(ParserState parser)
        {
            if (!(parser.Node is PartialXmlText text))
                return false;

            var parent = parser.DeserializedStack.Peek();
            
            if (XmlTypeConverter.TryChangeType(Property.Type, text.Text, out var value))
                Property.SetValue(parent, value);

            return true;
        }
    }

    class EndPropertyTransition : EndElementBasedTransition
    {
        public override bool Accept(ParserState parser)
        {
            if (!(parser.Node is PartialXmlEndElement endElement))
                return false;

            if (endElement.Name != ElementName)
                return false;

            return true;
        }
    }
}
