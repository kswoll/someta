using System;
using System.Linq;
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

        [Test]
        public void StaticSumParameters()
        {
            var value = TestClass.StaticSum(1, 2, 3, 4);
            value.ShouldBe(10);
        }

        [Test]
        public void ConcatParameterTypesTest()
        {
            var o = new GenericClass<float>();
            var types = o.ConcatTypes1(1.1f);
            types[0].ShouldBe(typeof(float));
        }

        [Test]
        public void GenericConcatParameterTypesTest()
        {
            var o = new GenericClass<float>();
            var types = o.WithGenericParameters(1.1f, 1L, 1d);
            types[0].ShouldBe(typeof(float));
            types[1].ShouldBe(typeof(long));
            types[2].ShouldBe(typeof(double));
        }

        public class LogInterceptorAttribute : MethodInterceptorAttribute
        {
            public override object InvokeMethod(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], object> invoker)
            {
                ((TestClass)instance).InvocationCount++;
                return base.InvokeMethod(methodInfo, instance, parameters, invoker);
            }
        }

        public class ConcatParameterTypes : MethodInterceptorAttribute
        {
            public override object InvokeMethod(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], object> invoker)
            {
                return parameters.Select(x => x.GetType()).ToArray();
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

            [SumParametersMethod]
            public static int StaticSum(int value1, int value2, int value3, int value4)
            {
                return 0;
            }
        }

        public class GenericClass<T>
        {
            [ConcatParameterTypes]
            public Type[] ConcatTypes1(T a)
            {
                return null;
            }

            [ConcatParameterTypes]
            public Type[] WithGenericParameters<U, V>(T a, U u, V v)
            {
                return null;
            }

            private void GenericWrapper<U2, V2>(T a, U2 u, V2 v)
            {
                WithGenericParameters<U2, V2>(a, u, v);
            }
        }
    }
}