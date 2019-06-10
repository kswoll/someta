using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SoMeta
{
    [AttributeUsage(AttributeTargets.Method)]
    public abstract class AsyncMethodInterceptorAttribute : InterceptorAttribute
    {
        public virtual Task<object> InvokeAsync(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], Task<object>> invoker)
        {
            return invoker(parameters);
        }
    }
}