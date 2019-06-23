using System;

namespace Someta
{
    /// <summary>
    /// Use in your extension (that implements IStateExtensionPoint) to add custom
    /// fields to the targeted class so you can track your own state.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class InjectedField<T>
    {
        private readonly Func<object, T> getter;
        private readonly Action<object, T> setter;

        public InjectedField(Func<object, T> getter, Action<object, T> setter)
        {
            this.getter = getter;
            this.setter = setter;
        }

        /// <summary>
        /// Given an instance (your target), gets the current value of your injected field.
        /// </summary>
        /// <param name="instance">The instance of the object with the field</param>
        /// <returns>The current value of the injected field</returns>
        public T GetValue(object instance)
        {
            return getter(instance);
        }

        /// <summary>
        /// Given an instance (your target), sets the current value of your injected field.
        /// </summary>
        /// <param name="instance">The instance of the object with the field</param>
        /// <param name="value">The new value for the injected field</param>
        public void SetValue(object instance, T value)
        {
            setter(instance, value);
        }
    }
}