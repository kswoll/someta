using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Shouldly;

namespace SoMeta.Fody.Tests
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
            o.StringProperty.ShouldBe("foofoo");
            o.StringProperty2.ShouldBe("barbar");
        }

        [Test]
        public void PrependDollarOnGet()
        {
            var o = new TestClass();
            o.Amount = "100";
            o.Amount.ShouldBe("$100");
        }

        [Test]
        public void SetterTest()
        {
            var action = new Action<object>(Setter);
        }

        private class TestClass
        {
            [DoubleStringOnSet]
            public string StringProperty { get; set; }

            [DoubleStringOnSet]
            public string StringProperty2 { get; set; }

            [PrependValueWithDollarOnGet]
            public string Amount { get; set; }

            public Task<string> StringTask => AsyncWork();

            private async Task<string> AsyncWork()
            {
                await Task.Delay(1);
                return StringProperty;
            }
        }

        private void Setter(object o)
        {
        }
    }
}