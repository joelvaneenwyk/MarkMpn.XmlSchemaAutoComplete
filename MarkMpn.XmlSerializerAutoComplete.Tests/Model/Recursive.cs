using System;
using System.Collections.Generic;
using System.Text;

namespace MarkMpn.XmlSerializerAutoComplete.Tests.Model
{
    public class Recursive
    {
        public string Name { get; set; }

        public Recursive Child { get; set; }
    }
}
