using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Schema;

namespace MarkMpn.XmlSchemaAutocomplete
{
    public abstract class AutocompleteSuggestion
    {
    }

    public class AutocompleteElementSuggestion : AutocompleteSuggestion
    {
        internal AutocompleteElementSuggestion(XmlSchemaElement element)
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
        public string Name { get; set; }

        public bool IncludeSlash { get; set; }
    }

    public class AutocompleteAttributeSuggestion : AutocompleteSuggestion
    {
        public string Name { get; set; }
    }

    public class AutocompleteAttributeValueSuggestion : AutocompleteSuggestion
    {
        public string Value { get; set; }

        public bool IncludeQuotes { get; set; }

        public char QuoteChar { get; set; }
    }

    public class AutocompleteValueSuggestion : AutocompleteSuggestion
    {
        public string Value { get; set; }
    }
}
