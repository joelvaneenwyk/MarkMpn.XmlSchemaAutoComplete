using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MarkMpn.XmlSerializerAutoComplete
{
    class PropertyOrField
    {
        private readonly PropertyInfo _prop;
        private readonly FieldInfo _field;

        public PropertyOrField(PropertyInfo prop)
        {
            _prop = prop;
        }

        public PropertyOrField(FieldInfo field)
        {
            _field = field;
        }

        public Type Type => _prop?.PropertyType ?? _field.FieldType;

        public void SetValue(object target, object value)
        {
            if (_prop != null)
                _prop.SetValue(target, value);
            else
                _field.SetValue(target, value);
        }

        public object GetValue(object target)
        {
            if (_prop != null)
                return _prop.GetValue(target);
            else
                return _field.GetValue(target);
        }
    }
}
