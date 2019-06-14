using System;
using System.Reflection;
using NUnit.Framework;
using Shouldly;

namespace Someta.Fody.Tests
{
    [TestFixture]
    public class InstanceInitializerTests
    {
        [Test]
        public void ClassInitializer()
        {
            var o = new ClassInitializerTestClass();
            o.MemberInfo.ShouldBe(typeof(ClassInitializerTestClass));
        }

        [ClassInitializer]
        public class ClassInitializerTestClass
        {
            public MemberInfo MemberInfo { get; set; }
        }

        [InterceptorScope(InterceptorScope.Class)]
        public class ClassInitializerAttribute : Attribute, IInstanceInitializer
        {
            public void Initialize(object instance, MemberInfo member)
            {
                ((ClassInitializerTestClass)instance).MemberInfo = member;
            }
        }
    }
}