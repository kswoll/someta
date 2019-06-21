using System;
using System.Collections.Generic;
using System.Reflection;

namespace Someta
{
    public class ExtensionPoint
    {
        public IExtensionPoint GetExtensionPoint(MemberInfo member, Type extensionPointType)
        {
            return null;
        }

        public IReadOnlyList<IExtensionPoint> GetExtensionPoints(MemberInfo member, Type extensionPointType)
        {
            return null;
        }
    }
}