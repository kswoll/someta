using System;
using System.Reflection;

namespace SoMeta.Reflection
{
    public static class AttributeFinder
    {
        /// <summary>
        /// This differs from Attribute.GetAttribute in that it will honor interfaces.
        /// </summary>
        /// <param name="member"></param>
        /// <param name="attributeType"></param>
        /// <returns></returns>
        public static Attribute FindAttribute(MemberInfo member, Type attributeType)
        {
            return null;
        }
    }
}