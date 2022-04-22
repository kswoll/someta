using System;
using System.Reflection;
using NUnit.Framework;
using Shouldly;

namespace Someta.Fody.Tests
{
    [TestFixture]
    public class ClassLevelInterceptorTests
    {
        [Test]
        public void Methods()
        {
            var o = new MethodClass();
            var s1 = o.Method1();
            var s2 = o.Method2();

            s1.ShouldBe(nameof(o.Method1));
            s2.ShouldBe(nameof(o.Method2));
        }

        [Test]
        public void Property()
        {
            var o = new PropertyClass();
            o.Property = "bar";
            var value = o.Property;
            value.ShouldBe("barfoo");
        }

        [ReturnMethodName]
        public class MethodClass
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

        [GetPropertyValue]
        public class PropertyClass
        {
            public string Property { get; set; }
        }

        private class ReturnMethodNameAttribute : Attribute, IMethodInterceptor
        {
            public object Invoke(MethodInfo methodInfo, object instance, Type[] typeArguments, object[] parameters, Func<object[], object> invoker)
            {
                return methodInfo.Name;
            }
        }

        private class GetPropertyValueAttribute : Attribute, IPropertyGetInterceptor
        {
            public object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter)
            {
                return getter() + "foo";
            }
        }
    }
}