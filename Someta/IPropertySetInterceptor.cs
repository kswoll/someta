using System;
using System.Reflection;

namespace Someta
{
    /// <summary>
    /// Have your extension point implement this interface to be able to intercept property set invocations.
    /// You can call the original implementation of the set method via the provided setter delegate.
    /// </summary>
    public interface IPropertySetInterceptor : IExtensionPoint
    {
        /// <summary>
        /// Called each time a property this interceptor is attached to is set.
        /// </summary>
        /// <param name="propertyInfo">The PropertyInfo that this extension was applied to.</param>
        /// <param name="instance">For static properties, this is null.  For instance properties, this is the instance containing the property.</param>
        /// <param name="oldValue">The current value of the property. (obtained by calling the property's getter)</param>
        /// <param name="newValue">The new value that was provided when setting the property.</param>
        /// <param name="setter">A delegate you can invoke to call the original setter.  The parameter should be the new value of the property.</param>
        void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, Action<object> setter);
    }
}