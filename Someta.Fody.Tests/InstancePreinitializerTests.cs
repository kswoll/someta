using NUnit.Framework;
using Shouldly;
using System.Reflection;

namespace Someta.Fody.Tests
{
    [TestFixture]
    public class InstancePreinitializerTests
    {
        [Test]
        public void ClassInitializer()
        {
            var o = new ClassInitializerTestClass();
            o.MemberInfo.ShouldBe(typeof(ClassInitializerTestClass));
        }

        [Test]
        public void PropertyInitializer()
        {
            var o = new PropertyInitializerTestClass();
            o.PropertyInfo.Name.ShouldBe(nameof(o.PropertyInfo));
        }

        [Test]
        public void Preinitializer()
        {
            var o = new PreinitializerClass();
            o.Value.ShouldBe("foobar");
        }

        //[ClassInitializer]
        public class ClassInitializerTestClass
        {
            public MemberInfo MemberInfo { get; set; }
        }

        public class ClassInitializerAttribute : Attribute, IInstancePreinitializer
        {
            public void Preinitialize(object instance, MemberInfo member)
            {
                ((ClassInitializerTestClass)instance).MemberInfo = member;
            }
        }

        public class PropertyInitializerTestClass
        {
           // [PropertyInitializer]
            public PropertyInfo PropertyInfo { get; set; }
        }

        public class PropertyInitializerAttribute : Attribute, IInstancePreinitializer
        {
            public void Preinitialize(object instance, MemberInfo member)
            {
                ((PropertyInitializerTestClass)instance).PropertyInfo = (PropertyInfo)member;
            }
        }

        [PreinitializerClass]
        public class PreinitializerClass
        {
            public string Value { get; set; }

            public PreinitializerClass()
            {
                Value = Value + "bar";
            }
        }

        public class PreinitializerClassAttribute : Attribute, IInstancePreinitializer
        {
            public void Preinitialize(object instance, MemberInfo member)
            {
                ((PreinitializerClass)instance).Value = "foo";
            }
        }
    }
}
