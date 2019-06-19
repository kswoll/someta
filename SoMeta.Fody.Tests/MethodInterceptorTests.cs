using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Shouldly;
using Someta.Reflection;

namespace Someta.Fody.Tests
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

        [Test]
        public void NestedGenericConcatParameterTypesTest()
        {
            var o = new NestedGenericClasses<float>.Level2<double>();

            var types = o.WithGenericParameters(1.1f, 1d, 1L, "foo");
            types[0].ShouldBe(typeof(float));
            types[1].ShouldBe(typeof(double));
            types[2].ShouldBe(typeof(long));
            types[3].ShouldBe(typeof(string));
        }

        public class LogInterceptorAttribute : MethodInterceptorAttribute
        {
            public override object Invoke(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], object> invoker)
            {
                ((TestClass)instance).InvocationCount++;
                return invoker(parameters);
            }
        }

        public class ConcatParameterTypes : MethodInterceptorAttribute
        {
            public override object Invoke(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], object> invoker)
            {
                return ((Type[])invoker(parameters)).Concat(parameters.Select(x => x.GetType())).ToArray();
            }
        }

        public class StringInterceptorAttribute : MethodInterceptorAttribute
        {
            public string Data { get; }

            public StringInterceptorAttribute(string data)
            {
                Data = data;
            }

            public override object Invoke(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], object> invoker)
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
                return new Type[0];
            }

            public void ConcatTypesWrapper()
            {
                ConcatTypes1(default);
            }

            [ConcatParameterTypes]
            public Type[] WithGenericParameters<U, V>(T a, U u, V v)
            {
                return new Type[0];
            }

            private void GenericWrapper<U2, V2>(T a, U2 u, V2 v)
            {
                WithGenericParameters<U2, V2>(a, u, v);
            }
        }

        public class NestedGenericClasses<T>
        {
            public class Level2<U>
            {
                [ConcatParameterTypes]
                public Type[] WithGenericParameters<V, W>(T a, U u, V v, W w)
                {
                    return new Type[0];
                }
            }
        }

        public class ClassWithMethodWithGenericParameters
        {
            [ConcatParameterTypes]
            public Type[] WithGenericParameters<U, V>(U u, V v)
            {
                return new Type[0];
            }

/*
            [ConcatParameterTypes]
            public Type[] WithGenericParameters<U, V>()
            {
                return new Type[0];
            }
*/
        }

        public struct GenericMethodTest<T>
        {
            private MethodInterceptorTests instance;

            public GenericMethodTest(MethodInterceptorTests instance)
            {
                this.instance = instance;
            }

//            [ConcatParameterTypes]
            public void Proceed(object[] arguments)
            {
                var arg1 = (T)arguments[0];
                instance.MockOriginal<T>(arg1);
            }
        }

        public void Mock<T>(T t)
        {
            new GenericMethodTest<T>().Proceed(new object[] { t });
        }

        public void MockOriginal<T>(T t)
        {

        }
    }
}