using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;

namespace MarkMpn.XmlSerializerAutoComplete
{
    class DfaBuilder<T>
    {
        public DfaState Build()
        {
            // Create the root node
            var root = new DfaState();

            // Allow skipping processing instructions
            root.Transitions.Add(new IgnoreProcessingInstructionTransition { Next = root });

            // Allow skipping text
            root.Transitions.Add(new IgnoreTextTransition { Next = root });

            // Handle the root element to create the root object
            var xmlRoot = typeof(T).GetCustomAttribute<XmlRootAttribute>();
            var createRoot = new CreateInstanceTransition
            {
                ElementName = xmlRoot?.ElementName ?? typeof(T).Name,
                Property = null,
                Type = typeof(T),
                Next = new DfaState()
            };

            root.Transitions.Add(createRoot);

            return root;
        }
    }
}
