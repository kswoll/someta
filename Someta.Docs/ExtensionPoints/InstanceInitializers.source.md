# Initializer Extension Points

Someta allows you to inject behavior into the constructor of a target class.  We provide two interfaces, one to add behavior at the start of the constructor and the other to add behavior at the end.  Sometimes you may want to wait for the constructor of the target class to complete before adding your own logic.  For that use `IInstanceInitializer`.  At other times, you may want to instantiate some state (through `InjectedField<T>`) and initialize it before the constructor of the target class starts so as to make available behavior within the constructor itself.

## [IInstanceInitializer](/Someta/IInstanceInitializer.cs)

This interface has one method:

snippet: InstanceInitializer

### Example

snippet: InstanceInitializerExample

## [IInstancePreinitializer](/Someta/IInstancePreinitializer.cs)

This interface has one method:

snippet: MethodInterceptor

### Example

snippet: InstancePreinitializerExample