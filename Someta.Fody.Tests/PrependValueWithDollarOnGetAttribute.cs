using System.Reflection;

namespace Someta.Fody.Tests
{
    public class PrependValueWithDollarOnGetAttribute : Attribute, IPropertyGetInterceptor
    {
        public object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter)
        {
            var value = getter();
            return $"${value}";
        }
    }
}