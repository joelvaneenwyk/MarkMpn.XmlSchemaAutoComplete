using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;

namespace MarkMpn.XmlSerializerAutoComplete
{
    class XmlTypeConverter
    {
        public static bool TryChangeType(Type type, string text, out object value)
        {
            if (type.IsEnum)
            {
                // Check if there are custom XmlEnumAttributes defined
                var xmlValue = type
                    .GetFields()
                    .Select(field => new { Field = field, XmlEnum = field.GetCustomAttribute<XmlEnumAttribute>() })
                    .SingleOrDefault(field => field.XmlEnum?.Name == text);

                if (xmlValue != null)
                {
                    value = xmlValue.Field.GetValue(null);
                    return true;
                }

                return Enum.TryParse(type, text, out value);
            }

            try
            {
                value = Convert.ChangeType(text, type);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }
    }
}
