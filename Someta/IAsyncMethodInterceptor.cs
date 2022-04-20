using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Someta
{
    /// <summary>
    /// Have your extension point implement this interface to be able to intercept *async* method
    /// invocations. By using this interface, you can use async/await semantics within your
    /// interceptor. Note: this does NOT apply to *properties* unless this interceptor is applied
    /// to the actual accessors (get; set;). You can call the original implementation of the method
    /// via the provided setter delegate. NOTE: If your extension *also* implements IMethodInterceptor
    /// then that interface's Invoke method will not be called for methods that return Task.  (As it
    /// will be handled by this interface)
    /// </summary>
    public interface IAsyncMethodInterceptor : IExtensionPoint
    {
        /// <summary>
        /// Called each time the method this interceptor is attached to is invoked.
        /// </summary>
        /// <param name="methodInfo">The MethodInfo representing the method this extension was applied to.</param>
        /// <param name="instance">The instance on which the method was invoked (or null for static methods).</param>
        /// <param name="arguments">The arguments passed to the method.</param>
        /// <param name="invoker">A delegate you can call to invoke the original method implementation.</param>
        /// <returns>The result of the method that should be returned to the caller.</returns>
        Task<object> InvokeAsync(MethodInfo methodInfo, object instance, object[] arguments, Func<object[], Task<object>> invoker);
    }
}