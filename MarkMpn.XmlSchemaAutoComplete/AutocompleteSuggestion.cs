using System;
using System.Collections.Generic;
using System.Text;

namespace MarkMpn.XmlSchemaAutocomplete
{
    public abstract class AutocompleteSuggestion
    {
    }

    public class AutocompleteElementSuggestion : AutocompleteSuggestion
    {
        public string Name { get; set; }
    }

    public class AutocompleteAttributeSuggestion : AutocompleteSuggestion
    {
        public string Name { get; set; }
    }

    public class AutocompleteAttributeValueSuggestion : AutocompleteSuggestion
    {
        public string Value { get; set; }

        public bool IncludeQuotes { get; set; }
    }

    public class AutocompleteValueSuggestion : AutocompleteSuggestion
    {
        public string Value { get; set; }
    }
}
