using NUnit.Framework;
using Shouldly;
using System.Reflection;

namespace Someta.Fody.Tests
{
    [TestFixture]
    public class NonPublicAccessTests
    {
        [Test]
        public void NonPublicField()
        {
            var testClass = new NonPublicTestClass();
            testClass.Run();

            testClass.NoParametersVoidWasInvoked.ShouldBeTrue();
        }

        [NonPublicTest]
        public class NonPublicTestClass
        {
            public const string NoParametersVoidWasInvokedName = nameof(NoParametersVoid);

            public bool NoParametersVoidWasInvoked { get; set; }

            [InjectTarget(NoParametersVoidWasInvokedName)]
            private void NoParametersVoid()
            {
                NoParametersVoidWasInvoked = true;
            }

            public void Run()
            {
            }
        }

        public class NonPublicTestAttribute : Attribute, INonPublicAccess, IMethodInterceptor
        {
            [InjectAccess(NonPublicTestClass.NoParametersVoidWasInvokedName)]
            public Action<object> NoParametersVoid { get; set; }

            public object Invoke(MethodInfo methodInfo, object instance, object[] arguments, Func<object[], object> invoker)
            {
                if (methodInfo.Name != NonPublicTestClass.NoParametersVoidWasInvokedName)
                {
                    NoParametersVoid(instance);
                }
                return invoker(arguments);
            }
        }
    }
}