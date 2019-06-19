using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Shouldly;

namespace Someta.Fody.Tests
{
    [TestFixture]
    public class AsyncMethodInterceptorTests
    {
        [Test]
        public async Task SimpleAsyncTest()
        {
            var o = new TestClass();
            var length = await o.M(0, 1);
            length.ShouldBe(7);
        }

        public class TestClass
        {
            [AsyncTestInterceptor]
            public async Task<int> M(int a, long b)
            {
                await Task.Delay(1);
                return 5;
            }
        }

        public class AsyncTestInterceptor : AsyncMethodInterceptorAttribute
        {
            public override async Task<object> InvokeAsync(MethodInfo methodInfo, object instance, object[] arguments, Func<object[], Task<object>> invoker)
            {
                await Task.Delay(1);
                var value = (int)await invoker(arguments);
                return value + arguments.Length;
            }
        }

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

        public class ConcatParameterTypes : AsyncMethodInterceptorAttribute
        {
            public override async Task<object> InvokeAsync(MethodInfo methodInfo, object instance, object[] arguments, Func<object[], Task<object>> invoker)
            {
                return ((Type[])await invoker(arguments)).Concat(arguments.Select(x => x.GetType())).ToArray();
            }
        }
    }
}