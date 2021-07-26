using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace MarkMpn.XmlSerializerAutoComplete.Tests.Model
{
    [XmlRoot(ElementName = "MyDoc")]
    public class Root
    {
        [XmlArray("Members")]
        [XmlArrayItem(ElementName = "p")]
        [XmlArrayItem(ElementName = "c", Type = typeof(Child))]
        public Person[] Clients { get; set; }

        [XmlElement(ElementName = "Staff")]
        public Person[] Staff { get; set; }
    }
}
