using System;

namespace Someta
{
    [AttributeUsage(AttributeTargets.Property)]
    public class InjectFieldAttribute : Attribute
    {
        public bool IsStatic { get; }

        public InjectFieldAttribute(bool isStatic = false)
        {
            IsStatic = isStatic;
        }
    }
}
