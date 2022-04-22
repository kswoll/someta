using NUnit.Framework;
using System.Reflection;

namespace Someta.Docs.Source.Samples;

[TestFixture]
public class PropertyGetInterceptorExample
{
    [Test]
    #region PropertyGetInterceptorExample
    public void PropertyGetExample()
    {
        var testClass = new PropertyGetTestClass();
        testClass.Value = 3;
        Console.WriteLine(testClass.Value);     // Prints 6
    }

    class PropertyGetTestClass
    {
        [PropertyGetInterceptor]
        public int Value { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    class PropertyGetInterceptor : Attribute, IPropertyGetInterceptor
    {
        public object GetPropertyValue(PropertyInfo propertyInfo, object instance, Func<object> getter)
        {
            var currentValue = (int)getter();
            return currentValue * 2;
        }
    }
    #endregion
}
