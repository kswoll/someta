using System;

namespace Someta
{
    public class InjectTargetAttribute : Attribute
    {
        public string Key { get; }

        public InjectTargetAttribute(string key)
        {
            Key = key;
        }
    }
}