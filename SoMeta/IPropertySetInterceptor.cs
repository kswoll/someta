using System;
using System.Reflection;

namespace Someta
{
    public interface IPropertySetInterceptor : IPropertyInterceptor
    {
        void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, Action<object> setter);
    }
}