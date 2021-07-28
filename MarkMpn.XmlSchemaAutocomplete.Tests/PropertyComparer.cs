using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MarkMpn.XmlSchemaAutocomplete.Tests
{
    class PropertyComparer : IEqualityComparer<object>
    {
        public new bool Equals([AllowNull] object x, [AllowNull] object y)
        {
            if (x == y)
                return true;

            if (x == null || y == null)
                return false;

            var type = x.GetType();

            if (y.GetType() != type)
                return false;

            foreach (var prop in type.GetProperties())
            {
                var xVal = prop.GetValue(x);
                var yVal = prop.GetValue(y);

                if (xVal == yVal)
                    continue;

                if (xVal == null || yVal == null || !xVal.Equals(yVal))
                    return false;
            }

            foreach (var field in type.GetFields())
            {
                var xVal = field.GetValue(x);
                var yVal = field.GetValue(y);

                if (xVal == yVal)
                    continue;

                if (xVal == null || yVal == null || !xVal.Equals(yVal))
                    return false;
            }

            return true;
        }

        public int GetHashCode([DisallowNull] object obj)
        {
            return 0;
        }
    }
}
