using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Someta.Helpers
{
    public static class ExtensionPointRegistry
    {
        private static ConcurrentDictionary<MemberInfo, List<IExtensionPoint>> storage = new ConcurrentDictionary<MemberInfo, List<IExtensionPoint>>();
        private static IReadOnlyList<IExtensionPoint> emptyList = new IExtensionPoint[0];

        public static void Register(MemberInfo member, IExtensionPoint extensionPoint)
        {
            var list = storage.GetOrAdd(member, _ => new List<IExtensionPoint>());
            lock (list)
            {
                list.Add(extensionPoint);
            }
        }

        public static IReadOnlyList<IExtensionPoint> GetExtensionPoints(MemberInfo member)
        {
            return storage.TryGetValue(member, out var list) ? list : emptyList;
        }
    }
}