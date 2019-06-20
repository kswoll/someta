using System;

namespace Someta
{
    /// <summary>
    /// Used in your normal classes to provide a key on which to correlate with an instance of
    /// a `INonPublicAccess` extension in conjunction with `InjectAccessAttribute`.
    /// </summary>
    public class InjectTargetAttribute : Attribute
    {
        public string Key { get; }

        public InjectTargetAttribute(string key)
        {
            Key = key;
        }
    }
}