using System;
using System.Linq;
using System.Reflection;

namespace Someta.Fody.Tests
{
    public class SumParametersMethodAttribute : MethodInterceptorAttribute
    {
        public override object Invoke(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], object> invoker)
        {
            return parameters.Select(x => (int)x).Sum();
        }
    }
}