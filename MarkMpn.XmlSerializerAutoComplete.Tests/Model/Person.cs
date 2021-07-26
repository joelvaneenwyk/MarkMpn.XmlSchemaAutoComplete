using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace MarkMpn.XmlSerializerAutoComplete.Tests.Model
{
    [XmlInclude(typeof(Child))]
    public class Person
    {
        [XmlElement("forename")]
        public string FirstName { get; set; }

        [XmlAttribute("surname")]
        public string LastName { get; set; }

        public int Age { get; set; }
    }
}
