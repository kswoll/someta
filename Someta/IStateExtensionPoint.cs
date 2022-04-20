namespace Someta
{
    /// <summary>
    /// Allows you to add fields to the class containing your extension point.  This interface works
    /// in conjunction with InjectedField.  The purpose of this interface is to act as a marker for
    /// whether or not to look for injected fields in your extension point.
    /// </summary>
    public interface IStateExtensionPoint
    {
    }

    /// <summary>
    /// When you use this generic version of IStateExtensionPoint it allows you to provide a scope for which kind
    /// of members you want to apply the state to.  For example, if you just apply IInstanceInitializer to a class,
    /// you are limited to only adding state you know to define at the class level.  This interface allows you to
    /// specify, for example, a properties scope, in which case the extension point acts as though it were applied
    /// to each property in the enclosing class.
    /// </summary>
    /// <typeparam name="T">Must be one one of the interfaces defined in ExtensionPointScopes</typeparam>
    public interface IStateExtensionPoint<T> : IStateExtensionPoint where T : ExtensionPointScopes.Scope
    {
    }
}