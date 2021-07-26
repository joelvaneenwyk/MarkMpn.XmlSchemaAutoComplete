using System;
using System.Collections.Generic;
using System.Text;

namespace MarkMpn.XmlSerializerAutoComplete
{
    class ParserState
    {
        public PartialXmlNode Node { get; set; }

        public Stack<object> DeserializedStack { get; } = new Stack<object>();
    }
}
