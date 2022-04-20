using System;

namespace Someta
{
    /// <summary>
    /// Represents a field that has been injected into the target class.  Must be used in conjunction
    /// with IStateExtensionPoint.  To use, simply declare a property of this type and it will be
    /// populated with an instance of this class that provides a proxy to allow getting and setting
    /// values on the field.
    /// </summary>
    /// <typeparam name="T">The type of the value stored in the field.</typeparam>
    public class InjectedField<T>
    {
        private readonly Func<object, T> getter;
        private readonly Action<object, T> setter;

        /// <summary>
        /// When Someta finds an extension point that implements IStateExtensionPoint with properties of
        /// type InjectedField those properties will be automatically assigned a new instance of this
        /// class.
        /// </summary>
        /// <param name="getter">A delegate provided by Someta to get the value of the field.</param>
        /// <param name="setter">A delegate provided by Someta to set the value of the field.</param>
        public InjectedField(Func<object, T> getter, Action<object, T> setter)
        {
            this.getter = getter;
            this.setter = setter;
        }

        /// <summary>
        /// Call to get the current value of the field as stored on the provided instance.
        /// </summary>
        /// <param name="instance">The instance of the object the field is attached to.  This must not be null.
        /// To represent static fields, simply declare a static field on your extension point.</param>
        /// <returns>The current value of the field.</returns>
        public T GetValue(object instance)
        {
            return getter(instance);
        }

        /// <summary>
        /// Call to set the current value of the field as stored on the provided instance.
        /// </summary>
        /// <param name="instance">The instance of the object the field is attached to.  This must not be null.
        /// To represent static fields, simply declare a static field on your extension point.</param>
        /// <param name="value">The new value that the field should be set to.</param>
        public void SetValue(object instance, T value)
        {
            setter(instance, value);
        }
    }
}