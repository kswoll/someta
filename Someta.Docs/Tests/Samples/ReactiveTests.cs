using NUnit.Framework;
using ReactiveUI;
using Shouldly;
using Someta.Docs.Source.Samples;

namespace Someta.Docs.Tests.Samples;

[TestFixture]
public class ReactiveTests
{
    [Test]
    #region ReactiveExample
    public void ReactiveExample()
    {
        var o = new TestClass();
        string? lastValue = null;
        o.WhenAnyValue(x => x.TestProperty).Subscribe(x => lastValue = x);
        o.TestProperty = "foo";
        lastValue.ShouldBe("foo");
    }

    public class TestClass : ReactiveObject
    {
        [Reactive]
        public string? TestProperty { get; set; }
    }
    #endregion
}
