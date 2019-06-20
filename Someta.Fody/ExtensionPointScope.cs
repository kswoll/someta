using System;

namespace Someta.Fody
{
    public enum ExtensionPointScope
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