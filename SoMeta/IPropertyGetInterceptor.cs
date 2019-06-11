using System;
using System.Reflection;

namespace SoMeta
{
    public interface IPropertyGetInterceptor : IInterceptor
    {
        object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter);
    }
}