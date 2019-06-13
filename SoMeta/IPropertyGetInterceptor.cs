using System;
using System.Reflection;

namespace Someta
{
    public interface IPropertyGetInterceptor : IPropertyInterceptor
    {
        object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter);
    }
}