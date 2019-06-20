using System;
using System.Reflection;
using Someta;

namespace Someta.Fody.Tests
{
    public class DoubleStringOnSetAttribute : PropertyInterceptorAttribute
    {
        public override void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, Action<object> setter)
        {
            var s = (string)newValue;
            s = s + s;
            setter(s);
        }
    }
}