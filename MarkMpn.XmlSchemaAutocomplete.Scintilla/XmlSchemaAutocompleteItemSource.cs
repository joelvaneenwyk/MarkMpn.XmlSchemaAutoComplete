using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AutocompleteMenuNS;

namespace MarkMpn.XmlSchemaAutocomplete.Scintilla
{
    public class XmlSchemaAutocompleteItemSource : IEnumerable<AutocompleteItem>
    {
        private readonly Autocomplete _autocomplete;
        private readonly ScintillaNET.Scintilla _scintilla;

        public XmlSchemaAutocompleteItemSource(Autocomplete autocomplete, ScintillaNET.Scintilla scintilla)
        {
            _autocomplete = autocomplete;
            _scintilla = scintilla;
        }

        public IEnumerator<AutocompleteItem> GetEnumerator()
        {
            var text = _scintilla.GetTextRange(0, _scintilla.CurrentPosition);
            var suggestions = _autocomplete.GetSuggestions(text, out var length);

            foreach (var suggestion in suggestions)
            {
                if (suggestion is AutocompleteElementSuggestion el)
                    yield return new XmlSchemaAutocompleteItem("<" + el.Name + (el.SelfClosing ? " />" : ""), 0, el.Name, "tooltip title", "tooltip text");
                else if (suggestion is AutocompleteAttributeSuggestion at)
                    yield return new XmlSchemaAutocompleteItem(at.Name, 1, at.Name, "tooltip title", "tooltip text");
                else if (suggestion is AutocompleteAttributeValueSuggestion av)
                    yield return new XmlSchemaAutocompleteItem($"{av.QuoteChar}{av.Value}{av.QuoteChar}", 2, av.Value, "tooltip title", "tooltip text");
                else if (suggestion is AutocompleteValueSuggestion v)
                    yield return new XmlSchemaAutocompleteItem(v.Value, 3, v.Value, "tooltip title", "tooltip text");
                else if (suggestion is AutocompleteEndElementSuggestion end)
                    yield return new XmlSchemaAutocompleteItem(end.IncludeSlash ? $"/{end.Name}" : end.Name, 4, $"/{end.Name}", "tooltip title", "tooltip text");
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        class XmlSchemaAutocompleteItem : AutocompleteItem
        {
            public XmlSchemaAutocompleteItem(string text, int imageIndex, string menuText, string tooltipTitle, string tooltip) : base(text, imageIndex, menuText, tooltipTitle, tooltip)
            {
            }

            public override CompareResult Compare(string fragmentText)
            {
                return CompareResult.VisibleAndSelected;
            }
        }
    }
}
