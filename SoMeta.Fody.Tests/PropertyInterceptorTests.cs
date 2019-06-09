using System;
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
            var o = new DoubleStringOnSetClass();
            o.StringProperty = "foo";
            o.StringProperty2 = "bar";
            o.StringProperty.ShouldBe("foofoo");
            o.StringProperty2.ShouldBe("barbar");
        }

        [Test]
        public void SetterTest()
        {
            var action = new Action<object>(Setter);
        }

        private class DoubleStringOnSetClass
        {
            [DoubleStringOnSet]
            public string StringProperty { get; set; }

            [DoubleStringOnSet]
            public string StringProperty2 { get; set; }
        }

        private void Setter(object o)
        {
        }
    }
}