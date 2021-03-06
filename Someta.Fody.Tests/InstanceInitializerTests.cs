﻿using System;
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

        [Test]
        public void PropertyInitializer()
        {
            var o = new PropertyInitializerTestClass();
            o.PropertyInfo.Name.ShouldBe(nameof(o.PropertyInfo));
        }

        [ClassInitializer]
        public class ClassInitializerTestClass
        {
            public MemberInfo MemberInfo { get; set; }
        }

        public class ClassInitializerAttribute : Attribute, IInstanceInitializer
        {
            public void Initialize(object instance, MemberInfo member)
            {
                ((ClassInitializerTestClass)instance).MemberInfo = member;
            }
        }

        public class PropertyInitializerTestClass
        {
            [PropertyInitializer]
            public PropertyInfo PropertyInfo { get; set; }
        }

        public class PropertyInitializerAttribute : Attribute, IInstanceInitializer
        {
            public void Initialize(object instance, MemberInfo member)
            {
                ((PropertyInitializerTestClass)instance).PropertyInfo = (PropertyInfo)member;
            }
        }
    }
}