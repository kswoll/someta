using System;

namespace SoMeta.Helpers
{
    [AttributeUsage(AttributeTargets.Property)]
    public abstract class SoMetaPropertyAttribute : Attribute
    {
        protected virtual object GetPropertyValue(object instance, object currentValue)
        {
            return currentValue;
        }

        protected virtual void SetPropertyValue(object instance, object newValue, Action<object> proceed)
        {
            proceed(newValue);
        }
    }
}