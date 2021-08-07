using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace MarkMpn.XmlSchemaAutocomplete
{
    public abstract class AutocompleteSuggestion
    {
        public AutocompleteSuggestion()
        {
        }

        public AutocompleteSuggestion(XmlSchemaAnnotated annotated)
        {
            if (annotated.Annotation == null)
            {
                if (annotated is XmlSchemaElement element)
                    annotated = element.ElementSchemaType;
                else if (annotated is XmlSchemaAttribute attribute)
                    annotated = attribute.AttributeSchemaType;
            }

            if (annotated.Annotation != null)
            {
                var documentation = annotated.Annotation.Items.OfType<XmlSchemaDocumentation>().SingleOrDefault();

                if (documentation != null)
                {
                    Title = documentation.Markup.OfType<XmlElement>().Where(el => el.Name == "h1").FirstOrDefault()?.InnerText;
                    Description = documentation.Markup.OfType<XmlElement>().Where(el => el.Name == "p").FirstOrDefault()?.InnerText;
                }
            }
        }

        public string Title { get; set; }

        public string Description { get; set; }

        public string DisplayName { get; set; }
    }

    public class AutocompleteElementSuggestion : AutocompleteSuggestion
    {
        internal AutocompleteElementSuggestion(XmlSchemaElement element) : base(element)
        {
            Name = element.Name;

            if (element.ElementSchemaType is XmlSchemaComplexType complex)
            {
                SelfClosing = complex.ContentType == XmlSchemaContentType.Empty;
                HasAttributes = complex.AttributeUses.Count > 0;
            }
        }

        public AutocompleteElementSuggestion()
        {
        }

        public string Name { get; set; }

        public bool SelfClosing { get; set; }

        public bool HasAttributes { get; set; }
    }

    public class AutocompleteEndElementSuggestion : AutocompleteSuggestion
    {
        internal AutocompleteEndElementSuggestion(XmlSchemaElement element) : base(element)
        {
            Name = element.Name;
        }

        public AutocompleteEndElementSuggestion()
        {
        }

        public string Name { get; set; }

        public bool IncludeSlash { get; set; }
    }

    public class AutocompleteAttributeSuggestion : AutocompleteSuggestion
    {
        public AutocompleteAttributeSuggestion(XmlSchemaAttribute attribute) : base(attribute)
        {
            Name = attribute.Name;
        }

        public AutocompleteAttributeSuggestion()
        {
        }

        public string Name { get; set; }
    }

    public class AutocompleteAttributeValueSuggestion : AutocompleteSuggestion
    {
        public AutocompleteAttributeValueSuggestion(XmlSchemaEnumerationFacet facet) : base(facet)
        {
            Value = facet.Value;
        }

        public AutocompleteAttributeValueSuggestion(XmlSchemaType type) : base(type)
        {
            Value = type.Name;
        }

        public AutocompleteAttributeValueSuggestion()
        {
        }

        public string Value { get; set; }

        public bool IncludeQuotes { get; set; }

        public char QuoteChar { get; set; }
    }

    public class AutocompleteValueSuggestion : AutocompleteSuggestion
    {
        public AutocompleteValueSuggestion(XmlSchemaEnumerationFacet facet) : base(facet)
        {
            Value = facet.Value;
        }

        public AutocompleteValueSuggestion()
        {
        }

        public string Value { get; set; }
    }
}
