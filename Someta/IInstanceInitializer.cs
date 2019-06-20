using System.Reflection;

namespace Someta
{
    public interface IInstanceInitializer : IExtensionPoint
    {
        void Initialize(object instance, MemberInfo member);
    }

    public interface IInstanceInitializer<T> : IInstanceInitializer where T : ExtensionPointScopes.Scope
    {
    }
}