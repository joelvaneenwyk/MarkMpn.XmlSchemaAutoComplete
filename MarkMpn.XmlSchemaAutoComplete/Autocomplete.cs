using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace MarkMpn.XmlSchemaAutocomplete
{
#if !NETCOREAPP
    static class StackExtensions
    {
        public static bool TryPeek<T>(this Stack<T> stack, out T result)
        {
            if (stack.Count == 0)
            {
                result = default(T);
                return false;
            }

            result = stack.Peek();
            return true;
        }

        public static bool TryPop<T>(this Stack<T> stack, out T result)
        {
            if (stack.Count == 0)
            {
                result = default(T);
                return false;
            }

            result = stack.Pop();
            return true;
        }
    }
#endif

    public class Autocomplete
    {
        private readonly XmlSchemaSet _schemas;

        public Autocomplete(XmlSchemaSet schemas)
        {
            if (!schemas.IsCompiled)
                schemas.Compile();

            _schemas = schemas;
        }

        public bool UsesXsi { get; set; }

        public void AddTypeDescription(string typeName, string title, string description)
        {
            var type = _schemas.Schemas()
                .Cast<XmlSchema>()
                .SelectMany(schema => schema.SchemaTypes.Values.Cast<XmlSchemaType>())
                .FirstOrDefault(t => t.Name == typeName);

            if (type == null)
                throw new ArgumentOutOfRangeException(nameof(typeName), "Unknown type name " + typeName);

            if (type.Annotation != null)
                throw new ArgumentOutOfRangeException(nameof(typeName), "Type already has annotation");

            var doc = new XmlDocument();
            var h1 = doc.CreateElement("h1");
            h1.InnerText = title;
            var p = doc.CreateElement("p");
            p.InnerText = description;

            type.Annotation = new XmlSchemaAnnotation();
            type.Annotation.Items.Add(new XmlSchemaDocumentation
            {
                Markup = new XmlNode[]
                {
                    h1,
                    p
                }
            });
        }

        public void AddElementDescription(string typeName, string elementName, string title, string description)
        {
            var type = _schemas.Schemas()
                .Cast<XmlSchema>()
                .SelectMany(schema => schema.SchemaTypes.Values.Cast<XmlSchemaType>())
                .FirstOrDefault(t => t.Name == typeName);

            if (type == null)
                throw new ArgumentOutOfRangeException(nameof(typeName), "Unknown type name " + typeName);

            if (!(type is XmlSchemaComplexType complex))
                throw new ArgumentOutOfRangeException(nameof(typeName), "Type is not a complex type");

            XmlSchemaElement element;

            if (complex.Particle is XmlSchemaSequence sequence)
                element = sequence.Items.OfType<XmlSchemaElement>().SingleOrDefault(el => el.Name == elementName);
            else if (complex.Particle is XmlSchemaChoice choice)
                element = choice.Items.OfType<XmlSchemaElement>().SingleOrDefault(el => el.Name == elementName);
            else
                throw new ArgumentOutOfRangeException(nameof(typeName), "Type must be a sequence or choice");

            if (element == null)
                throw new ArgumentOutOfRangeException(nameof(elementName), "Unknown element name " + elementName);

            var doc = new XmlDocument();
            var h1 = doc.CreateElement("h1");
            h1.InnerText = title;
            var p = doc.CreateElement("p");
            p.InnerText = description;

            element.Annotation = new XmlSchemaAnnotation();
            element.Annotation.Items.Add(new XmlSchemaDocumentation
            {
                Markup = new XmlNode[]
                {
                    h1,
                    p
                }
            });
        }

        public void AddAttributeDescription(string typeName, string attributeName, string title, string description)
        {
            var type = _schemas.Schemas()
                .Cast<XmlSchema>()
                .SelectMany(schema => schema.SchemaTypes.Values.Cast<XmlSchemaType>())
                .FirstOrDefault(t => t.Name == typeName);

            if (type == null)
                throw new ArgumentOutOfRangeException(nameof(typeName), "Unknown type name " + typeName);

            if (!(type is XmlSchemaComplexType complex))
                throw new ArgumentOutOfRangeException(nameof(typeName), "Type is not a complex type");

            var attr = complex.AttributeUses.Values.OfType<XmlSchemaAttribute>().SingleOrDefault(el => el.Name == attributeName);

            if (attr == null)
                throw new ArgumentOutOfRangeException(nameof(attributeName), "Unknown attribute name " + attributeName);

            var doc = new XmlDocument();
            var h1 = doc.CreateElement("h1");
            h1.InnerText = title;
            var p = doc.CreateElement("p");
            p.InnerText = description;

            attr.Annotation = new XmlSchemaAnnotation();
            attr.Annotation.Items.Add(new XmlSchemaDocumentation
            {
                Markup = new XmlNode[]
                {
                    h1,
                    p
                }
            });
        }

        class ElementState
        {
            public ElementState(XmlSchemaElement schemaElement, XmlElement element)
            {
                SchemaElement = schemaElement;
                ElementCount = new Dictionary<XmlSchemaObject, int>();
                Element = element;
            }

            public XmlSchemaElement SchemaElement { get; }

            public string ElementName => SchemaElement.Name;

            public XmlSchemaType Type => SchemaElement.ElementSchemaType;

            public bool IsNillable => SchemaElement.IsNillable;

            public Dictionary<XmlSchemaObject, int> ElementCount { get; }

            public int NextChildElement { get; set; }

            public int RepeatCount { get; set; }

            public XmlElement Element { get; }
        }

        public event EventHandler<AutocompleteValueEventArgs> AutocompleteValue;

        public event EventHandler<AutocompleteAttributeValueEventArgs> AutocompleteAttributeValue;

        public AutocompleteSuggestion[] GetSuggestions(string text, out int length)
        {
            length = 0;

            var parser = new PartialXmlReader(text);
            var elements = new Stack<ElementState>();
            PartialXmlElement firstElement = null;
            PartialXmlNode lastNode = null;
            var valid = true;
            var document = new XmlDocument();

            while (parser.TryRead(out var node))
            {
                lastNode = node;

                if (node is PartialXmlProcessingInstruction)
                    continue;

                if (node is PartialXmlText txt && String.IsNullOrWhiteSpace(txt.Text))
                    continue;

                if (!valid)
                    return Array.Empty<AutocompleteSuggestion>();

                if (firstElement == null)
                    firstElement = node as PartialXmlElement;

                if (elements.Count == 0 && node == firstElement)
                {
                    if (!(node is PartialXmlElement elem))
                        return Array.Empty<AutocompleteSuggestion>();

                    foreach (XmlSchemaElement rootElement in _schemas.GlobalElements.Values)
                    {
                        if (rootElement.Name == elem.Name)
                        {
                            // Add the element to the document
                            var newElement = CreateElement(document, elem);
                            document.AppendChild(newElement);
                            elements.Push(new ElementState(rootElement, newElement));
                            break;
                        }
                    }

                    if (elements.Count == 0)
                        valid = false;
                }
                else if (node is PartialXmlEndElement end)
                {
                    if (!elements.TryPop(out var lastElement))
                        return Array.Empty<AutocompleteSuggestion>();
                    
                    if (lastElement.ElementName != end.Name)
                    {
                        if (parser.State == ReaderState.InEndElement)
                        {
                            elements.Push(lastElement);
                            valid = false;
                        }
                        else
                        {
                            return Array.Empty<AutocompleteSuggestion>();
                        }
                    }
                }
                else if (node is PartialXmlElement elem)
                {
                    if (!elements.TryPeek(out var currentElement))
                        return Array.Empty<AutocompleteSuggestion>();

                    if (!(currentElement.Type is XmlSchemaComplexType complex))
                        return Array.Empty<AutocompleteSuggestion>();

                    if (complex.ContentTypeParticle is XmlSchemaSequence sequence)
                    {
                        var foundMatch = false;

                        while (currentElement.RepeatCount < sequence.MaxOccurs && !foundMatch)
                        {
                            var atSequenceStart = currentElement.NextChildElement == 0;

                            for (var i = currentElement.NextChildElement; i < sequence.Items.Count; i++)
                            {
                                var particle = sequence.Items[i] as XmlSchemaParticle;
                                currentElement.ElementCount.TryGetValue(sequence.Items[i], out var count);
                                XmlSchemaElement matchedElement = null;

                                if (sequence.Items[i] is XmlSchemaChoice choice && MatchChoice(choice, elem, out matchedElement))
                                {
                                    foundMatch = true;
                                }
                                else if (sequence.Items[i] is XmlSchemaElement childElement && childElement.Name == elem.Name)
                                {
                                    matchedElement = childElement;
                                    foundMatch = true;
                                }

                                if (foundMatch)
                                {
                                    count++;
                                    currentElement.ElementCount[sequence.Items[i]] = count;

                                    if (particle != null)
                                    {
                                        if (count == particle.MaxOccurs)
                                            currentElement.NextChildElement = i + 1;
                                        else
                                            currentElement.NextChildElement = i;
                                    }

                                    var newElement = CreateElement(document, elem);
                                    currentElement.Element.AppendChild(newElement);
                                    elements.Push(new ElementState(matchedElement, newElement));
                                    break;
                                }

                                if (particle != null && particle.MinOccurs > count)
                                    valid = false;
                            }

                            if (!foundMatch)
                            {
                                if (parser.State == ReaderState.InStartElement)
                                    break;

                                currentElement.RepeatCount++;
                                currentElement.NextChildElement = 0;
                                currentElement.ElementCount.Clear();

                                if (atSequenceStart)
                                    valid = false;
                            }
                        }

                        if (currentElement.RepeatCount == complex.ContentTypeParticle.MaxOccurs)
                            valid = false;
                    }
                    else if (complex.ContentTypeParticle is XmlSchemaChoice choice)
                    {
                        if (currentElement.RepeatCount < choice.MaxOccurs)
                        {
                            if (MatchChoice(choice, elem, out var matchedElement))
                            {
                                var newElement = CreateElement(document, elem);
                                currentElement.Element.AppendChild(newElement);
                                elements.Push(new ElementState(matchedElement, newElement));
                            }
                            else
                            {
                                valid = false;
                            }
                        }
                    }
                    else
                    {
                        return Array.Empty<AutocompleteSuggestion>();
                    }

                    if (valid && elem.SelfClosing)
                        elements.Pop();
                }
            }

            if (lastNode is PartialXmlElement element)
            {
                if (parser.State == ReaderState.InStartElement)
                {
                    length = element.Name.Length;

                    if (element == firstElement)
                    {
                        // Suggest possible root elements
                        return _schemas.GlobalElements.Values
                            .Cast<XmlSchemaElement>()
                            .Where(e => e.Name.StartsWith(element.Name))
                            .Select(e => new AutocompleteElementSuggestion(e))
                            .ToArray<AutocompleteSuggestion>();
                    }

                    var suggestions = new List<AutocompleteSuggestion>();

                    if (elements.TryPeek(out var currentElement))
                    {
                        var canClose = String.IsNullOrEmpty(element.Name);

                        if (currentElement.Type is XmlSchemaComplexType complex)
                        {
                            if (complex.ContentTypeParticle is XmlSchemaSequence sequence)
                            {
                                foreach (var child in sequence.Items.Cast<XmlSchemaObject>().Skip(currentElement.NextChildElement))
                                {
                                    if (child is XmlSchemaChoice choice)
                                    {
                                        foreach (var choiceItem in choice.Items)
                                        {
                                            if (!(choiceItem is XmlSchemaElement childElement))
                                                break;

                                            if (childElement.Name.StartsWith(element.Name))
                                                suggestions.Add(new AutocompleteElementSuggestion(childElement));
                                        }
                                    }
                                    else if (child is XmlSchemaElement childElement)
                                    {
                                        if (childElement.Name.StartsWith(element.Name))
                                            suggestions.Add(new AutocompleteElementSuggestion(childElement));

                                        currentElement.ElementCount.TryGetValue(childElement, out var count);
                                        if (childElement.MinOccurs > count)
                                            break;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }

                                if (canClose)
                                {
                                    // This element can only be closed if all the child elements have reached their minimum number
                                    canClose = sequence.Items
                                        .Cast<XmlSchemaObject>()
                                        .Skip(currentElement.NextChildElement)
                                        .OfType<XmlSchemaParticle>()
                                        .All(particle =>
                                        {
                                            currentElement.ElementCount.TryGetValue(particle, out var count);
                                            return count >= particle.MinOccurs;
                                        });
                                }
                            }
                            else if (complex.ContentTypeParticle is XmlSchemaChoice choice)
                            {
                                foreach (var child in choice.Items)
                                {
                                    if (!(child is XmlSchemaElement childElement))
                                        break;

                                    if (childElement.Name.StartsWith(element.Name))
                                        suggestions.Add(new AutocompleteElementSuggestion(childElement));
                                }

                                canClose = currentElement.RepeatCount >= choice.MinOccurs;
                            }
                        }

                        if (canClose)
                            suggestions.Add(new AutocompleteEndElementSuggestion(currentElement.SchemaElement) { IncludeSlash = true });
                    }

                    return suggestions.ToArray();
                }
                else if ((parser.State == ReaderState.AwaitingAttribute && Char.IsWhiteSpace(text[text.Length - 1])) || parser.State == ReaderState.InAttributeName)
                {
                    length = element.CurrentAttribute?.Length ?? 0;

                    var suggestions = new List<AutocompleteAttributeSuggestion>();

                    if (elements.TryPeek(out var currentElement) &&
                        currentElement.Type is XmlSchemaComplexType complex)
                    {
                        suggestions.AddRange(complex.AttributeUses
                            .Values
                            .Cast<XmlSchemaAttribute>()
                            .Select(a => new AutocompleteAttributeSuggestion(a))
                        );

                        // Sort all the attributes by name
                        suggestions.Sort((x, y) => x.Name.CompareTo(y.Name));
                    }

                    // Special cases for xsi:type

                    // If this type has derived types, offer them too
                    if (_schemas.Schemas().Cast<XmlSchema>().Any(schema => schema.SchemaTypes.Values.OfType<XmlSchemaComplexType>().Any(type => type.BaseXmlSchemaType == currentElement.Type)))
                    {
                        suggestions.Insert(0, new AutocompleteAttributeSuggestion { Name = "xsi:type", Title = "Type", Description = "Indicates the derived type to use for this element" });
                    }

                    // Offer xsi:nil for nillable types
                    if (currentElement.IsNillable)
                    {
                        suggestions.Insert(0, new AutocompleteAttributeSuggestion { Name = "xsi:nil", Title = "Nil", Description = "Indicates that this element has no value" });
                    }

                    // If this is the root element and we have any extension types, offer the xmlns:xsi attribute
                    if (UsesXsi && element == firstElement && _schemas.Schemas().Cast<XmlSchema>().Any(schema => schema.SchemaTypes.Values.OfType<XmlSchemaComplexType>().Any(type => type.BaseXmlSchemaType != null)))
                    {
                        suggestions.Insert(0, new AutocompleteAttributeSuggestion { Name = "xmlns:xsi", Title = "XML Schema Instance", Description = "Includes the XML Schema Instance namespace" });
                    }

                    return suggestions
                        .Where(a => a.Name.StartsWith(element.CurrentAttribute ?? "") && !element.Attributes.ContainsKey(a.Name))
                        .ToArray<AutocompleteSuggestion>();
                }
                else if (parser.State == ReaderState.InAttributeEquals || parser.State == ReaderState.InAttributeValue)
                {
                    if (parser.State == ReaderState.InAttributeValue)
                        length = element.Attributes[element.CurrentAttribute].Length;

                    var suggestions = new List<AutocompleteAttributeValueSuggestion>();

                    if (elements.TryPeek(out var currentElement) &&
                        currentElement.Type is XmlSchemaComplexType complex)
                    {
                        var attribute = complex.AttributeUses
                            .Values
                            .Cast<XmlSchemaAttribute>()
                            .SingleOrDefault(a => a.Name == element.CurrentAttribute);

                        if (attribute != null)
                        {
                            if (attribute.AttributeSchemaType.TypeCode == XmlTypeCode.Boolean)
                            {
                                suggestions.Add(new AutocompleteAttributeValueSuggestion { Value = "false" });
                                suggestions.Add(new AutocompleteAttributeValueSuggestion { Value = "true" });
                            }
                            else if (attribute.AttributeSchemaType.Content is XmlSchemaSimpleTypeRestriction attrValues)
                            {
                                suggestions.AddRange(attrValues.Facets
                                    .OfType<XmlSchemaEnumerationFacet>()
                                    .Select(value => new AutocompleteAttributeValueSuggestion(value))
                                    );
                            }
                        }

                        // Use the event callback to gather suggestions
                        if (AutocompleteAttributeValue != null && attribute != null)
                        {
                            var schemaTypes = new Stack<XmlSchemaType>();
                            var schemaElements = new Stack<string>();

                            foreach (var el in elements.Reverse())
                            {
                                schemaTypes.Push(el.Type);
                                schemaElements.Push(el.ElementName);
                            }

                            AutocompleteAttributeValue(this, new AutocompleteAttributeValueEventArgs(suggestions, currentElement.Element, schemaTypes, schemaElements, attribute));
                        }
                    }

                    // Special cases for xsi
                    if (element.CurrentAttribute == "xmlns:xsi")
                    {
                        suggestions.Add(new AutocompleteAttributeValueSuggestion { Value = "http://www.w3.org/2001/XMLSchema-instance" });
                    }
                    else if (element.CurrentAttribute == "xsi:type")
                    {
                        suggestions.AddRange(_schemas.Schemas()
                            .Cast<XmlSchema>()
                            .SelectMany(schema =>
                                schema.SchemaTypes
                                    .Values
                                    .OfType<XmlSchemaComplexType>()
                                    .Where(type => type.BaseXmlSchemaType == currentElement.Type)
                                    .Select(type => new AutocompleteAttributeValueSuggestion(type))
                            )
                        );
                    }
                    else if (element.CurrentAttribute == "xsi:nil")
                    {
                        suggestions.Add(new AutocompleteAttributeValueSuggestion { Value = "true" });
                        suggestions.Add(new AutocompleteAttributeValueSuggestion { Value = "false" });
                    }

                    foreach (var suggestion in suggestions)
                    {
                        suggestion.IncludeQuotes = parser.State == ReaderState.InAttributeEquals;
                        suggestion.QuoteChar = parser.State == ReaderState.InAttributeEquals ? '"' : text[text.Length - element.Attributes[element.CurrentAttribute].Length - 1];
                    }

                    return suggestions
                        .Where(a => a.Value.StartsWith(element.Attributes[element.CurrentAttribute] ?? ""))
                        .ToArray<AutocompleteSuggestion>();
                }
                else if (parser.State == ReaderState.InText)
                {
                    return CompleteTextNode(elements, "");
                }
            }
            else if (lastNode is PartialXmlText txt)
            {
                length = txt.Text.Length;
                return CompleteTextNode(elements, txt.Text);
            }
            else if (lastNode is PartialXmlEndElement endElement)
            {
                if (elements.TryPeek(out var currentElement) &&
                    currentElement.ElementName.StartsWith(endElement.Name))
                    return new AutocompleteSuggestion[] { new AutocompleteEndElementSuggestion(currentElement.SchemaElement) };
            }

            return Array.Empty<AutocompleteSuggestion>();
        }

        private bool MatchChoice(XmlSchemaChoice choice, PartialXmlElement elem, out XmlSchemaElement matchedElement)
        {
            matchedElement = null;

            for (var i = 0; i < choice.Items.Count; i++)
            {
                if (!(choice.Items[i] is XmlSchemaElement childElement))
                    return false;

                if (childElement.Name == elem.Name)
                {
                    matchedElement = childElement;
                    return true;
                }
            }

            return false;
        }

        private AutocompleteSuggestion[] CompleteTextNode(Stack<ElementState> elements, string text)
        {
            if (!elements.TryPeek(out var currentElement))
                return Array.Empty<AutocompleteSuggestion>();

            if (currentElement.Type is XmlSchemaSimpleType simple)
            {
                if (simple.TypeCode == XmlTypeCode.Boolean)
                {
                    return new[] { "false", "true" }
                        .Where(value => value.StartsWith(text.Trim()))
                        .Select(value => new AutocompleteValueSuggestion { Value = value })
                        .ToArray<AutocompleteSuggestion>();
                }

                if (simple.Content is XmlSchemaSimpleTypeRestriction attrValues &&
                    attrValues.Facets.Count > 0)
                {
                    return attrValues.Facets
                        .OfType<XmlSchemaEnumerationFacet>()
                        .Where(value => value.Value.StartsWith(text.Trim()))
                        .Select(value => new AutocompleteValueSuggestion(value))
                        .ToArray<AutocompleteSuggestion>();
                }
            }

            // Use the event callback to gather suggestions
            if (AutocompleteValue != null)
            {
                var suggestions = new List<AutocompleteValueSuggestion>();
                var schemaTypes = new Stack<XmlSchemaType>();
                var schemaElements = new Stack<string>();

                foreach (var el in elements.Reverse())
                {
                    schemaTypes.Push(el.Type);
                    schemaElements.Push(el.ElementName);
                }

                AutocompleteValue(this, new AutocompleteValueEventArgs(suggestions, currentElement.Element, schemaTypes, schemaElements));
                return suggestions.ToArray<AutocompleteSuggestion>();
            }

            return Array.Empty<AutocompleteSuggestion>();
        }

        private XmlElement CreateElement(XmlDocument document, PartialXmlElement elem)
        {
            var newElement = document.CreateElement(elem.Name);

            foreach (var attr in elem.Attributes)
                newElement.SetAttribute(attr.Key, attr.Value);

            return newElement;
        }
    }

    public class AutocompleteValueEventArgs : EventArgs
    {
        internal AutocompleteValueEventArgs(List<AutocompleteValueSuggestion> suggestions, XmlElement element, Stack<XmlSchemaType> schemaTypes, Stack<string> schemaElements)
        {
            Suggestions = suggestions;
            Element = element;
            SchemaTypes = schemaTypes;
            SchemaElements = schemaElements;
        }

        public List<AutocompleteValueSuggestion> Suggestions { get; }

        public XmlElement Element { get; }

        public Stack<XmlSchemaType> SchemaTypes { get; }

        public Stack<string> SchemaElements { get; }
    }

    public class AutocompleteAttributeValueEventArgs : EventArgs
    {
        internal AutocompleteAttributeValueEventArgs(List<AutocompleteAttributeValueSuggestion> suggestions, XmlElement element, Stack<XmlSchemaType> schemaTypes, Stack<string> schemaElements, XmlSchemaAttribute attribute)
        {
            Suggestions = suggestions;
            Element = element;
            SchemaTypes = schemaTypes;
            SchemaElements = schemaElements;
            Attribute = attribute;
        }

        public List<AutocompleteAttributeValueSuggestion> Suggestions { get; }

        public XmlElement Element { get; }

        public Stack<XmlSchemaType> SchemaTypes { get; }

        public Stack<string> SchemaElements { get; }

        public XmlSchemaAttribute Attribute { get; }
    }

    public class AutocompleteDocumentation
    {
        public string Title { get; set; }

        public string Description { get; set; }
    }

    public class Autocomplete<T> : Autocomplete
    {
        public Autocomplete() : base(GetSchemaSet())
        {
        }

        private static XmlSchemaSet GetSchemaSet()
        {
            var schemas = new XmlSchemas();
            var exporter = new XmlSchemaExporter(schemas);
            var mapping = new XmlReflectionImporter().ImportTypeMapping(typeof(T));
            exporter.ExportTypeMapping(mapping);

            var writer = new StringWriter();
            foreach (XmlSchema schema in schemas)
                schema.Write(writer);

            schemas.Compile(null, true);

            var schemaSet = new XmlSchemaSet();
            foreach (XmlSchema schema in schemas)
                schemaSet.Add(schema);

            return schemaSet;
        }

        public void AddTypeDescription<TObject>(string title, string description)
        {
            var schemas = new XmlSchemas();
            var exporter = new XmlSchemaExporter(schemas);
            var mapping = new XmlReflectionImporter().ImportTypeMapping(typeof(TObject));
            exporter.ExportTypeMapping(mapping);

            base.AddTypeDescription(mapping.XsdTypeName, title, description);
        }

        public void AddMemberDescription<TObject>(string memberName, string title, string description)
        {
            var member = typeof(TObject).GetMember(memberName).SingleOrDefault();

            if (member == null)
                throw new ArgumentOutOfRangeException(nameof(memberName), "Unknown member " + memberName);

            var attr = member.GetCustomAttributes(typeof(XmlAttributeAttribute));

            var schemas = new XmlSchemas();
            var exporter = new XmlSchemaExporter(schemas);
            var mapping = new XmlReflectionImporter().ImportTypeMapping(typeof(TObject));
            exporter.ExportTypeMapping(mapping);

            if (attr.Any())
                AddAttributeDescription(mapping.XsdTypeName, memberName, title, description);
            else
                AddElementDescription(mapping.XsdTypeName, memberName, title, description);
        }
    }
}
