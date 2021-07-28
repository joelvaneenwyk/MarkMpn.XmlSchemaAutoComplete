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
                ElementCount = new Dictionary<XmlSchemaObject, int>();
            }

            public string ElementName { get; }

            public XmlSchemaType Type { get; }

            public Dictionary<XmlSchemaObject, int> ElementCount { get; }

            public int NextChildElement { get; set; }
        }

        public AutocompleteSuggestion[] GetSuggestions(string text)
        {
            var parser = new PartialXmlReader(text);
            var elements = new Stack<ElementState>();
            PartialXmlNode lastNode = null;

            while (parser.TryRead(out var node))
            {
                lastNode = node;

                if (node is PartialXmlProcessingInstruction)
                    continue;

                if (node is PartialXmlText txt && String.IsNullOrWhiteSpace(txt.Text))
                    continue;

                if (elements.Count == 0)
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
                    {
                        // Suggest possible root elements
                        return _schemas.GlobalElements.Values
                            .Cast<XmlSchemaElement>()
                            .Where(e => e.Name.StartsWith(elem.Name))
                            .Select(e => new AutocompleteElementSuggestion { Name = e.Name })
                            .ToArray<AutocompleteSuggestion>();
                    }
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

                    if (!(complex.ContentTypeParticle is XmlSchemaSequence sequence))
                        return Array.Empty<AutocompleteSuggestion>();

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
                            break;
                        }

                        if (childElement.MinOccurs > count)
                            return Array.Empty<AutocompleteSuggestion>();
                    }
                }
            }

            if (lastNode is PartialXmlElement element)
            {
                if (parser.State == ReaderState.InStartElement)
                {
                    if (elements.TryPeek(out var currentElement) &&
                        currentElement.Type is XmlSchemaComplexType complex &&
                        complex.ContentTypeParticle is XmlSchemaSequence sequence)
                    {
                        var suggestions = new List<AutocompleteSuggestion>();

                        foreach (var child in sequence.Items)
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
