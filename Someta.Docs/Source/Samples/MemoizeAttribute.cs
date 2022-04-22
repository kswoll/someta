using Nito.AsyncEx;
using System.Reflection;

namespace Someta.Docs.Source.Samples;

#region Memoize
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
public class MemoizeAttribute : Attribute, IPropertyGetInterceptor, IStateExtensionPoint,
    IMethodInterceptor, IAsyncMethodInterceptor, IInstanceInitializer
{
    public InjectedField<object> Field { get; set; } = default!;
    public InjectedField<object> Locker { get; set; } = default!;

    public void Initialize(object instance, MemberInfo member)
    {
        // If an async method use an AsyncLock
        if (member is MethodInfo methodInfo && typeof(Task).IsAssignableFrom(methodInfo.ReturnType))
            Locker.SetValue(instance, new AsyncLock());
        else
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

    public object Invoke(MethodInfo methodInfo, object instance, Type[] typeArguments, object[] parameters, Func<object[], object> invoker)
    {
        lock (Locker.GetValue(instance))
        {
            var currentValue = Field.GetValue(instance);
            if (currentValue == null)
            {
                currentValue = invoker(parameters);
                Field.SetValue(instance, currentValue);
            }

            return currentValue;
        }
    }

    public async Task<object> InvokeAsync(MethodInfo methodInfo, object instance, Type[] typeArguments, object[] arguments, Func<object[], Task<object>> invoker)
    {
        using (await ((AsyncLock)Locker.GetValue(instance)).LockAsync())
        {
            var currentValue = Field.GetValue(instance);
            if (currentValue == null)
            {
                currentValue = await invoker(arguments);
                Field.SetValue(instance, currentValue);
            }

            return currentValue;
        }
    }
}
#endregion