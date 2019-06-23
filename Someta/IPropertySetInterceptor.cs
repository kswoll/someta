using System;
using System.Reflection;

namespace Someta
{
    public interface IPropertySetInterceptor : IExtensionPoint
    {
        /// <summary>
        /// Called when the property is set. You are given the current value and the new value.
        /// To actually set the property, pass a value to the `proceed` function -- either the
        /// provided `newValue` or any other value of your choosing.
        /// </summary>
        /// <param name="propertyInfo">The PropertyInfo of the property being accessed.</param>
        /// <param name="instance">The instance of the object where the property resides.  If a
        /// static property, this is null.</param>
        /// <param name="oldValue">The current value of the property</param>
        /// <param name="newValue">The new value being assigned by the caller</param>
        /// <param name="proceed">A function you can call to actually set the value on the
        /// property.</param>
        void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, 
            Action<object> proceed);
    }
}