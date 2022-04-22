using NUnit.Framework;
using System.ComponentModel;
using System.Reflection;

namespace Someta.Docs.Source.Samples;

[TestFixture]
public class NonPublicAccessExample
{
    [Test]
    #region NonPublicAccessExample
    public void NonPublicAccess()
    {
        var testClass = new NonPublicAccessTestClass();
        int counter = 0;
        testClass.PropertyChanged += (sender, args) => counter++;
        testClass.Value = 42;
        Console.WriteLine(counter);         // Prints 1
    }

    [NotifyPropertyChanged]
    class NonPublicAccessTestClass : INotifyPropertyChanged
    {
        // Except for this property, the rest of this class would be suitable as a base class for any class that wants to reuse this logic.
        public int Value { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        [InjectTarget(nameof(OnPropertyChanged))]
        protected virtual void OnPropertyChanged(string propertyName, object oldValue, object newValue)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // this is only here to cause an overload situation
        protected virtual void OnPropertyChanged(string propertyName, object oldValue, string newValue)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    class NotifyPropertyChanged : Attribute, IPropertySetInterceptor, INonPublicAccess
    {
        [InjectAccess("OnPropertyChanged")]
        public Action<object, string, object, object>? OnPropertyChanged { get; set; }

        public void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, Action<object> setter)
        {
            if (!Equals(oldValue, newValue))
            {
                setter(newValue);
                OnPropertyChanged?.Invoke(instance, propertyInfo.Name, oldValue, newValue);
            }
        }
    }
    #endregion
}
