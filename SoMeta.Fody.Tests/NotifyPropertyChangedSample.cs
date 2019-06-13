using System;
using System.ComponentModel;
using System.Reflection;
using NUnit.Framework;
using Shouldly;

namespace SoMeta.Fody.Tests
{
    [TestFixture]
    public class NotifyPropertyChangedSample
    {
        [Test]
        public void VerifyChangeNotification()
        {
            var o = new TestClass();
            var newValue = "";
            o.PropertyChanged += (sender, args) => newValue = o.StringProperty;
            o.StringProperty = "foobar";
            newValue.ShouldBe("foobar");
        }

        [Test]
        public void VerifyChangeNotificationOnGenericClass()
        {
            var o = new GenericTestClass<string>();
            var newValue = "";
            o.PropertyChanged += (sender, args) => newValue = o.Property;
            o.Property = "foobar";
            newValue.ShouldBe("foobar");
        }

        [Test]
        public void VerifyChangeNotificationOnGenericClassValueType()
        {
            var o = new GenericTestClass<int>();
            var newValue = 0;
            o.PropertyChanged += (sender, args) => newValue = o.Property;
            o.Property = 1;
            newValue.ShouldBe(1);
        }

        [NotifyPropertyChanged]
        public class BaseClass : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

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

        public class TestClass : BaseClass
        {
            public string StringProperty { get; set; }
        }

        public class GenericTestClass<T> : BaseClass
        {
            public T Property { get; set; }
        }

        public class NotifyPropertyChangedAttribute : Attribute, IPropertySetInterceptor, IClassEnhancer
        {
            [InjectAccess("OnPropertyChanged")]
            public Action<object, string, object, object> OnPropertyChanged { get; set; }

            public void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, Action<object> setter)
            {
                if (!Equals(oldValue, newValue))
                {
                    setter(newValue);
                    OnPropertyChanged(instance, propertyInfo.Name, oldValue, newValue);
                }
            }
        }
    }
}