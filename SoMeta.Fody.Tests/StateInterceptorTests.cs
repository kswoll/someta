using System;
using System.Reflection;
using NUnit.Framework;
using Shouldly;

namespace SoMeta.Fody.Tests
{
    [TestFixture]
    public class StateInterceptorTests
    {
        [Test]
        public void ClassState()
        {
            var o = new TestClass();
            o.Property = "one";
            o.Property = "two";
            o.Property = "three";
            o.Property.ShouldBe("three3");
        }

        private class TestClass
        {
            [PropertySetCounter]
            public string Property { get; set; }
        }

        private class PropertySetCounterAttribute : Attribute, IPropertyStateInterceptor, IPropertySetInterceptor
        {
            public InjectedField<int> Field { get; set; }

            public void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, Action<object> setter)
            {
                var current = Field.GetValue(instance);
                current++;
                Field.SetValue(instance, current);
                setter((string)newValue + current);
            }
        }
    }
}