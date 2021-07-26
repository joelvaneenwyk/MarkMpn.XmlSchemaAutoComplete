using System;
using System.Collections.Generic;
using System.Text;

namespace MarkMpn.XmlSerializerAutoComplete
{
    class DfaState
    {
        public List<DfaTransition> Transitions { get; } = new List<DfaTransition>();
    }
}
