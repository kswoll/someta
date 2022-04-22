using System.Reflection;
using NUnit.Framework;
using Shouldly;

namespace Someta.Fody.Tests
{
    public class AsyncMethodInterceptorTests
    {
        [TestFixture]
        public class SimpleAsyncTests
        {
            [Test]
            public async Task SimpleAsyncTest()
            {
                var testClass = new TestClass();
                var value = await testClass.M(5);
                value.ShouldBe(10);
            }

            public class TestClass
            {
                [TestInterceptor]
                public async Task<int> M(int value)
                {
                    await Task.Delay(1);
                    return value;
                }
            }

            public class TestInterceptor : Attribute, IAsyncMethodInterceptor
            {
                public async Task<object> InvokeAsync(MethodInfo methodInfo, object instance, Type[] typeArguments, object[] arguments, Func<object[], Task<object>> invoker)
                {
                    await Task.Delay(1);
                    var value = (int)await invoker(arguments);
                    return value * 2;
                }
            }
        }

        [TestFixture]
        public class GenericTests
        {
            [Test]
            public async Task ConcatParameterTypesTest()
            {
                var o = new GenericClass<float>();
                var types = await o.ConcatTypes1(1.1f);
                types[0].ShouldBe(typeof(float));
            }

            [Test]
            public async Task GenericConcatParameterTypesTest()
            {
                var o = new GenericClass<float>();
                var types = await o.WithGenericParameters(1.1f, 1L, 1d);
                types[0].ShouldBe(typeof(float));
                types[1].ShouldBe(typeof(long));
                types[2].ShouldBe(typeof(double));
            }

            [Test]
            public async Task StaticGenericConcatParameterTypesTest()
            {
                var types = await GenericClass<float>.WithGenericParametersStatic(1.1f, 1L, 1d);
                types[0].ShouldBe(typeof(float));
                types[1].ShouldBe(typeof(long));
                types[2].ShouldBe(typeof(double));
            }

            [Test]
            public async Task NestedGenericConcatParameterTypesTest()
            {
                var o = new NestedGenericClasses<float>.Level2<double>();

                var types = await o.WithGenericParameters(1.1f, 1d, 1L, "foo");
                types[0].ShouldBe(typeof(float));
                types[1].ShouldBe(typeof(double));
                types[2].ShouldBe(typeof(long));
                types[3].ShouldBe(typeof(string));
            }

            public class GenericClass<T>
            {
                [ConcatParameterTypes]
                public async Task<Type[]> ConcatTypes1(T a)
                {
                    await Task.Delay(1);
                    return new Type[0];
                }

                [ConcatParameterTypes]
                public async Task<Type[]> WithGenericParameters<U, V>(T a, U u, V v)
                {
                    await Task.Delay(1);
                    return new Type[0];
                }

                [ConcatParameterTypes]
                public static async Task<Type[]> WithGenericParametersStatic<U, V>(T a, U u, V v)
                {
                    await Task.Delay(1);
                    return new Type[0];
                }
            }

            public class NestedGenericClasses<T>
            {
                public class Level2<U>
                {
                    [ConcatParameterTypes]
                    public async Task<Type[]> WithGenericParameters<V, W>(T a, U u, V v, W w)
                    {
                        await Task.Delay(1);
                        return new Type[0];
                    }
                }
            }

            public class ClassWithMethodWithGenericParameters
            {
                [ConcatParameterTypes]
                public async Task<Type[]> WithGenericParameters<U, V>(U u, V v)
                {
                    await Task.Delay(1);
                    return new Type[0];
                }
            }

            public class ConcatParameterTypes : Attribute, IAsyncMethodInterceptor
            {
                public async Task<object> InvokeAsync(MethodInfo methodInfo, object instance, Type[] typeArguments, object[] arguments, Func<object[], Task<object>> invoker)
                {
                    return ((Type[])await invoker(arguments)).Concat(arguments.Select(x => x.GetType())).ToArray();
                }
            }
        }
    }
}