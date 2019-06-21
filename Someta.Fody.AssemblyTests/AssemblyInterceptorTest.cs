using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Shouldly;
using Someta.Fody.AssemblyTests;

[assembly: AssemblyMethodInterceptor]

namespace Someta.Fody.AssemblyTests
{
    [TestFixture]
    public class AssemblyInterceptorTest
    {
        public static List<MethodInfo> Invocations { get; } = new List<MethodInfo>();

        [Test]
        public void InvokeTest()
        {
            var o = new TestClass();
            o.M();
            o.N();

            Invocations.Count.ShouldBe(2);
        }

        public class TestClass
        {
            public void M()
            {
            }

            public void N()
            {
            }
        }
    }

    public class AssemblyMethodInterceptorAttribute : Attribute, IMethodInterceptor
    {
        public object Invoke(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], object> invoker)
        {
            AssemblyInterceptorTest.Invocations.Add(methodInfo);
            return invoker(parameters);
        }
    }
}