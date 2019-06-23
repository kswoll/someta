using System;
using System.Reflection;

namespace Someta
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = true)]
    public abstract class PropertyInterceptorAttribute : ExtensionPointAttribute, IPropertyGetInterceptor, IPropertySetInterceptor
    {
        public virtual object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> proceed)
        {
            return proceed();
        }

        public virtual void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, Action<object> proceed)
        {
            proceed(newValue);
        }
    }
}