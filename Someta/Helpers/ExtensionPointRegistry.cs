using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Someta.Helpers
{
    /// <summary>
    /// Maps instances of `MemberInfo` and the instance of your extension attribute.
    /// </summary>
    public static class ExtensionPointRegistry
    {
        private static readonly ConcurrentDictionary<ICustomAttributeProvider, List<IExtensionPoint>> storage = new ConcurrentDictionary<ICustomAttributeProvider, List<IExtensionPoint>>();
        private static readonly IReadOnlyList<IExtensionPoint> emptyList = new IExtensionPoint[0];

        public static void Register(ICustomAttributeProvider member, IExtensionPoint extensionPoint)
        {
            var list = storage.GetOrAdd(member, _ => new List<IExtensionPoint>());
            lock (list)
            {
                list.Add(extensionPoint);
            }
        }

        public static IReadOnlyList<IExtensionPoint> GetExtensionPoints(ICustomAttributeProvider member)
        {
            return storage.TryGetValue(member, out var list) ? list : emptyList;
        }
    }
}