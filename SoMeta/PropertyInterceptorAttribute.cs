using System;
using System.Reflection;

namespace SoMeta
{
    [AttributeUsage(AttributeTargets.Property)]
    public abstract class PropertyInterceptorAttribute : InterceptorAttribute
    {
        public virtual object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter)
        {
            return getter();
        }

        public virtual void SetPropertyValue(PropertyInfo propertyInfo, object instance, object newValue, Action<object> setter)
        {
            setter(newValue);
        }
    }
}