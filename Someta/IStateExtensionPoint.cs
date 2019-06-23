namespace Someta
{
    /// <summary>
    /// Use in conjunction with `InjectedField` so your extension can add custom
    /// fields to the containing class.
    /// </summary>
    public interface IStateExtensionPoint
    {
    }

    /// <summary>
    /// <inheritdoc cref="Someta.IStateExtensionPoint" />
    /// </summary>
    public interface IStateExtensionPoint<T> : IStateExtensionPoint where T : ExtensionPointScopes.Scope
    {
    }
}