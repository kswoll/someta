namespace Someta
{
    /// <summary>
    /// An extension point that exposes non public members to your attribute.  Used with `InjectAccessAttribute`
    /// and `InjectTargetAttribute`.
    /// </summary>
    public interface INonPublicAccess : IExtensionPoint
    {
    }
}