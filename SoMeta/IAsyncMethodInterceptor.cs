using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SoMeta
{
    public interface IAsyncMethodInterceptor : IInterceptor
    {
        Task<object> InvokeAsync(MethodInfo methodInfo, object instance, object[] arguments, Func<object[], Task<object>> invoker);
    }
}