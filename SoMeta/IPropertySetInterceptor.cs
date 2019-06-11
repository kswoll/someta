using System;
using System.Reflection;

namespace SoMeta
{
    public interface IPropertySetInterceptor : IInterceptor
    {
        void SetPropertyValue(PropertyInfo propertyInfo, object instance, object newValue, Action<object> setter);
    }
}