<!--
GENERATED FILE - DO NOT EDIT
This file was generated by [MarkdownSnippets](https://github.com/SimonCropp/MarkdownSnippets).
Source File: /Someta.Docs/ExtensionPoints/NonPublicAccess.source.md
To change this file edit the source file and then run MarkdownSnippets.
-->

# Non Public Access Extension Points

Someta allows you to provide non-public access to methods in a target class. This can be useful, for example, if you have a `NotifyPropertyChanged` extension point with a protected `OnPropertyChanged` method, you can use this extension point to provide acccess to that protected method from within your extension point.

**Note**: Currently this extension point only supports methods.  (i.e. not properties, events, etc.)

## [INonPublicAccess](/Someta/INonPublicAccess.cs)

This interface works in conjunction with [`InjectAccess`](/Someta/InjectAccessAttribute.cs) and [`InjectTarget`](/Someta/InjectTargetAttribute.cs).  The purpose of this interface is to act as a marker for whether or not to look for properties decorated with the attribute `InjectAccess` in your extension point.  If such properties are found, the corresponding properties will be set to a delegate that matches the signature of the target method plus an addition parameter in the front representing the instance.

The target method is matched to the `InjectAccess` property by adding the `InjectTarget` attribute to the target method.  The `Key` property should be the same for both.

### Example

<!-- snippet: NonPublicAccessExample -->
<a id='snippet-nonpublicaccessexample'></a>
```cs
public void NonPublicAccess()
{
    var testClass = new NonPublicAccessTestClass();
    int counter = 0;
    testClass.PropertyChanged += (sender, args) => counter++;
    testClass.Value = 42;
    Console.WriteLine(counter);         // Prints 1
}

[NotifyPropertyChanged]
class NonPublicAccessTestClass : INotifyPropertyChanged
{
    // Except for this property, the rest of this class would be suitable as a base class for any class that wants to reuse this logic.
    public int Value { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    [InjectTarget(nameof(OnPropertyChanged))]
    protected virtual void OnPropertyChanged(string propertyName, object oldValue, object newValue)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // this is only here to cause an overload situation
    protected virtual void OnPropertyChanged(string propertyName, object oldValue, string newValue)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

[AttributeUsage(AttributeTargets.Class)]
class NotifyPropertyChanged : Attribute, IPropertySetInterceptor, INonPublicAccess
{
    [InjectAccess("OnPropertyChanged")]
    public Action<object, string, object, object>? OnPropertyChanged { get; set; }

    public void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, Action<object> setter)
    {
        if (!Equals(oldValue, newValue))
        {
            setter(newValue);
            OnPropertyChanged?.Invoke(instance, propertyInfo.Name, oldValue, newValue);
        }
    }
}
```
<sup><a href='/Someta.Docs/Source/Samples/NonPublicAccessExample.cs#L11-L57' title='Snippet source file'>snippet source</a> | <a href='#snippet-nonpublicaccessexample' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->
