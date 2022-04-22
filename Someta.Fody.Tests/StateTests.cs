using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Shouldly;

namespace Someta.Fody.Tests
{
    [TestFixture]
    public class StateTests
    {
        [Test]
        public void PropertyState()
        {
            var o = new TestClass();
            o.Property = "one";
            o.Property = "two";
            o.Property = "three";
            o.Property.ShouldBe("three3");
        }

        [Test]
        public void Memoization()
        {
            var o = new MemoizationTestClass();
            var value1 = o.Value;
            var value2 = o.Value;

            o.InvocationCount.ShouldBe(1);
            value1.ShouldBe("foobar");
            value2.ShouldBe("foobar");
        }

        [Test]
        public void MemoizationInt()
        {
            var o = new MemoizationTestClass();
            var value1 = o.IntValue;
            var value2 = o.IntValue;

            o.InvocationCount.ShouldBe(1);
            value1.ShouldBe(1);
            value2.ShouldBe(1);
        }

        [Test]
        public void ClassState()
        {
            var o = new ClassStateTestClass();
            o.Property1 = "foo";
            o.PreviousPropertySet.ShouldBe(null);
            o.Property2 = "bar";
            o.PreviousPropertySet.ShouldBe(nameof(o.Property1));
            o.Property1 = "foo";
            o.PreviousPropertySet.ShouldBe(nameof(o.Property2));
        }

        [Test]
        public void StaticClassState()
        {
            StaticClassStateTestClass.Property1 = "foo";
            StaticClassStateTestClass.PreviousPropertySet.ShouldBe(null);
            StaticClassStateTestClass.Property2 = "bar";
            StaticClassStateTestClass.PreviousPropertySet.ShouldBe(nameof(StaticClassStateTestClass.Property1));
            StaticClassStateTestClass.Property1 = "foo";
            StaticClassStateTestClass.PreviousPropertySet.ShouldBe(nameof(StaticClassStateTestClass.Property2));
        }

        [Test]
        public async Task MethodMemoize()
        {
            var o = new MethodMemoizeTestClass();
            var value1 = await o.Call();
            var value2 = await o.Call();
            o.InvocationCount.ShouldBe(1);
            value1.ShouldBe(1);
            value2.ShouldBe(1);
        }

        private class TestClass
        {
            [PropertySetCounter]
            public string Property { get; set; }
        }

        private class PropertySetCounterAttribute : Attribute, IPropertySetInterceptor, IStateExtensionPoint<ExtensionPointScopes.Property>
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

        private class MemoizationTestClass
        {
            public int InvocationCount { get; set; }

            [Memoize]
            public string Value
            {
                get
                {
                    InvocationCount++;
                    return "foobar";
                }
            }

            [Memoize]
            public int IntValue
            {
                get
                {
                    InvocationCount++;
                    return 1;
                }
            }
        }

        [AttributeUsage(AttributeTargets.Property)]
        private class MemoizeAttribute : Attribute, IPropertyGetInterceptor, IInstanceInitializer<ExtensionPointScopes.Property>, IStateExtensionPoint<ExtensionPointScopes.Property>
        {
            public InjectedField<object> Field { get; set; }
            public InjectedField<object> Locker { get; set; }

            public void Initialize(object instance, MemberInfo memberInfo)
            {
                Locker.SetValue(instance, new object());
            }

            public object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter)
            {
                lock (Locker.GetValue(instance))
                {
                    var currentValue = Field.GetValue(instance);
                    if (currentValue == null)
                    {
                        currentValue = getter();
                        Field.SetValue(instance, currentValue);
                    }

                    return currentValue;
                }
            }
        }

        [ClassState]
        private class ClassStateTestClass
        {
            public string PreviousPropertySet { get; set; }
            public string Property1 { get; set; }
            public string Property2 { get; set; }
        }

        private class ClassStateAttribute : Attribute, IPropertySetInterceptor, IStateExtensionPoint
        {
            public InjectedField<string> Field { get; set; }

            public void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, Action<object> setter)
            {
                if (propertyInfo.Name == nameof(ClassStateTestClass.PreviousPropertySet))
                {
                    setter(newValue);
                    return;
                }

                ((ClassStateTestClass)instance).PreviousPropertySet = Field.GetValue(instance);
                setter(newValue);
                Field.SetValue(instance, propertyInfo.Name);
            }
        }

        [StaticClassState]
        private static class StaticClassStateTestClass
        {
            public static string PreviousPropertySet { get; set; }
            public static string Property1 { get; set; }
            public static string Property2 { get; set; }
        }

        private class StaticClassStateAttribute : Attribute, IPropertySetInterceptor, IStateExtensionPoint
        {
            [InjectField(isStatic: true)]
            public InjectedField<string> Field { get; set; }

            public void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, Action<object> setter)
            {
                if (propertyInfo.Name == nameof(StaticClassStateTestClass.PreviousPropertySet))
                {
                    setter(newValue);
                    return;
                }

                StaticClassStateTestClass.PreviousPropertySet = Field.GetValue(instance);
                setter(newValue);
                Field.SetValue(instance, propertyInfo.Name);
            }
        }

        private class MethodMemoizeTestClass
        {
            public int InvocationCount { get; set; }

            [MethodMemoize]
            public async Task<int> Call()
            {
                InvocationCount++;
                await Task.Delay(1);
                return 1;
            }
        }

        private class MethodMemoizeAttribute : Attribute, IAsyncMethodInterceptor, IStateExtensionPoint<ExtensionPointScopes.Method>
        {
            public InjectedField<object> Field { get; set; }

            public async Task<object> InvokeAsync(MethodInfo methodInfo, object instance, Type[] typeArguments, object[] arguments, Func<object[], Task<object>> invoker)
            {
                var currentValue = Field.GetValue(instance);
                if (currentValue == null)
                {
                    currentValue = await invoker(arguments);
                    Field.SetValue(instance, currentValue);
                }

                return currentValue;
            }
        }
    }
}