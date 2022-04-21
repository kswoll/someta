# Non Public Access Extension Points

Someta allows you to provide non-public access to methods in a target class. This can be useful, for example, if you have a `NotifyPropertyChanged` extension point with a protected `OnPropertyChanged` method, you can use this extension point to provide acccess to that protected method from within your extension point.

**Note**: Currently this extension point only supports methods.  (i.e. not properties, events, etc.)

## [INonPublicAccess](/Someta/INonPublicAccess.cs)

This interface works in conjunction with [`InjectAccess`](/Someta/InjectAccessAttribute.cs) and [`InjectTarget`](/Someta/InjectTargetAttribute.cs).  The purpose of this interface is to act as a marker for whether or not to look for properties decorated with the attribute `InjectAccess` in your extension point.  If such properties are found, the corresponding properties will be set to a delegate that matches the signature of the target method plus an addition parameter in the front representing the instance.

The target method is matched to the `InjectAccess` property by adding the `InjectTarget` attribute to the target method.  The `Key` property should be the same for both.

### Example

snippet: NonPublicAccessExample