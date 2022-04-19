using System;
using System.Reflection;

namespace Someta
{
    /// <summary>
    /// Provides an extension point to trap method invocations.
    /// </summary>
    public interface IMethodInterceptor : IExtensionPoint
    {
        object Invoke(MethodInfo methodInfo, object instance, object[] arguments, Func<object[], object> invoker);
    }
}