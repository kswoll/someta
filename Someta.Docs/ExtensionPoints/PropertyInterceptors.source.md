# Markdown File

Someta supports property interceptors.  What this means is that when you decorate your property with an implementation of one or both of `IPropertyGetInterceptor` and `IPropertySetInterceptor` you can have your own code called instead.  Both property gets and sets allow you to call the original implementation via a provided delegate.

## IPropertyGetInterceptor

This interface has one method:

```
object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter);
```

As you can see, your implementation is provided with everything you need to customize the behavior of the getter.  If you don't want to call the original get, simply don't invoke `getter`.

snippet: PropertyGetInterceptorExample

## IPropertySetInterceptor

This interface has one method:

```
void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, Action<object> setter);
```

As you can see, your implementation is provided with everything you need to customize the behavior of the setter.  If you don't want to call the original set, simply don't invoke `setter`.

### Example

snippet: PropertySetInterceptorExample