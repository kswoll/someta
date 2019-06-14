using System;

namespace Someta.Fody
{
    [Flags]
    public enum InterceptorScope
    {
        None =      0b00000000,
        Property =  0b00000001,
        Method =    0b00000010,
        Event =     0b00000100,
        Class =     0b00001000,
        Module =    0b00010000,
        Assembly =  0b00100000
    }
}