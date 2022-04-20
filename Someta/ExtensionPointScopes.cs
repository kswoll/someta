using System.Diagnostics.CodeAnalysis;

namespace Someta
{
    /// <summary>
    /// Some extension point types allow you to specify the scope on which the extension should apply.
    /// For example, IStateExtensionPoint has a generic version that takes as its type argument one of
    /// the types defined in this class.  By default, if your extension point is applied to a class,
    /// there will only be one instance of your extension and the only context you have is the type
    /// of the containing class.  However, often you'll want to apply a single extension to your
    /// class but want the equivalent of having applied the extension to each (for example) property
    /// in that class. That's where these scopes come into play.
    ///
    /// To make this example a bit more clear, imagine you defined an extension point that implements
    /// IStateExtensionPoint{T} and you apply it to your class, but you want to actually add state for
    /// each property in the class.  To do that, you would simply implement
    /// IStateExtensionPoint{ExtensionPointScopes.Property} and Someta will understand that to mean
    /// that instead of applying the extension to the class, it will instead apply it to each property
    /// in the class.
    ///
    /// One final question one might ask is why these scoped versions of the extension point interfaces
    /// aren't available for all the extension types.  The reason is that some of the extension
    /// types can only work for a particular scope.  For example, property interceptors can clearly
    /// only work on properties, so if a property interceptor extension is applied to a class, it
    /// operates implicitly as though the scope was set to Property.
    /// </summary>
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "This is a unique usage of interfaces and scans better when the type names aren't prefixed with 'I'")]
    public class ExtensionPointScopes
    {
        public interface Scope
        {
        }

        public interface Property : Scope
        {
        }

        public interface Method : Scope
        {
        }

        public interface Event : Scope
        {
        }

        public interface Class : Scope
        {
        }
    }
}