<!--
GENERATED FILE - DO NOT EDIT
This file was generated by [MarkdownSnippets](https://github.com/SimonCropp/MarkdownSnippets).
Source File: /Someta.Docs/Samples/Reactive.source.md
To change this file edit the source file and then run MarkdownSnippets.
-->

# Reactive

[ReactiveUI](https://reactiveui.net/) provides a Fody extension (that I originally authored), but using this framework, you can just do it yourself, if you wanted.  It provides a good example of the sort of extensions you can author with minimal fuss:

<!-- snippet: Reactive -->
<a id='snippet-reactive'></a>
```cs
[AttributeUsage(AttributeTargets.Property)]
public class ReactiveAttribute : Attribute, IPropertySetInterceptor
{
    public void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, Action<object> setter)
    {
        if (!Equals(oldValue, newValue))
        {
            setter(newValue);
            ((IReactiveObject)instance).RaisePropertyChanged(propertyInfo.Name);
        }
    }
}
```
<sup><a href='/Someta.Docs/Source/Samples/ReactiveAttribute.cs#L6-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-reactive' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The basic idea is you want to call `RaisePropertyChanged` when the value of the property changes.  Here we use an `IPropertySetInterceptor` that can rather trivially do this for you without authoring your own Fody package.

Example usage:

<!-- snippet: ReactiveExample -->
<a id='snippet-reactiveexample'></a>
```cs
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
```
<sup><a href='/Someta.Docs/Tests/Samples/ReactiveTests.cs#L12-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-reactiveexample' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->
