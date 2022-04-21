using NUnit.Framework;
using System.Reflection;

namespace Someta.Docs.Samples;

[TestFixture]
public class StateExtensionPointExample
{
    [Test]
    #region PropertyGetInterceptorExample
    public void PropertyGetExample()
    {
        var testClass = new StateExtensionPointTestClass();
        testClass.Run();
        var extensionPoint = ExtensionPoint.GetExtensionPoint<StateExtensionPoint>(testClass.GetType());
        var invocationCount = extensionPoint.GetCurrentValue(testClass);
        Console.WriteLine(invocationCount);     // Prints 1
    }

    [StateExtensionPoint]
    class StateExtensionPointTestClass
    {
        public int Value { get; set; }

        public void Run()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    class StateExtensionPoint : Attribute, IStateExtensionPoint, IMethodInterceptor
    {
        public InjectedField<int> TestField { get; set; } = default!;

        public object? Invoke(MethodInfo methodInfo, object instance, object[] arguments, Func<object[], object> invoker)
        {
            var value = TestField.GetValue(instance);
            var newValue = value + 1;
            TestField.SetValue(instance, newValue);
            return null;
        }

        public int GetCurrentValue(object instance) => TestField.GetValue(instance);
    }
    #endregion

}
