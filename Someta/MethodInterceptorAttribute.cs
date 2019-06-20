using System;
using System.Reflection;

namespace Someta
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public abstract class MethodInterceptorAttribute : ExtensionPointAttribute, IMethodInterceptor
    {
        public abstract object Invoke(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], object> invoker);
    }
}