using System;
using System.Reflection;

namespace SoMeta
{
    public interface IPropertySetInterceptor : IPropertyInterceptor
    {
        void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, Action<object> setter);
    }
}