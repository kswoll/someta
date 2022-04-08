using System;
using System.Reflection;

namespace Someta.Fody.Tests
{
    public class PrependValueWithDollarOnGetAttribute : PropertyInterceptorAttribute
    {
        public override object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter)
        {
            var value = getter();
            return $"${value}";
        }
    }
}