using System;
using System.Collections.Generic;
using System.Text;

namespace MarkMpn.XmlSchemaAutocomplete
{
    public class PartialXmlReader
    {
        private readonly string _text;
        private int _offset;
        private ReaderState _state;

        public PartialXmlReader(string text)
        {
            _text = text;
        }

        public ReaderState State => _state;

        public bool TryRead(out PartialXmlNode node)
        {
            if (_offset >= _text.Length)
            {
                node = null;
                return false;
            }

            if (_text[_offset] == '<')
            {
                _offset++;

                // Could be start element, end element or processing instruction. Check the next character

                // If we've only got the opening < character, assume this is a start element
                if (_offset == _text.Length)
                {
                    _state = ReaderState.InStartElement;
                    node = new PartialXmlElement { Name = String.Empty };
                    return true;
                }

                if (_text[_offset] == '?')
                {
                    _offset++;
                    _state = ReaderState.InProcessingInstruction;

                    // Processing instruction - continue to the ending ?>
                    for (; _offset < _text.Length; _offset++)
                    {
                        if (_text[_offset - 1] == '?' && _text[_offset] == '>')
                        {
                            _state = ReaderState.InText;
                            _offset++;
                            break;
                        }
                    }

                    node = new PartialXmlProcessingInstruction();
                    return true;
                }

                if (_text[_offset] == '/')
                {
                    _offset++;
                    _state = ReaderState.InEndElement;

                    // End element - continue to the ending >
                    var nameStart = _offset;
                    var nameEnd = -1;

                    for (; _offset < _text.Length; _offset++)
                    {
                        if (_text[_offset] == ' ')
                        {
                            if (nameEnd == -1)
                                nameEnd = _offset;
                        }

                        if (_text[_offset] == '>')
                        {
                            if (nameEnd == -1)
                                nameEnd = _offset;

                            _state = ReaderState.InText;
                            _offset++;
                            break;
                        }
                    }

                    if (nameEnd == -1)
                        nameEnd = _offset;

                    node = new PartialXmlEndElement { Name = _text.Substring(nameStart, nameEnd - nameStart) };
                    return true;
                }

                {
                    _state = ReaderState.InStartElement;

                    // Start element - continue to ending >, handling attributes along the way
                    // Go to the end of the name first
                    var nameStart = _offset;
                    var nameEnd = -1;
                    var selfClosing = false;

                    for (; _offset < _text.Length; _offset++)
                    {
                        if (_text[_offset] == ' ')
                        {
                            if (nameEnd == -1)
                                nameEnd = _offset;

                            _state = ReaderState.AwaitingAttribute;
                            break;
                        }

                        if (_text[_offset] == '>')
                        {
                            if (_text[_offset - 1] == '/')
                                selfClosing = true;

                            if (nameEnd == -1)
                            {
                                if (selfClosing)
                                    nameEnd = _offset - 1;
                                else
                                    nameEnd = _offset;
                            }

                            _state = ReaderState.InText;
                            _offset++;
                            break;
                        }
                    }

                    if (nameEnd == -1)
                        nameEnd = _offset;

                    node = new PartialXmlElement { Name = _text.Substring(nameStart, nameEnd - nameStart), SelfClosing = selfClosing };

                    // If we're still in the element, continue looking for attributes
                    if (_state == ReaderState.InText)
                        return true;

                    var attributeName = String.Empty;
                    var start = -1;
                    var quote = '\0';

                    for (; _offset < _text.Length; _offset++)
                    {
                        if (_state == ReaderState.AwaitingAttribute && _text[_offset] == '>')
                        {
                            if (_text[_offset - 1] == '/')
                                ((PartialXmlElement)node).SelfClosing = true;

                            _state = ReaderState.InText;
                            _offset++;
                            break;
                        }

                        if (_state == ReaderState.AwaitingAttribute && _text[_offset] == ' ')
                            continue;

                        if (_state == ReaderState.AwaitingAttribute && _text[_offset] == '/')
                            continue;

                        if (_state == ReaderState.AwaitingAttribute)
                        {
                            start = _offset;
                            _state = ReaderState.InAttributeName;
                            continue;
                        }

                        if (_state == ReaderState.InAttributeName && _text[_offset] == '>')
                        {
                            // Malformed, but break out of this element anyway
                            _state = ReaderState.InText;
                            _offset++;
                            break;
                        }

                        if (_state == ReaderState.InAttributeName && _text[_offset] == ' ')
                        {
                            // Malformed, but ignore this attribute and carry on
                            _state = ReaderState.InStartElement;
                            continue;
                        }

                        if (_state == ReaderState.InAttributeName && _text[_offset] == '=')
                        {
                            attributeName = _text.Substring(start, _offset - start);
                            _state = ReaderState.InAttributeEquals;
                            continue;
                        }

                        if (_state == ReaderState.InAttributeEquals && (_text[_offset] == '"' || _text[_offset] == '\''))
                        {
                            start = _offset + 1;
                            quote = _text[_offset];
                            _state = ReaderState.InAttributeValue;
                            continue;
                        }

                        if (_state == ReaderState.InAttributeValue && _text[_offset] == quote)
                        {
                            var attributeValue = _text.Substring(start, _offset - start);
                            ((PartialXmlElement)node).Attributes[attributeName] = attributeValue;
                            _state = ReaderState.AwaitingAttribute;
                            continue;
                        }
                    }

                    // If we're currently in an attribute value, show the value so far
                    if (_state == ReaderState.InAttributeValue)
                    {
                        var attributeValue = _text.Substring(start);
                        ((PartialXmlElement)node).Attributes[attributeName] = attributeValue;
                        ((PartialXmlElement)node).CurrentAttribute = attributeName;
                    }

                    if (_state == ReaderState.InAttributeEquals)
                    {
                        ((PartialXmlElement)node).Attributes[attributeName] = null;
                        ((PartialXmlElement)node).CurrentAttribute = attributeName;
                    }

                    if (_state == ReaderState.InAttributeName)
                    {
                        ((PartialXmlElement)node).CurrentAttribute = _text.Substring(start, _offset - start);
                    }

                    return true;
                }
            }

            // Anything else is text - take everything up to the next <
            {
                var start = _offset;

                for (; _offset < _text.Length; _offset++)
                {
                    if (_text[_offset] == '<')
                        break;
                }

                node = new PartialXmlText { Text = _text.Substring(start, _offset - start) };
                return true;
            }
        }
    }

    public enum ReaderState
    {
        BOF,
        InProcessingInstruction,
        InStartElement,
        AwaitingAttribute,
        InEndElement,
        InAttributeName,
        InAttributeEquals,
        InAttributeValue,
        InText
    }
}
