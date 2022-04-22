using ReactiveUI;
using System.Reflection;

namespace Someta.Docs.Source.Samples;

#region Reactive
[AttributeUsage(AttributeTargets.Property)]
public class ReactiveAttribute : Attribute, IPropertySetInterceptor
{
    public void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, Action<object> setter)
    {
        if (!Equals(oldValue, newValue))
        {
            setter(newValue);
            ((IReactiveObject)instance).RaisePropertyChanged(propertyInfo.Name);
        }
    }
}
#endregion