using System;
using System.Reflection;

namespace SoMeta
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public abstract class MethodInterceptorAttribute : InterceptorAttribute, IMethodInterceptor
    {
        public abstract object Invoke(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], object> invoker);
    }
}