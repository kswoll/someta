using NUnit.Framework;
using Shouldly;
using Someta.Docs.Source.Samples;

namespace Someta.Docs.Tests.Samples;

[TestFixture]
public class UnnullableTests
{
    [Test]
    #region UnnullableExample
    public void UnnullableExample()
    {
        var testClass = new TestClass();

        testClass.Value.ShouldBeNull();
        testClass.Value = "foo";
        testClass.Value.ShouldBe("foo");
        testClass.Value = null;
        testClass.Value.ShouldBe("foo");
    }

    public class TestClass
    {
        [Unnullable]
        public string? Value { get; set; }
    }
    #endregion
}