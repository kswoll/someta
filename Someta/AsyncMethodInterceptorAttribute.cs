using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Someta
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public abstract class AsyncMethodInterceptorAttribute : ExtensionPointAttribute, IAsyncMethodInterceptor
    {
        public virtual Task<object> InvokeAsync(MethodInfo methodInfo, object instance, object[] arguments, Func<object[], Task<object>> invoker)
        {
            return invoker(arguments);
        }
    }
}