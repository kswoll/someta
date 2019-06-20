using System;
using System.Reflection;

namespace Someta
{
    public interface IPropertyGetInterceptor : IExtensionPoint
    {
        object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter);
    }
}