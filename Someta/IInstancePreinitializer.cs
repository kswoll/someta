using System.Reflection;

namespace Someta
{
    /// <summary>
    /// Apply to a member (type, property, etc.) to provide some initialization at the START of the enclosing type's
    /// constructor (or the type's constructor if the member is a type).  Pair with InjectedField to also define new
    /// fields in the enclosing type.  Note that while this method will be invoked at the start of the constructor,
    /// if you have other extension points of this type on the same member, they will be invoked in the order in which
    /// the attributes were declared.
    /// </summary>
    public interface IInstancePreinitializer : IExtensionPoint
    {
        /// <summary>
        /// Called each time a new instance of the target member (or its enclosing class) is instantiated.
        /// </summary>
        /// <param name="instance">The new instance of the target member (or its enclosing class)</param>
        /// <param name="member">The member to which this extension point was applied</param>
        #region InstancePreinitializer
        void Preinitialize(object instance, MemberInfo member);
        #endregion
    }

    /// <summary>
    /// When you use this generic version of IInstanceInitializer it allows you to provide a scope for which kind
    /// of members you want to apply the preinitialization to.  For example, if you just apply IInstanceInitializer to a class,
    /// you are limited to only adding state you know to define at the class level.  This interface allows you to
    /// specify, for example, a properties scope, in which case the extension point acts as though it were applied
    /// to each property in the containing class.
    /// </summary>
    /// <typeparam name="T">Must be one one of the interfaces defined in ExtensionPointScopes</typeparam>
    public interface IInstancePreinitializer<T> : IInstancePreinitializer where T : ExtensionPointScopes.Scope
    {
    }
}
