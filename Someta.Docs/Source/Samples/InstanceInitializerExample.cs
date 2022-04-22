using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Someta.Docs.Source.Samples;

[TestFixture]
public class InstanceInitializerExample
{
    [Test]
    #region InstanceInitializerExample
    public void InitializerExample()
    {
        var testClass = new InitializerTestClass();
        Console.WriteLine(testClass.Value);         // Prints 1
    }

    [InitializerExtensionPoint]
    class InitializerTestClass
    {
        public int Value { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    class InitializerExtensionPoint : Attribute, IInstanceInitializer
    {
        public void Initialize(object instance, MemberInfo member)
        {
            ((InitializerTestClass)instance).Value++;
        }
    }
    #endregion
}