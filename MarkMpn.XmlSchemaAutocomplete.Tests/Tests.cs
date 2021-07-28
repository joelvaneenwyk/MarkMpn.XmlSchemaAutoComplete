using System;
using System.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;
using MarkMpn.XmlSchemaAutocomplete.Tests.Model;
using Xunit;

namespace MarkMpn.XmlSchemaAutocomplete.Tests
{
    public class Tests
    {
        [Theory]
        [InlineData("<", "MyDoc")]
        [InlineData("<M", "MyDoc")]
        [InlineData("<m")]
        public void SuggestsAllPossibleRootElements(string input, params string[] elements)
        {
            var autocomplete = new Autocomplete<Root>();
            var suggestions = autocomplete.GetSuggestions(input);
            var expected = elements.Select(e => new AutocompleteElementSuggestion { Name = e }).ToArray<AutocompleteSuggestion>();
            Assert.Equal(expected, suggestions, new PropertyComparer());
        }

        [Theory]
        [InlineData("<MyDoc><", "Members", "Staff")]
        [InlineData("<MyDoc><M", "Members")]
        [InlineData("<MyDoc><S", "Staff")]
        [InlineData("<MyDoc><x")]
        public void SuggestsAllPossibleChildElements(string input, params string[] elements)
        {
            var autocomplete = new Autocomplete<Root>();
            var suggestions = autocomplete.GetSuggestions(input);
            var expected = elements.Select(e => new AutocompleteElementSuggestion { Name = e }).ToArray<AutocompleteSuggestion>();
            Assert.Equal(expected, suggestions, new PropertyComparer());
        }
    }
}
