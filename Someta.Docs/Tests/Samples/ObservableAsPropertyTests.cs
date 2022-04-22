using NUnit.Framework;
using ReactiveUI;
using Shouldly;
using Someta.Docs.Source.Samples;
using System.Reactive.Subjects;

namespace Someta.Docs.Tests.Samples;

#region ObservableAsPropertyExample
[TestFixture]
public class ObservableAsPropertyTests
{
    [Test]
    public void ObservableAsPropertyExample()
    {
        var subject = new Subject<string>();
        var o = new TestClass();
        subject.ToPropertyEx(o, x => x.StringProperty);

        var initialValue = o.StringProperty;
        initialValue.ShouldBeNull();

        subject.OnNext("first value");

        var nextValue = o.StringProperty;
        nextValue.ShouldBe("first value");
    }

    public class TestClass : ReactiveObject
    {
        [ObservableAsProperty]
        public string? StringProperty { get; }
    }
}
#endregion