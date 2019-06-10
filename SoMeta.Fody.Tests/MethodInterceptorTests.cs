using System;
using System.Reflection;
using NUnit.Framework;
using Shouldly;

namespace SoMeta.Fody.Tests
{
    [TestFixture]
    public class MethodInterceptorTests
    {
        [Test]
        public void NoParametersVoidReturn()
        {
            var o = new TestClass();
            o.M();

            o.InvocationCount.ShouldBe(1);
        }

        public class LogInterceptorAttribute : MethodInterceptorAttribute
        {
            public override object InvokeMethod(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], object> invoker)
            {
                ((TestClass)instance).InvocationCount++;
                return base.InvokeMethod(methodInfo, instance, parameters, invoker);
            }
        }

        public class TestClass
        {
            public int InvocationCount { get; set; }

            [LogInterceptor]
            public void M()
            {
            }
        }
    }
}