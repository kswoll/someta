using System;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using Shouldly;

namespace Someta.Fody.Tests
{
    [TestFixture]
    public class ExtensionPointTests
    {
        [Test]
        public void FieldInitialized()
        {
            var o = new TestClass();
            ObservableAsPropertyHelper<string>.ToProperty(o, x => x.StringProperty, "test");
            var value = o.StringProperty;
            value.ShouldBe("test");
        }

        public class TestClass
        {
            [ObservableAsProperty]
            public string StringProperty { get; private set; }
        }
        
        public class ObservableAsProperty : Attribute, IPropertyGetInterceptor, IStateExtensionPoint
        {
            public InjectedField<object> Field { get; set; }

            public object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter)
            {
                var helper = Field.GetValue(instance);
                var value = helper.GetType().GetProperty("Value").GetValue(helper);
                return value;
            }
        }

        public class ObservableAsPropertyHelper<TValue>
        {
            public static void ToProperty<T>(T instance, Expression<Func<T, object>> property, TValue value)
            {
                var propertyInfo = ((MemberExpression)property.Body).Member;
                var extensionPoint = propertyInfo.GetExtensionPoint<ObservableAsProperty>();
                extensionPoint.Field.SetValue(instance, new ObservableAsPropertyHelper<TValue> { Value = value });
            }

            public TValue Value { get; set; }
        }
    }
}