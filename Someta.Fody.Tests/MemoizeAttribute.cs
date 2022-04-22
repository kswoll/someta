using System;
using System.Reflection;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Someta;

namespace Someta.Fody.Tests
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class MemoizeAttribute : Attribute, IPropertyGetInterceptor, IMethodInterceptor, IAsyncMethodInterceptor,
        IInstanceInitializer, IStateExtensionPoint
    {
        public InjectedField<object> Field { get; set; }
        public InjectedField<object> Locker { get; set; }

        public void Initialize(object instance, MemberInfo member)
        {
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

        public async Task<object> InvokeAsync(MethodInfo methodInfo, object instance, object[] arguments, Func<object[], Task<object>> invoker)
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
}