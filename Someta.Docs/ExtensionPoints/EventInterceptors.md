<!--
GENERATED FILE - DO NOT EDIT
This file was generated by [MarkdownSnippets](https://github.com/SimonCropp/MarkdownSnippets).
Source File: /Someta.Docs/ExtensionPoints/EventInterceptors.source.md
To change this file edit the source file and then run MarkdownSnippets.
-->

# Event Interceptors

Someta supports event interceptors.  What this means is that when you decorate your event with an implementation of one or both of `IEventAddInterceptor` and `IEventRemoveInterceptor` you can have your own code called instead.  Both event adds and removes allow you to call the original implementation via a provided delegate.

## [IEventAddInterceptor](/Someta/IEventAddInterceptor.cs)

This interface has one method:

<!-- snippet: EventAddInterceptor -->
<a id='snippet-eventaddinterceptor'></a>
```cs
void AddEventHandler(EventInfo eventInfo, object instance, Delegate handler, Action<Delegate> proceed);
```
<sup><a href='/Someta/IEventAddInterceptor.cs#L19-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-eventaddinterceptor' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

As you can see, your implementation is provided with everything you need to customize the behavior of the add accessor.  If you don't want to call the original add, simply don't invoke `proceed`.

### Example

<!-- snippet: PropertyGetInterceptorExample -->
<a id='snippet-propertygetinterceptorexample'></a>
```cs
public void PropertyGetExample()
{
    var testClass = new PropertyGetTestClass();
    testClass.Value = 3;
    Console.WriteLine(testClass.Value);     // Prints 6
}

class PropertyGetTestClass
{
    [PropertyGetInterceptor]
    public int Value { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
class PropertyGetInterceptor : Attribute, IPropertyGetInterceptor
{
    public object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter)
    {
        var currentValue = (int)getter();
        return currentValue * 2;
    }
}
```
<sup><a href='/Someta.Docs/Source/Samples/PropertyGetInterceptorExample.cs#L10-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-propertygetinterceptorexample' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## [IPropertySetInterceptor](/Someta/IPropertySetInterceptor.cs)

This interface has one method:

<!-- snippet: EventRemoveInterceptor -->
<a id='snippet-eventremoveinterceptor'></a>
```cs
void RemoveEventHandler(EventInfo eventInfo, object instance, Delegate handler, Action<Delegate> proceed);
```
<sup><a href='/Someta/IEventRemoveInterceptor.cs#L19-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-eventremoveinterceptor' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

As you can see, your implementation is provided with everything you need to customize the behavior of the remove accessor.  If you don't want to call the original remove, simply don't invoke `proceed`.

### Example

<!-- snippet: PropertySetInterceptorExample -->
<a id='snippet-propertysetinterceptorexample'></a>
```cs
public void PropertySetExample()
{
    var testClass = new PropertySetTestClass();
    testClass.Value = 2;
    Console.WriteLine(testClass.Value);     // Prints 4
}

class PropertySetTestClass
{
    [PropertySetInterceptor]
    public int Value { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
class PropertySetInterceptor : Attribute, IPropertySetInterceptor
{
    public void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, Action<object> setter)
    {
        var value = (int)newValue;
        value *= 2;
        setter(value);
    }
}
```
<sup><a href='/Someta.Docs/Source/Samples/PropertySetInterceptorExample.cs#L10-L34' title='Snippet source file'>snippet source</a> | <a href='#snippet-propertysetinterceptorexample' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->
