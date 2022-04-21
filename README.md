# Introduction
What is this library? It aims to be a swiss-army knife toolkit for easy meta programming in C# built on the back of the [Fody](https://github.com/Fody/Fody) engine.

## Installation

> Install-Package Someta.Fody

## Summary
Please see our [Wiki](https://github.com/kswoll/someta/wiki) for documentation and samples.

It provides a number of extension points for you to customize your compiled code.  The current set of available extension points are:

* [Property interceptors](Someta.Docs/ExtensionPoints/PropertyInterceptors.md)
  * `get` in the form of `IPropertyGetInterceptor`
  * `set` in the form of `IPropertySetInterceptor`
* Event interceptors
  * `add` in the form of `IEventAddInterceptor`
  * `remove` in the form of `IEventRemoveInterceptor`
* Method interceptors
  * `IMethodInterceptor` will intercept all methods unless the extension also implements `IAsyncMethodInterceptor`, in which case async methods (defined as returning an instance of `Task` or `Task<T>`) are ignored and handled by the following extension point.
  * `IAsyncMethodInterceptor` will intercept only methods that return `Task` or `Task<T>` and allows the interceptor to use async semantics (i.e. `await`) when intercepting.
* [State](Someta.Docs/ExtensionPoints/StateExtensionPoints.md)
  * `IStateExtensionPoint` allows you to inject fields into the host so your extension can track state against your types and instances directly.
* Non public access
  * `INonPublicAccess` allows you to inject access to non-public members of the annotated class.  Used with `InjectAccessAttribute`
`InjectTargetAttribute`.
* Instance initialization
  * `IInstanceInitializer` gives you a place to add behavior to the end of the constructor of the annotated class.  Useful for instantiating fields when using the state extension point.
* Instance preinitialization
  * `IInstancePreinitializer` gives you a place to add behavior to the start of the constructor of the annotated class.  Useful for instantiating fields when using the state extension point, and you want them initialized before the actual constructor runs.  Note that preinitialization begins after calling the base class constructor.

## Extensions

To use these extension points, you simply subclass `Attribute`, implement one or more of the above interfaces, and decorate your type or members depending on your scenario.  See our various [samples](https://github.com/kswoll/someta/wiki/Samples) if you want to learn by example.

### Chaining
Particularly with the interceptor form of extension points, it should be noted that it's perfectly acceptable to chain multiple extensions on a single member. For example, with an `IPropertyGetInterceptor`, you can declare multiple extensions (i.e. `[ExtensionA, ExtensionB]`) and both will be applied to the property -- in that order.  You can see a demonstration of this in our [unit tests](https://github.com/kswoll/someta/blob/master/Someta.Fody.Tests/ChainingTests.cs).

### Scopes
Some extension point types allow you to specify the scope on which the extension should apply. For example, IStateExtensionPoint has a generic version that takes as its type argument one of the types defined in `ExtensionPointScopes`.  By default, if your extension point is applied to a class, there will only be one instance of your extension and the only context you have is the type of the containing class.  However, often you'll want to apply a single extension to your class but want the equivalent of having applied the extension to each (for example) property in that class. That's where these scopes come into play.

To make this example a bit more clear, imagine you defined an extension point that implements `IStateExtensionPoint<T>` and you apply it to your class, but you want to actually add state for each property in the class.  To do that, you would simply implement `IStateExtensionPoint<ExtensionPointScopes.Property>` and Someta will understand that to mean that instead of applying the extension to the class, it will instead apply it to each property in the class.

One final question one might ask is why these scoped versions of the extension point interfaces aren't available for all the extension types.  The reason is that some of the extension types can only work for a particular scope.  For example, property interceptors can clearly only work on properties, so if a property interceptor extension is applied to a class, it operates implicitly as though the scope was set to Property.

## FAQ

Please see our [FAQ](Someta.Docs/FAQ.md) to see if it addresses any of your questions or concerns.