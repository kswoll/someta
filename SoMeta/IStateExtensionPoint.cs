namespace Someta
{
    public interface IStateExtensionPoint
    {
    }

    public interface IStateExtensionPoint<T> : IStateExtensionPoint where T : ExtensionPointScopes.Scope
    {
    }
}