using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace MarkMpn.XmlSchemaAutocomplete
{
    public class Autocomplete
    {
        private readonly XmlSchemaSet _schemas;

        public Autocomplete(XmlSchemaSet schemas)
        {
            if (!schemas.IsCompiled)
                schemas.Compile();

            _schemas = schemas;
        }

        class ElementState
        {
            public ElementState(XmlSchemaElement element)
            {
                ElementName = element.Name;
                Type = element.ElementSchemaType;
                IsNillable = element.IsNillable;
                ElementCount = new Dictionary<XmlSchemaObject, int>();
            }

            public string ElementName { get; }

            public XmlSchemaType Type { get; }

            public bool IsNillable { get; }

            public Dictionary<XmlSchemaObject, int> ElementCount { get; }

            public int NextChildElement { get; set; }

            public int RepeatCount { get; set; }
        }

        public AutocompleteSuggestion[] GetSuggestions(string text)
        {
            var parser = new PartialXmlReader(text);
            var elements = new Stack<ElementState>();
            PartialXmlElement firstElement = null;
            PartialXmlNode lastNode = null;
            var valid = true;

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
                            elements.Push(new ElementState(rootElement));
                            break;
                        }
                    }

                    if (elements.Count == 0)
                        valid = false;
                }
                else if (node is PartialXmlEndElement end)
                {
                    if (!elements.TryPop(out var lastElement)
                        || lastElement.ElementName != end.Name)
                    {
                        return Array.Empty<AutocompleteSuggestion>();
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
                                if (!(sequence.Items[i] is XmlSchemaElement childElement))
                                    return Array.Empty<AutocompleteSuggestion>();

                                currentElement.ElementCount.TryGetValue(childElement, out var count);

                                if (childElement.Name == elem.Name)
                                {
                                    count++;
                                    currentElement.ElementCount[childElement] = count;

                                    if (count == childElement.MaxOccurs)
                                        currentElement.NextChildElement = i + 1;
                                    else
                                        currentElement.NextChildElement = i;

                                    elements.Push(new ElementState(childElement));
                                    foundMatch = true;
                                    break;
                                }

                                if (childElement.MinOccurs > count)
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
                            var foundMatch = false;

                            for (var i = 0; i < choice.Items.Count; i++)
                            {
                                if (!(choice.Items[i] is XmlSchemaElement childElement))
                                    return Array.Empty<AutocompleteSuggestion>();

                                if (childElement.Name == elem.Name)
                                {
                                    elements.Push(new ElementState(childElement));
                                    foundMatch = true;
                                    break;
                                }
                            }

                            if (!foundMatch)
                                valid = false;
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
                    if (element == firstElement)
                    {
                        // Suggest possible root elements
                        return _schemas.GlobalElements.Values
                            .Cast<XmlSchemaElement>()
                            .Where(e => e.Name.StartsWith(element.Name))
                            .Select(e => new AutocompleteElementSuggestion { Name = e.Name })
                            .ToArray<AutocompleteSuggestion>();
                    }

                    if (elements.TryPeek(out var currentElement) &&
                        currentElement.Type is XmlSchemaComplexType complex)
                    {
                        if (complex.ContentTypeParticle is XmlSchemaSequence sequence)
                        {
                            var suggestions = new List<AutocompleteSuggestion>();

                            foreach (var child in sequence.Items.Cast<XmlSchemaObject>().Skip(currentElement.NextChildElement))
                            {
                                if (!(child is XmlSchemaElement childElement))
                                    break;

                                if (childElement.Name.StartsWith(element.Name))
                                    suggestions.Add(new AutocompleteElementSuggestion { Name = childElement.Name });

                                currentElement.ElementCount.TryGetValue(childElement, out var count);
                                if (childElement.MinOccurs > count)
                                    break;
                            }

                            return suggestions.ToArray();
                        }
                        else if (complex.ContentTypeParticle is XmlSchemaChoice choice)
                        {
                            var suggestions = new List<AutocompleteSuggestion>();

                            foreach (var child in choice.Items)
                            {
                                if (!(child is XmlSchemaElement childElement))
                                    break;

                                if (childElement.Name.StartsWith(element.Name))
                                    suggestions.Add(new AutocompleteElementSuggestion { Name = childElement.Name });
                            }

                            return suggestions.ToArray();
                        }
                    }
                }
                else if (parser.State == ReaderState.AwaitingAttribute || parser.State == ReaderState.InAttributeName)
                {
                    var suggestions = new List<AutocompleteAttributeSuggestion>();

                    if (elements.TryPeek(out var currentElement) &&
                        currentElement.Type is XmlSchemaComplexType complex)
                    {
                        suggestions.AddRange(complex.Attributes
                            .Cast<XmlSchemaAttribute>()
                            .Select(a => new AutocompleteAttributeSuggestion { Name = a.Name })
                        );

                        // Recurse through base types adding their attributes too
                        var baseType = complex.BaseXmlSchemaType;
                        while (baseType is XmlSchemaComplexType baseComplex)
                        {
                            suggestions.AddRange(baseComplex.Attributes.Cast<XmlSchemaAttribute>().Select(a => new AutocompleteAttributeSuggestion { Name = a.Name }));
                            baseType = baseType.BaseXmlSchemaType;
                        }

                        // Sort all the attributes by name
                        suggestions.Sort((x, y) => x.Name.CompareTo(y.Name));

                    }

                    // Special cases for xsi:type

                    // If this type has derived types, offer them too
                    if (_schemas.Schemas().Cast<XmlSchema>().Any(schema => schema.SchemaTypes.Values.OfType<XmlSchemaComplexType>().Any(type => type.BaseXmlSchemaType == currentElement.Type)))
                    {
                        suggestions.Insert(0, new AutocompleteAttributeSuggestion { Name = "xsi:type" });
                    }

                    // Offer xsi:nil for nillable types
                    if (currentElement.IsNillable)
                    {
                        suggestions.Insert(0, new AutocompleteAttributeSuggestion { Name = "xsi:nil" });
                    }

                    // If this is the root element and we have any extension types, offer the xmlns:xsi attribute
                    if (element == firstElement && _schemas.Schemas().Cast<XmlSchema>().Any(schema => schema.SchemaTypes.Values.OfType<XmlSchemaComplexType>().Any(type => type.BaseXmlSchemaType != null)))
                    {
                        suggestions.Insert(0, new AutocompleteAttributeSuggestion { Name = "xmlns:xsi" });
                    }

                    return suggestions
                        .Where(a => a.Name.StartsWith(element.CurrentAttribute ?? ""))
                        .ToArray<AutocompleteSuggestion>();
                }
                else if (parser.State == ReaderState.InAttributeEquals || parser.State == ReaderState.InAttributeValue)
                {
                    var suggestions = new List<AutocompleteAttributeValueSuggestion>();

                    if (elements.TryPeek(out var currentElement) &&
                        currentElement.Type is XmlSchemaComplexType complex)
                    {
                        var attribute = complex.Attributes
                            .Cast<XmlSchemaAttribute>()
                            .SingleOrDefault(a => a.Name == element.CurrentAttribute);

                        if (attribute != null && attribute.AttributeSchemaType.Content is XmlSchemaSimpleTypeRestriction attrValues)
                        {
                            suggestions.AddRange(attrValues.Facets
                                .OfType<XmlSchemaEnumerationFacet>()
                                .Select(value => new AutocompleteAttributeValueSuggestion { Value = value.Value })
                                );
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
                                    .Select(type => new AutocompleteAttributeValueSuggestion { Value = type.Name })
                            )
                        );
                    }
                    else if (element.CurrentAttribute == "xsi:nil")
                    {
                        suggestions.Add(new AutocompleteAttributeValueSuggestion { Value = "true" });
                        suggestions.Add(new AutocompleteAttributeValueSuggestion { Value = "false" });
                    }

                    foreach (var suggestion in suggestions)
                        suggestion.IncludeQuotes = parser.State == ReaderState.InAttributeEquals;

                    return suggestions
                        .Where(a => a.Value.StartsWith(element.Attributes[element.CurrentAttribute] ?? ""))
                        .ToArray<AutocompleteSuggestion>();
                }
                else if (parser.State == ReaderState.InText)
                {
                    if (elements.TryPeek(out var currentElement) &&
                        currentElement.Type is XmlSchemaSimpleType simple &&
                        simple.Content is XmlSchemaSimpleTypeRestriction attrValues)
                    {
                        return attrValues.Facets
                            .OfType<XmlSchemaEnumerationFacet>()
                            .Select(value => new AutocompleteValueSuggestion { Value = value.Value })
                            .ToArray<AutocompleteSuggestion>();
                    }
                }
            }
            else if (lastNode is PartialXmlText txt)
            {
                if (elements.TryPeek(out var currentElement) &&
                    currentElement.Type is XmlSchemaSimpleType simple &&
                    simple.Content is XmlSchemaSimpleTypeRestriction attrValues)
                {
                    return attrValues.Facets
                        .OfType<XmlSchemaEnumerationFacet>()
                        .Where(value => value.Value.StartsWith(txt.Text.Trim()))
                        .Select(value => new AutocompleteValueSuggestion { Value = value.Value })
                        .ToArray<AutocompleteSuggestion>();
                }
            }

            return Array.Empty<AutocompleteSuggestion>();
        }
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
    }
}
