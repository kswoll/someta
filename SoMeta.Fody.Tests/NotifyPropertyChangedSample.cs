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

        [NotifyPropertyChanged]
        public class BaseClass : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged(string propertyName, object oldValue, object newValue)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public class TestClass : BaseClass
        {
            public string StringProperty { get; set; }
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