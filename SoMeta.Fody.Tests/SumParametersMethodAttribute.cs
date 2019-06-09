using System;
using System.Linq;
using System.Reflection;

namespace SoMeta.Fody.Tests
{
    public class SumParametersMethodAttribute : MethodInterceptorAttribute
    {
        public override object InvokeMethod(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], object> invoker)
        {
            return parameters.Select(x => (int)x).Sum();
        }
    }
}