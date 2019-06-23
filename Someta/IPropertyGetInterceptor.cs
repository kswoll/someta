using System;
using System.Reflection;

namespace Someta
{
    public interface IPropertyGetInterceptor : IExtensionPoint
    {
        /// <summary>
        /// Called when the (a) property is accessed.  You can return your own value.  To access
        /// what the property would have returned if not intercepted, you can call `proceed`.
        /// </summary>
        /// <param name="propertyInfo">The PropertyInfo of the property being accessed.</param>
        /// <param name="instance">The instance of the object where the property resides.  If a
        /// static property, this is null.</param>
        /// <param name="proceed">A callback you can invoke to obtain the unintercepted value
        /// of the property.</param>
        /// <returns>Return to the caller a value for the property.</returns>
        object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> proceed);
    }
}