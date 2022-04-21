# State Extension Points

Someta allows you to add fields to the class containing your extension point.  There are a variety of reasons where you may want to track state for each instance that the extension point is attached to.  (For example, )

**Note**: static fields are not supported as there's no point.  If you need a static field, just declare it in your extension point as a normal static field.

## [IStateExtensionPoint](/Someta/IStateExtensionPoint.cs)

This interface works in conjunction with [`InjectedField<T>`](../../Someta/InjectedField.cs).  The purpose of this interface is to act as a marker for whether or not to look for injected fields in your extension point.  If injected fields are found, the corresponding properties will be set to a new instance of `InjectedField<T>` where it's set up in a way that will allow direct access to the field through the injected field's `GetValue` and `SetValue` methods.

### Example

snippet: StateExtensionPointExample