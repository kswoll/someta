using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Someta.Helpers;

namespace Someta
{
    public static class ExtensionPoint
    {
        public static T GetExtensionPoint<T>(this MemberInfo member)
            where T : IExtensionPoint
        {
            return (T)member.GetExtensionPoint(typeof(T));
        }

        public static IExtensionPoint GetExtensionPoint(this MemberInfo member, Type extensionPointType)
        {
            var extensionPoints = member.GetExtensionPoints(extensionPointType).ToArray();
            if (extensionPoints.Length > 1)
                throw new InvalidOperationException($"More than one matching extension point of type {extensionPointType.FullName} found on {member.DeclaringType.FullName}.{member.Name}");
            return extensionPoints.SingleOrDefault();
        }

        public static IEnumerable<IExtensionPoint> GetExtensionPoints(this MemberInfo member, Type extensionPointType)
        {
            return member.GetExtensionPoints().Where(x => extensionPointType.IsInstanceOfType(x));
        }

        public static IReadOnlyList<IExtensionPoint> GetExtensionPoints(this MemberInfo member)
        {
            return ExtensionPointRegistry.GetExtensionPoints(member);
        }
    }
}