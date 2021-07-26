using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;

namespace MarkMpn.XmlSerializerAutoComplete
{
    class AutoComplete<T>
    {
        public AutoComplete(string text)
        {
            Parse(text);
        }

        public T ParsedObject { get; private set; }

        public AutocompleteSuggestion[] Suggestions { get; private set; }

        private void Parse(string text)
        {
            var dfa = new DfaBuilder<T>().Build();
            var parserState = new ParserState();
            var parser = new PartialXmlReader(text);
            var lastTransition = default(DfaTransition);
            var accepted = false;

            while (parser.TryRead(out var node))
            {
                parserState.Node = node;
                accepted = false;

                foreach (var transition in dfa.Transitions)
                {
                    if (transition.Accept(parserState))
                    {
                        accepted = true;
                        dfa = transition.Next;
                        lastTransition = transition;
                        break;
                    }
                }

                if (!accepted)
                    break;
            }

            ParsedObject = (T) parserState.DeserializedStack.LastOrDefault();

            // Find what available transitions we have left to make suggestions from
            var suggestions = new List<AutocompleteSuggestion>();

            if (parser.State == ReaderState.InStartElement)
            {
                var element = (PartialXmlElement)parserState.Node;

                // Suggest available elements
                var elementSuggestions = dfa.Transitions
                    .OfType<ElementBasedTransition>()
                    .Select(t => t.ElementName)
                    .Where(name => name.StartsWith(element.Name))
                    .Distinct()
                    .OrderBy(name => name);

                suggestions.AddRange(elementSuggestions.Select(name => new AutocompleteElementSuggestion { Name = name }));
            }

            if (parser.State == ReaderState.AwaitingAttribute || parser.State == ReaderState.InAttributeName)
            {
                var element = (PartialXmlElement)parserState.Node;

                if (accepted && lastTransition is CreateInstanceTransition create)
                {
                    var attributeSuggestions = create
                        .AttributeProperties
                        .Keys
                        .Except(element.Attributes.Keys)
                        .Where(name => element.CurrentAttribute == null || name.StartsWith(element.CurrentAttribute))
                        .OrderBy(name => name);

                    suggestions.AddRange(attributeSuggestions.Select(name => new AutocompleteAttributeSuggestion { Name = name }));
                }
            }

            Suggestions = suggestions.ToArray();
        }
    }
}
