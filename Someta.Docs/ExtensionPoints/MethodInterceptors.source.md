# Method Interceptors

Someta supports method interceptors.  What this means is that when you decorate your method with an implementation of one or both of `IMethodInterceptor` and `IAsyncMethodInterceptor` you can have your own code called instead.  Both kinds allow you to call the original implementation via a provided delegate.

The reason we have two types of interfaces is that for async interceptors, there's a good chance you'll want to use `await`.  Since `await` requires the return type of the interceptor 


## [IMethodInterceptor](/Someta/IMethodInterceptor.cs)

This interface has one method:

snippet: MethodInterceptor


### Example

snippet: StateExtensionPointExample


## [IAsyncMethodInterceptor](/Someta/IAsyncMethodInterceptor.cs)

This interface has one method:

snippet: AsyncMethodInterceptor