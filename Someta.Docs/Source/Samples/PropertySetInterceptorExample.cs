using NUnit.Framework;
using System.Reflection;

namespace Someta.Docs.Source.Samples;

[TestFixture]
public class PropertySetInterceptorExample
{
    [Test]
    #region PropertySetInterceptorExample
    public void PropertySetExample()
    {
        var testClass = new PropertySetTestClass();
        testClass.Value = 2;
        Console.WriteLine(testClass.Value);     // Prints 4
    }

    class PropertySetTestClass
    {
        [PropertySetInterceptor]
        public int Value { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    class PropertySetInterceptor : Attribute, IPropertySetInterceptor
    {
        public void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, Action<object> setter)
        {
            var value = (int)newValue;
            value *= 2;
            setter(value);
        }
    }
    #endregion
}