using NUnit.Framework;
using System.Reflection;

namespace Someta.Docs.Samples;

[TestFixture]
public class AsyncMethodInterceptorExample
{
    [Test]
    #region AsyncMethodInterceptorExample
    public async Task AsyncMethodExample()
    {
        var testClass = new AsyncMethodTestClass();
        await testClass.AsyncMethod();
        Console.WriteLine(testClass.InvocationCount);     // Prints 1
    }

    class AsyncMethodTestClass
    {
        [AsyncMethodInterceptor]
        public async Task AsyncMethod()
        {
            await Task.Delay(0);        // Just to force await semantics
        }

        public int InvocationCount { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    class AsyncMethodInterceptor : Attribute, IAsyncMethodInterceptor
    {
        public async Task<object> InvokeAsync(MethodInfo methodInfo, object instance, Type[] typeArguments, object[] arguments, Func<object[], Task<object>> invoker)
        {
            await Task.Delay(0);        // Just to demonstrate await semantics
            ((AsyncMethodTestClass)instance).InvocationCount++;
            return await invoker(arguments);
        }
    }
    #endregion
}