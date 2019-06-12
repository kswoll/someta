using System;

namespace SoMeta
{
    /// <summary>
    /// Injects a lambda into the associated property that can be used to invoke
    /// a private or protected member from within an implementation of `IClassEnhancer`.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class InjectAccessAttribute : Attribute
    {
        public string PropertyName { get; }

        public InjectAccessAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }
    }
}