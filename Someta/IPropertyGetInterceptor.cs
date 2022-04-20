using System;
using System.Reflection;

namespace Someta
{
    /// <summary>
    /// Have your extension point implement this interface to be able to intercept property get invocations.
    /// You can call the original implementation of the get method via the provided getter delegate.
    /// </summary>
    public interface IPropertyGetInterceptor : IExtensionPoint
    {
        /// <summary>
        /// Called each time a the getter for the property this interceptor is attached to is accessed.
        /// </summary>
        /// <param name="propertyInfo">The PropertyInfo that this extension was applied to.</param>
        /// <param name="instance">For static properties, this is null.  For instance properties, this is the instance containing the property.</param>
        /// <param name="getter">A delegate you can invoke to call the original getter.</param>
        /// <returns>The value of the property that should be returned to the caller.</returns>
        object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter);
    }
}