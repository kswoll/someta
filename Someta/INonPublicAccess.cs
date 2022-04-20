namespace Someta
{
    /// <summary>
    /// An extension point that exposes non public methods to your attribute.  Used with `InjectAccessAttribute`
    /// and `InjectTargetAttribute`.  Currently only supports methods (not properties, events, etc.)
    /// </summary>
    public interface INonPublicAccess : IExtensionPoint
    {
    }
}