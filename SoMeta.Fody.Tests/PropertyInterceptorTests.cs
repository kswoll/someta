﻿using System;
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
            TestClass.StaticAmount.ShouldBe("$200");
        }

        [Test]
        public void SumMethod()
        {
            var o = new TestClass();
            var result = o.Sum(1, 2, 3);
            result.ShouldBe(6);
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

            [DoubleStringOnSet]
            public static string StaticStringProperty { get; set; }

            [PrependValueWithDollarOnGet]
            public string Amount { get; set; }

            [PrependValueWithDollarOnGet]
            public static string StaticAmount { get; set; }

            public Task<string> StringTask => AsyncWork();

            private async Task<string> AsyncWork()
            {
                await Task.Delay(1);
                return StringProperty;
            }

            [SumParametersMethod]
            public int Sum(params int[] values)
            {
                return values.Length;
            }
        }

        private void Setter(object o)
        {
        }
    }
}