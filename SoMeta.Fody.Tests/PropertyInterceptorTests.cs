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
            o.StringProperty.ShouldBe("foofoo");
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
        }

        private void Setter(object o)
        {
        }
    }
}