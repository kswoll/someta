# Memoize

This simple example shows how you can add [memoization](https://en.wikipedia.org/wiki/Memoization) to a method or property.  This is similar to `Lazy<T>` but more automatic.  It also demonstrates the concept of adding custom fields to the containing class, property/method interception, and instance initialization.  This implementation does not handle methods with more than one parameter.  The full implementation is at the bottom, but we'll take this step by step and start with properties:

snippet: MemoizeJustPropertiesNoLocking

#### Key Concepts:

* Implements `IStateExtensionPoint`:  
  This marker interface causes the attribute class to be scanned for properties of type `InjectedField<>` and initializes it with an instance that exposes methods to get and set the value of the field for a particular instance.
* Implements `IPropertyGetInterceptor`:  
  This interface exposes the `GetPropertyValue` method, which will get called instead of the original property's getter.  To get the value originally provided by the getter, the `getter` delegate is provided to you.
* The `Field` property allows us to get and set the cached value.

As you can see here, we put all this together in `GetPropertyValue` to get the value from the original getter the first time, store it, and return the cached value in subsequent calls.

#### Usage:

```
[Memoize]
public string ExpensiveGetter
{
    get
    {
        // Only gets called once
        // do expensive work
        return computedValue;
    }
}
```

You may be thinking to yourself, "ah, but wait! this isn't threadsafe."  This is true.  To make this threadsafe we will introduce a new field to store an object we will `lock` around.

snippet: MemoizeWithLocking

#### Key Concepts:

* Implements `IInstanceInitializer`  
  Exposes the method `Initialize` which gets called when a given instance is constructed.  This method gets called at the end of the original constructor(s).

We use the initializer to create a new instance of `object` that we will use for locking.  We then modify `GetPropertyValue` to surround the original body with a `lock` statement.

#### Wrapping up

Finally, let's add support for methods (and async methods):

snippet: Memoize

#### Key Concepts
* Implements `IAsyncMethodInterceptor` so we can await when proceeding to the original implementation
* The initializer now creates a new instance of `object` (or `AsyncLock` from [Nito.AsyncEx](https://github.com/StephenCleary/AsyncEx) in the case of async methods) that we will use for locking.
