using System;
using System.Reflection;
using SoMeta;

namespace SoMeta.Fody.Tests
{
    public class DoubleStringOnSetAttribute : PropertyInterceptorAttribute
    {
        public override void SetPropertyValue(PropertyInfo propertyInfo, object instance, object newValue, Action<object> setter)
        {
            var s = (string)newValue;
            s = s + s;
            setter(s);
        }
    }
}