using System;
using System.Reflection;

namespace Someta
{
    /// <summary>
    /// Have your extension point implement this interface to be able to intercept method invocations.
    /// You can call the original implementation of the method via the provided setter delegate.
    ///
    /// NOTE: this does NOT apply to *properties* by design.  It needlessly complicates method interceptors
    /// and leads to surprising results when property getters and setters are included in method interception.
    /// NOTE: If your extension *also* implements IAsyncMethodInterceptor then this
    /// interface's Invoke method will not be called for methods that return Task.
    /// </summary>
    public interface IMethodInterceptor : IExtensionPoint
    {
        /// <summary>
        /// Called each time the method this interceptor is attached to is invoked.
        /// </summary>
        /// <param name="methodInfo">The MethodInfo representing the method this extension was applied to.</param>
        /// <param name="instance">The instance on which the method was invoked (or null for static methods).</param>
        /// <param name="arguments">The arguments passed to the method.</param>
        /// <param name="invoker">A delegate you can call to invoke the original method implementation.</param>
        /// <returns>The result of the method that should be returned to the caller.</returns>
        object Invoke(MethodInfo methodInfo, object instance, object[] arguments, Func<object[], object> invoker);
    }
}