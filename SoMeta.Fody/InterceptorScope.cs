using System;

namespace Someta.Fody
{
    public enum InterceptorScope
    {
        None,
        Property,
        Method,
        Event,
        Class,
        Module,
        Assembly
    }
}