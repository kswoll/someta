using System.Reflection;

namespace Someta
{
    /// <summary>
    /// Apply to a member (type, property, etc.) to provide some initialization at the END of the enclosing type's
    /// constructor (or the type's constructor if the member is a type).  Pair with InjectedField to also define new
    /// fields in the enclosing type.
    /// </summary>
    public interface IInstanceInitializer : IExtensionPoint
    {
        /// <summary>
        /// Called each time a new instance of the target member (or its enclosing class) is instantiated.  This is
        /// called AFTER all the default constructor logic has completed.  Use IInstancePreinitializer to add logic
        /// BEFORE all the default constructor logic has started.
        /// </summary>
        /// <param name="instance">The new instance of the target member (or its enclosing class)</param>
        /// <param name="member">The member to which this extension point was applied</param>
        void Initialize(object instance, MemberInfo member);
    }

    /// <summary>
    /// When you use this generic version of IInstanceInitializer it allows you to provide a scope for which kind
    /// of members you want to apply the initialization to.  For example, if you just apply IInstanceInitializer to a class,
    /// you are limited to only adding state you know to define at the class level.  This interface allows you to
    /// specify, for example, a properties scope, in which case the extension point acts as though it were applied
    /// to each property in the containing class.
    /// </summary>
    /// <typeparam name="T">Must be one one of the interfaces defined in ExtensionPointScopes</typeparam>
    public interface IInstanceInitializer<T> : IInstanceInitializer where T : ExtensionPointScopes.Scope
    {
    }
}