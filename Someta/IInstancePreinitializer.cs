using System.Reflection;

namespace Someta
{
    public interface IInstancePreinitializer : IExtensionPoint
    {
        void Preinitialize(object instance, MemberInfo member);
    }

    public interface IInstancePreinitializer<T> : IInstancePreinitializer where T : ExtensionPointScopes.Scope
    {
    }
}
