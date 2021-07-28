using System;
using System.Collections.Generic;
using System.Text;

namespace MarkMpn.XmlSchemaAutocomplete
{
    abstract class PartialXmlNode
    {
    }

    class PartialXmlProcessingInstruction : PartialXmlNode
    {

    }

    class PartialXmlElement : PartialXmlNode
    {
        public string Name { get; set; }

        public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>();

        public bool SelfClosing { get; set; }

        public string CurrentAttribute { get; set; }
    }

    class PartialXmlEndElement : PartialXmlNode
    {
        public string Name { get; set; }
    }

    class PartialXmlText : PartialXmlNode
    {
        public string Text { get; set; }
    }
}
