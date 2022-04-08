using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Shouldly;

namespace Someta.Fody.Tests
{
    [TestFixture]
    public class PropertyInterceptorTests
    {
        [Test]
        public void DoubleStringOnSet()
        {
            var o = new TestClass();
            o.StringProperty = "foo";
            o.StringProperty2 = "bar";
            TestClass.StaticStringProperty = "foobar";
            o.StringProperty.ShouldBe("foofoo");
            o.StringProperty2.ShouldBe("barbar");
            TestClass.StaticStringProperty.ShouldBe("foobarfoobar");
        }

        [Test]
        public void PrependDollarOnGet()
        {
            var o = new TestClass();
            o.Amount = "100";
            TestClass.StaticAmount = "200";

            o.Amount.ShouldBe("$100");
            TestClass.StaticAmount.ShouldBe("$200");
        }

        [Test]
        public void GenericPrependDollarOnGet()
        {
            var o = new GenericTestClass<string>();
            o.Amount = "100";
            o.Amount.ShouldBe("$100");
        }

/*
        [Test]
        public void SumMethod()
        {
            var o = new TestClass();
            var result = o.Sum(1, 2, 3);
            result.ShouldBe(6);
        }
*/

        [Test]
        public void GetOnly()
        {
            var o = new TestClass();
            o.GetOnly.ShouldBe("foobar");
        }

        private class TestClass
        {
            [DoubleStringOnSet]
            public string StringProperty { get; set; }

            [DoubleStringOnSet]
            public string StringProperty2 { get; set; }

            [DoubleStringOnSet]
            public static string StaticStringProperty { get; set; }

            [PrependValueWithDollarOnGet]
            public string Amount { get; set; }

            [PrependValueWithDollarOnGet]
            public static string StaticAmount { get; set; }

            [GetOnlyInterceptor]
            public string GetOnly { get; set; }

            public Task<string> StringTask => AsyncWork();

            private async Task<string> AsyncWork()
            {
                await Task.Delay(1);
                return StringProperty;
            }
        }

        private class GenericTestClass<T>
        {
            [PrependValueWithDollarOnGet]
            public T Amount { get; set; }
        }

        private class GetOnlyInterceptor : Attribute, IPropertyGetInterceptor
        {
            public object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter)
            {
                return "foobar";
            }
        }
    }
}