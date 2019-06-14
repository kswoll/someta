using System;

namespace Someta
{
    [AttributeUsage(AttributeTargets.Class)]
    public class InterceptorScopeAttribute : Attribute
    {
        public InterceptorScope Scope { get; }

        public InterceptorScopeAttribute(InterceptorScope scope)
        {
            Scope = scope;
        }
    }
}