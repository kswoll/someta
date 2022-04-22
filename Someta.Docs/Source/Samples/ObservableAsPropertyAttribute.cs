using ReactiveUI;
using System.Linq.Expressions;
using System.Reactive.Concurrency;
using System.Reflection;

namespace Someta.Docs.Source.Samples;

#region ObservableAsProperty
[AttributeUsage(AttributeTargets.Property)]
public class ObservableAsPropertyAttribute : Attribute, IPropertyGetInterceptor, IStateExtensionPoint,
    IInstanceInitializer
{
    public InjectedField<object> Helper { get; set; } = default!;

    private Delegate? getValue;

    public void Initialize(object instance, MemberInfo member)
    {
        // Use expression trees to create a delegate of the form:
        //
        // Func<object, PropertyType> = x => ((ObservableAsPropertyHelper<PropertyType>)x).Value
        //
        // The first type parameter represents the ObservableAsPropertyHelper<> instance
        // The seconds type parameter is the property type of the property the attribute is associated with
        // You *could* just use simple reflection, but this solution is more performant
        var propertyInfo = (PropertyInfo)member;
        var observableAsPropertyHelperType = typeof(ObservableAsPropertyHelper<>).MakeGenericType(propertyInfo.PropertyType);
        var valueProperty = observableAsPropertyHelperType.GetProperty("Value")!;
        var instanceParameter = Expression.Parameter(typeof(object));
        var castedInstance = Expression.Convert(instanceParameter, observableAsPropertyHelperType);
        var body = Expression.MakeMemberAccess(castedInstance, valueProperty);
        var delegateType = typeof(Func<,>).MakeGenericType(typeof(object), propertyInfo.PropertyType);
        var lambda = Expression.Lambda(delegateType, body, instanceParameter);
        getValue = lambda.Compile();
    }

    public object? GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter)
    {
        var helper = Helper.GetValue(instance);
        return getValue!.DynamicInvoke(helper);
    }
}
#endregion

#region ObservableAsPropertyExtensions
public static class ObservableAsPropertyExtensions
{
    public static ObservableAsPropertyHelper<TRet> ToPropertyEx<TObj, TRet>(this IObservable<TRet> @this, TObj source, Expression<Func<TObj, TRet>> property, bool deferSubscription = false, IScheduler? scheduler = null)
        where TObj : ReactiveObject
    {
        var result = @this.ToProperty(source, property, deferSubscription, scheduler);

        // Now assign the field
        var propertyInfo = property.GetPropertyInfo();
        var extensionPoint = propertyInfo.GetExtensionPoint<ObservableAsPropertyAttribute>();
        extensionPoint.Helper.SetValue(source, result);

        return result;
    }

    private static PropertyInfo GetPropertyInfo(this LambdaExpression expression)
    {
        var current = expression.Body;
        if (current is UnaryExpression unary)
        {
            current = unary.Operand;
        }

        var call = (MemberExpression)current;
        return (PropertyInfo)call.Member;
    }
}
#endregion