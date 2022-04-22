namespace Someta.Docs.Source.Samples;

using System.Reflection;

#region MemoizeJustPropertiesNoLocking
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
public class MemoizeAttributeJustPropertiesNoLocking : Attribute, IPropertyGetInterceptor, IStateExtensionPoint
{
    public InjectedField<object> Field { get; set; } = default!;
    public InjectedField<object> Locker { get; set; } = default!;

    public object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter)
    {
        var currentValue = Field.GetValue(instance);
        if (currentValue == null)
        {
            currentValue = getter();
            Field.SetValue(instance, currentValue);
        }

        return currentValue;
    }
}
#endregion

#region MemoizeWithLocking
[AttributeUsage(AttributeTargets.Property)]
public class MemoizeAttributeWithLocking : Attribute, IStateExtensionPoint, IPropertyGetInterceptor, IInstanceInitializer
{
    public InjectedField<object> Field { get; set; } = default!;
    public InjectedField<object> Locker { get; set; } = default!;

    public void Initialize(object instance, MemberInfo member)
    {
        Locker.SetValue(instance, new object());
    }

    public object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter)
    {
        lock (Locker.GetValue(instance))
        {
            var currentValue = Field.GetValue(instance);
            if (currentValue == null)
            {
                currentValue = getter();
                Field.SetValue(instance, currentValue);
            }

            return currentValue;
        }
    }
}
#endregion