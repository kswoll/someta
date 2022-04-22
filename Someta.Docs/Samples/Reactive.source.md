# Reactive

[ReactiveUI](https://reactiveui.net/) provides a Fody extension (that I originally authored), but using this framework, you can just do it yourself, if you wanted.  It provides a good example of the sort of extensions you can author with minimal fuss:

snippet: Reactive

The basic idea is you want to call `RaisePropertyChanged` when the value of the property changes.  Here we use an `IPropertySetInterceptor` that can rather trivially do this for you without authoring your own Fody package.

Example usage:

snippet: ReactiveExample