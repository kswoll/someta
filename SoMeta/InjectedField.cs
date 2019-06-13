using System;

namespace SoMeta
{
    public class InjectedField<T>
    {
        private readonly Func<object, T> getter;
        private readonly Action<object, T> setter;

        public InjectedField(Func<object, T> getter, Action<object, T> setter)
        {
            this.getter = getter;
            this.setter = setter;
        }

        public T GetValue(object instance)
        {
            return getter(instance);
        }

        public void SetValue(object instance, T value)
        {
            setter(instance, value);
        }
    }
}