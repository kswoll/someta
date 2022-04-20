using System;
using System.Reflection;
using NUnit.Framework;
using Shouldly;

namespace Someta.Fody.Tests
{
    [TestFixture]
    public class ChainingTests
    {
        [Test]
        public void SimpleChain()
        {
            var o = new TestClass();
            o.Property = "foo";
            var value = o.Property;
            value.ShouldBe("fooAB");
        }

        [Test]
        public void SameChain()
        {
            var o = new TestClass();
            o.SameChain = "foo";
            var value = o.SameChain;
            value.ShouldBe("fooCD");
        }

        private class TestClass
        {
            [ChainA, ChainB]
            public string Property { get; set; }

            [Chain("C"), Chain("D")]
            public string SameChain { get; set; }
        }

        [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
        private class Chain : Attribute, IPropertyGetInterceptor
        {
            public string Suffix { get; }

            public Chain(string suffix)
            {
                Suffix = suffix;
            }

            public object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter)
            {
                return getter() + Suffix;
            }
        }

        [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
        private class ChainA : Attribute, IPropertyGetInterceptor
        {
            public object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter)
            {
                return getter() + "A";
            }
        }

        [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
        private class ChainB : Attribute, IPropertyGetInterceptor
        {
            public object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter)
            {
                return getter() + "B";
            }
        }
    }
}