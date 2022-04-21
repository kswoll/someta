# Event Interceptors

Someta supports event interceptors.  What this means is that when you decorate your event with an implementation of one or both of `IEventAddInterceptor` and `IEventRemoveInterceptor` you can have your own code called instead.  Both event adds and removes allow you to call the original implementation via a provided delegate.

## [IEventAddInterceptor](/Someta/IEventAddInterceptor.cs)

This interface has one method:

snippet: EventAddInterceptor

As you can see, your implementation is provided with everything you need to customize the behavior of the add accessor.  If you don't want to call the original add, simply don't invoke `proceed`.

### Example

snippet: PropertyGetInterceptorExample

## [IPropertySetInterceptor](/Someta/IPropertySetInterceptor.cs)

This interface has one method:

snippet: EventRemoveInterceptor

As you can see, your implementation is provided with everything you need to customize the behavior of the remove accessor.  If you don't want to call the original remove, simply don't invoke `proceed`.

### Example

snippet: PropertySetInterceptorExample