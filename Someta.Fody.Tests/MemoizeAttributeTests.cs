using System.Threading.Tasks;
using NUnit.Framework;
using Shouldly;

namespace Someta.Fody.Tests
{
    [TestFixture]
    public class MemoizeAttributeTests
    {
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
        public void MemoizationMethod()
        {
            var o = new MemoizationTestClass();
            o.Method();
            o.Method();

            o.InvocationCount.ShouldBe(1);
        }

        [Test]
        public async Task MemoizationAsyncMethod()
        {
            var o = new MemoizationTestClass();
            await o.AsyncMethod();
            await o.AsyncMethod();

            o.InvocationCount.ShouldBe(1);
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

            [Memoize]
            public int Method()
            {
                InvocationCount++;
                return 1;
            }

            [Memoize]
            public async Task<string> AsyncMethod()
            {
                await Task.Delay(1);
                InvocationCount++;
                return "foo";
            }
        }
    }
}