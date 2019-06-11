using System;
using System.Reflection;
using NUnit.Framework;
using Shouldly;

namespace SoMeta.Fody.Tests
{
    [TestFixture]
    public class ClassLevelInterceptorTests
    {
        [Test]
        public void Methods()
        {
            var o = new TestClass();
            var s1 = o.Method1();
            var s2 = o.Method2();

            s1.ShouldBe(nameof(o.Method1));
            s2.ShouldBe(nameof(o.Method2));
        }

        [ReturnMethodName]
        public class TestClass
        {
            public string Method1()
            {
                return "";
            }

            public string Method2()
            {
                return "";
            }
        }

        private class ReturnMethodNameAttribute : MethodInterceptorAttribute
        {
            public override object Invoke(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], object> invoker)
            {
                return methodInfo.Name;
            }
        }
    }
}