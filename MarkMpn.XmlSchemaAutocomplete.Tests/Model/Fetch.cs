using System;
using System.Collections.Generic;
using System.Text;

namespace MarkMpn.XmlSchemaAutocomplete.Tests.Model
{
    [System.Xml.Serialization.XmlRootAttribute("fetch")]
    public class Fetch
    {
        [System.Xml.Serialization.XmlElementAttribute("entity", typeof(Entity))]
        [System.Xml.Serialization.XmlElementAttribute("order", typeof(Order))]
        public object[] Items;
    }
}
