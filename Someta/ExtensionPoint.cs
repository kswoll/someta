using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Someta.Helpers;

namespace Someta
{
    /// <summary>
    /// Helper class to get a static instance of your extension point that was applied to your type or member.
    /// </summary>
    public static class ExtensionPoint
    {
        /// <summary>
        /// Gets the extension point of the specified type T associated with the MemberInfo.
        /// </summary>
        /// <typeparam name="T">The type of your extension point</typeparam>
        /// <param name="member">The member that the extension point attribute was applied to</param>
        /// <returns>The instance of your extension point</returns>
        public static T GetExtensionPoint<T>(this MemberInfo member)
            where T : IExtensionPoint
        {
            return (T)member.GetExtensionPoint(typeof(T));
        }

        /// <summary>
        /// Gets the extension point of the specified type associated with the MemberInfo.
        /// </summary>
        /// <param name="member">The member that the extension point attribute was applied to</param>
        /// <param name="extensionPointType">The type of your extension point</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static IExtensionPoint GetExtensionPoint(this MemberInfo member, Type extensionPointType)
        {
            var extensionPoints = member.GetExtensionPoints(extensionPointType).ToArray();
            if (extensionPoints.Length > 1)
                throw new InvalidOperationException($"More than one matching extension point of type {extensionPointType.FullName} found on {member.DeclaringType.FullName}.{member.Name}");
            return extensionPoints.SingleOrDefault();
        }

        /// <summary>
        /// Gets all the extension points of the specified type for the provided member.
        /// </summary>
        /// <param name="member">The member that the extension point attributes were applied to</param>
        /// <param name="extensionPointType">The type of extension point to return</param>
        public static IEnumerable<IExtensionPoint> GetExtensionPoints(this MemberInfo member, Type extensionPointType)
        {
            return member.GetExtensionPoints().Where(x => extensionPointType.IsInstanceOfType(x));
        }

        /// <summary>
        /// Gets all the extension points for the provided member
        /// </summary>
        /// <param name="member">The member that the extension point attributes were applied to</param>
        public static IReadOnlyList<IExtensionPoint> GetExtensionPoints(this MemberInfo member)
        {
            return ExtensionPointRegistry.GetExtensionPoints(member);
        }
    }
}