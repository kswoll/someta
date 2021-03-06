﻿using System;

namespace Someta
{
    /// <summary>
    /// Injects a lambda into the associated property that can be used to invoke
    /// a private or protected member from within an implementation of `INonPublicAccess`.
    /// The delegate type of the property should match the signature of the target method,
    /// with one additional parameter at the beginning for the instance of the target.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class InjectAccessAttribute : Attribute
    {
        public string Key { get; }

        public InjectAccessAttribute(string key)
        {
            Key = key;
        }
    }
}