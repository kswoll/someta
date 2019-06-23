using System.Reflection;

namespace Someta
{
    /// <summary>
    /// Allows your extension to provide initialization logic at the end of the target's
    /// constructor.
    /// </summary>
    public interface IInstanceInitializer : IExtensionPoint
    {
        void Initialize(object instance, MemberInfo member);
    }

    /// <summary>
    /// <inheritdoc cref="Someta.IInstanceInitializer" />
    /// </summary>
    public interface IInstanceInitializer<T> : IInstanceInitializer where T : ExtensionPointScopes.Scope
    {
    }
}