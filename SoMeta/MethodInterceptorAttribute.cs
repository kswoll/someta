using System;
using System.Reflection;

namespace SoMeta
{
    [AttributeUsage(AttributeTargets.Method)]
    public abstract class MethodInterceptorAttribute : InterceptorAttribute
    {
        public virtual object Invoke(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], object> invoker)
        {
            return invoker(parameters);
        }
    }
}