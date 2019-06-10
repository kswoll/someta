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

        [Test]
        public void NoParametersStringReturn()
        {
            var o = new TestClass();
            var value = o.S();
            value.ShouldBe("foobar");
        }

        [Test]
        public void SumParameters()
        {
            var o = new TestClass();
            var value = o.Sum(1, 2, 3, 4);
            value.ShouldBe(10);
        }

        public class LogInterceptorAttribute : MethodInterceptorAttribute
        {
            public override object InvokeMethod(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], object> invoker)
            {
                ((TestClass)instance).InvocationCount++;
                return base.InvokeMethod(methodInfo, instance, parameters, invoker);
            }
        }

        public class StringInterceptorAttribute : MethodInterceptorAttribute
        {
            public string Data { get; }

            public StringInterceptorAttribute(string data)
            {
                Data = data;
            }

            public override object InvokeMethod(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], object> invoker)
            {
                var originalValue = invoker(parameters);
                return originalValue + Data;
            }
        }

        public class TestClass
        {
            public int InvocationCount { get; set; }

            [LogInterceptor]
            public void M()
            {
            }

            [StringInterceptor("bar")]
            public string S()
            {
                return "foo";
            }

            [SumParametersMethod]
            public int Sum(int value1, int value2, int value3, int value4)
            {
                return 0;
            }
        }
    }
}