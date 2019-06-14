using System;
using System.Reflection;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace Someta.Fody.Tests
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class MemoizeAttribute : Attribute, IPropertyStateInterceptor, IPropertyGetInterceptor,
        IMethodStateInterceptor, IMethodInterceptor, IAsyncMethodInterceptor, IInstanceInitializer
    {
        public InjectedField<object> Field { get; set; }
        public InjectedField<object> Locker { get; set; }
        public InjectedField<AsyncLock> AsyncLocker { get; set; }

        public void Initialize(object instance)
        {
            Locker.SetValue(instance, new object());
            AsyncLocker.SetValue(instance, new AsyncLock());
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

        public object Invoke(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], object> invoker)
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
            using (await AsyncLocker.GetValue(instance).LockAsync())
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