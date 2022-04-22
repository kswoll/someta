using NUnit.Framework;
using System.Reflection;

namespace Someta.Docs.Source.Samples;

[TestFixture]
public class MethodInterceptorExample
{
    [Test]
    #region MethodInterceptorExample
    public void MethodExample()
    {
        var testClass = new MethodTestClass();
        testClass.Method();
        Console.WriteLine(testClass.InvocationCount);     // Prints 1
    }

    class MethodTestClass
    {
        [MethodInterceptor]
        public void Method()
        {
        }

        public int InvocationCount { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    class MethodInterceptor : Attribute, IMethodInterceptor
    {
        public object Invoke(MethodInfo methodInfo, object instance, Type[] typeArguments, object[] arguments, Func<object[], object> invoker)
        {
            ((MethodTestClass)instance).InvocationCount++;
            return invoker(arguments);
        }
    }
    #endregion
}
