﻿using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Shouldly;

namespace SoMeta.Fody.Tests
{
    [TestFixture]
    public class AsyncMethodInterceptorTests
    {
        [Test]
        public async Task SimpleAsyncTest()
        {
            var o = new TestClass();
            var length = await o.M(0, 1);   
            length.ShouldBe(2);
        }

        public class TestClass
        {
//            [AsyncTestInterceptor]
            public async Task<int> M(int a, long b)
            {
                await Task.Delay(1);
                return 0;
            }
        }

        public class AsyncTestInterceptor : AsyncMethodInterceptorAttribute
        {
            public override async Task<object> InvokeMethod(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], Task<object>> invoker)
            {
                await Task.Delay(1);
                return parameters.Length;
            }
        }
    }
}