using System;

namespace SoMeta
{
    /// <summary>
    /// Injects a lambda into the associated property that can be used to invoke
    /// a private or protected member from within an implementation of `IClassEnhancer`.
    /// The delegate type of the property should match the signature of the target method,
    /// with one additional parameter at the beginning for the instance of the target.
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