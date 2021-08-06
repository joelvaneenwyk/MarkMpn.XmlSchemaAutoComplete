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
            var suggestions = autocomplete.GetSuggestions(input, out _);
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
            var suggestions = autocomplete.GetSuggestions(input, out _).OfType<AutocompleteElementSuggestion>().ToArray();
            var expected = elements.Select(e => new AutocompleteElementSuggestion { Name = e, HasAttributes = e == "Staff" }).ToArray();
            Assert.Equal(expected, suggestions, new PropertyComparer());
        }

        [Theory]
        [InlineData("<MyDoc><Members><", "p", "c")]
        [InlineData("<MyDoc><S", "Staff")]
        public void SuggestsArrayMembers(string input, params string[] elements)
        {
            var autocomplete = new Autocomplete<Root>();
            var suggestions = autocomplete.GetSuggestions(input, out _).OfType<AutocompleteElementSuggestion>().ToArray();
            var expected = elements.Select(e => new AutocompleteElementSuggestion { Name = e, HasAttributes = true }).ToArray();
            Assert.Equal(expected, suggestions, new PropertyComparer());
        }

        [Theory]
        [InlineData("<MyDoc ", "xmlns:xsi", "xsi:nil")]
        [InlineData("<MyDoc><Members><p ", "xsi:nil", "xsi:type", "gender", "manager", "surname")]
        [InlineData("<MyDoc><Members><c ", "xsi:nil", "gender", "manager", "surname")]
        [InlineData("<MyDoc><Staff ", "xsi:type", "gender", "manager", "surname")]
        [InlineData("<MyDoc><Staff s", "surname")]
        [InlineData("<MyDoc><Staff gender='Male'")]
        [InlineData("<MyDoc><Staff gender='Male' ", "xsi:type", "manager", "surname")]
        [InlineData("<MyDoc><Staff f")]
        public void SuggestsAttributes(string input, params string[] attributes)
        {
            var autocomplete = new Autocomplete<Root>();
            autocomplete.UsesXsi = true;
            var suggestions = autocomplete.GetSuggestions(input, out _);

            // Some descriptions are auto-generated. We're not testing them, so strip them for readability
            foreach (var suggestion in suggestions)
            {
                suggestion.Title = null;
                suggestion.Description = null;
            }

            var expected = attributes.Select(e => new AutocompleteAttributeSuggestion { Name = e }).ToArray<AutocompleteSuggestion>();
            Assert.Equal(expected, suggestions, new PropertyComparer());
        }

        [Theory]
        [InlineData("<MyDoc xmlns:xsi=", "http://www.w3.org/2001/XMLSchema-instance")]
        [InlineData("<MyDoc xmlns:xsi=\"", "http://www.w3.org/2001/XMLSchema-instance")]
        [InlineData("<MyDoc><Staff gender=", "Male", "Female")]
        [InlineData("<MyDoc><Staff gender=\"M", "Male")]
        [InlineData("<MyDoc><Staff manager=\"", "false", "true")]
        public void SuggestsAttributeValues(string input, params string[] values)
        {
            var autocomplete = new Autocomplete<Root>();
            var suggestions = autocomplete.GetSuggestions(input, out _);
            var expected = values.Select(e => new AutocompleteAttributeValueSuggestion { Value = e, IncludeQuotes = input.EndsWith("="), QuoteChar = '"' }).ToArray<AutocompleteSuggestion>();
            Assert.Equal(expected, suggestions, new PropertyComparer());
        }

        [Theory]
        [InlineData("<Recursive><Gender>", "Male", "Female")]
        [InlineData("<Recursive><Gender>M", "Male")]
        public void SuggestsElementEnumValues(string input, params string[] values)
        {
            var autocomplete = new Autocomplete<Recursive>();
            var suggestions = autocomplete.GetSuggestions(input, out _);
            var expected = values.Select(e => new AutocompleteValueSuggestion { Value = e }).ToArray<AutocompleteSuggestion>();
            Assert.Equal(expected, suggestions, new PropertyComparer());
        }

        [Theory]
        [InlineData("<", "Recursive")]
        [InlineData("<Recursive><", "Name", "Gender")]
        [InlineData("<Recursive><Gender xsi:nil='true' /><", "Child")]
        [InlineData("<Recursive><Gender xsi:nil='true' /><Child><", "Name", "Gender")]
        [InlineData("<Recursive><Gender xsi:nil='true' /><Child><Gender xsi:nil='true' /><", "Child")]
        [InlineData("<Recursive><Gender xsi:nil='true' /><Child><Gender xsi:nil='true' /><Child><Gender xsi:nil='true' /><", "Child")]
        public void SuggestsRecursiveElements(string input, params string[] elements)
        {
            var autocomplete = new Autocomplete<Recursive>();
            var suggestions = autocomplete.GetSuggestions(input, out _).OfType<AutocompleteElementSuggestion>().ToArray();
            var expected = elements.Select(e => new AutocompleteElementSuggestion { Name = e }).ToArray();
            Assert.Equal(expected, suggestions, new PropertyComparer());
        }

        [Theory]
        [InlineData("<Recursive><Name ", false)]
        [InlineData("<Recursive><Gender ", true)]
        public void SuggestsNillableAttribute(string input, bool nillableExpected)
        {
            var autocomplete = new Autocomplete<Recursive>();
            var suggestions = autocomplete.GetSuggestions(input, out _);

            if (nillableExpected)
                Assert.Contains(suggestions.OfType<AutocompleteAttributeSuggestion>(), a => a.Name == "xsi:nil");
            else
                Assert.DoesNotContain(suggestions.OfType<AutocompleteAttributeSuggestion>(), a => a.Name == "xsi:nil");
        }

        [Theory]
        [InlineData("<Recursive><Gender xsi:nil=")]
        public void SuggestsBooleanValues(string input)
        {
            var autocomplete = new Autocomplete<Recursive>();
            var suggestions = autocomplete.GetSuggestions(input, out _);

            var expected = new AutocompleteSuggestion[]
            {
                new AutocompleteAttributeValueSuggestion { Value = "true", IncludeQuotes = input.EndsWith("="), QuoteChar = '"' },
                new AutocompleteAttributeValueSuggestion { Value = "false", IncludeQuotes = input.EndsWith("="), QuoteChar = '"' },
            };

            Assert.Equal(expected, suggestions, new PropertyComparer());
        }

        [Theory]
        [InlineData("<MyDoc><Staff><forename>", "MyDoc/Staff/forename")]
        [InlineData("<MyDoc><Staff surname=", "MyDoc/Staff/@surname")]
        public void CallbacksToCompleteValues(string input, string path)
        {
            var autocomplete = new Autocomplete<Root>();
            autocomplete.AutocompleteValue += (sender, args) =>
            {
                args.Suggestions.Add(new AutocompleteValueSuggestion { Value = String.Join("/", args.SchemaElements.Reverse()) });
            };
            autocomplete.AutocompleteAttributeValue += (sender, args) =>
            {
                args.Suggestions.Add(new AutocompleteAttributeValueSuggestion { Value = String.Join("/", args.SchemaElements.Reverse()) + "/@" + args.Attribute.Name });
            };
            var suggestions = autocomplete.GetSuggestions(input, out _);
            var expected = new AutocompleteSuggestion[]
            {
                input.EndsWith("=") ? (AutocompleteSuggestion) new AutocompleteAttributeValueSuggestion { Value = path, IncludeQuotes = true, QuoteChar = '"' } : new AutocompleteValueSuggestion { Value = path },
            };
            Assert.Equal(expected, suggestions, new PropertyComparer());
        }

        [Theory]
        [InlineData("<f", "fetch")]
        [InlineData("<fetch><", "entity", "order")]
        [InlineData("<fetch><entity /><", "entity", "order")]
        public void SequenceOfChoice(string input, params string[] elements)
        {
            var autocomplete = new Autocomplete<Fetch>();
            var suggestions = autocomplete.GetSuggestions(input, out _).OfType<AutocompleteElementSuggestion>();
            var expected = elements.Select(e => new AutocompleteElementSuggestion { Name = e, SelfClosing = e != "fetch" }).ToArray();
            Assert.Equal(expected, suggestions, new PropertyComparer());
        }

        [Theory]
        [InlineData("<fetch><", "fetch")]
        [InlineData("<fetch></", "fetch")]
        public void EndElement(string input, string element)
        {
            var autocomplete = new Autocomplete<Fetch>();
            var suggestions = autocomplete.GetSuggestions(input, out _).OfType<AutocompleteEndElementSuggestion>().ToArray();
            var expected = new []
            {
                new AutocompleteEndElementSuggestion { Name = element, IncludeSlash = !input.EndsWith("/") }
            };
            Assert.Equal(expected, suggestions, new PropertyComparer());
        }
    }
}
