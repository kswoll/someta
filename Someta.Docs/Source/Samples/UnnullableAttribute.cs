using System.Reflection;

namespace Someta.Docs.Source.Samples;

#region Unnullable
[AttributeUsage(AttributeTargets.Property)]
public class UnnullableAttribute : Attribute, IPropertySetInterceptor
{
    public void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, Action<object> setter)
    {
        if (newValue != null)
            setter(newValue);
    }
}
#endregion