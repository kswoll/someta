using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Someta.Helpers;

namespace Someta
{
    public static class ExtensionPoint
    {
        public static T GetExtensionPoint<T>(this ICustomAttributeProvider attributeProvider)
            where T : IExtensionPoint
        {
            return (T)attributeProvider.GetExtensionPoint(typeof(T));
        }

        public static IExtensionPoint GetExtensionPoint(this ICustomAttributeProvider attributeProvider, Type extensionPointType)
        {
            var extensionPoints = attributeProvider.GetExtensionPoints(extensionPointType).ToArray();
            string name;
            switch (attributeProvider)
            {
                case Assembly assembly:
                    name = assembly.FullName;
                    break;
                case MemberInfo member:
                    name = $"{member.DeclaringType.FullName}.{member.Name}";
                    break;
                default:
                    throw new InvalidOperationException($"Unknown type: {attributeProvider.GetType().FullName}");
            }
            if (extensionPoints.Length > 1)
                throw new InvalidOperationException($"More than one matching extension point of type {extensionPointType.FullName} found on {name}");
            return extensionPoints.SingleOrDefault();
        }

        public static IEnumerable<IExtensionPoint> GetExtensionPoints(this ICustomAttributeProvider attributeProvider, Type extensionPointType)
        {
            return attributeProvider.GetExtensionPoints().Where(x => extensionPointType.IsInstanceOfType(x));
        }

        public static IReadOnlyList<IExtensionPoint> GetExtensionPoints(this ICustomAttributeProvider attributeProvider)
        {
            return ExtensionPointRegistry.GetExtensionPoints(attributeProvider);
        }
    }
}