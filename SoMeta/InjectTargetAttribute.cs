using System;

namespace SoMeta
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