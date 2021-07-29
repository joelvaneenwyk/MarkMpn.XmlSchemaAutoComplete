using System;
using System.Collections.Generic;
using System.Text;

namespace MarkMpn.XmlSchemaAutocomplete.Tests.Model
{
    public class Recursive
    {
        public string Name { get; set; }

        public Gender? Gender { get; set; }

        public Recursive Child { get; set; }
    }
}
