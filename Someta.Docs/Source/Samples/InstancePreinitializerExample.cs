using NUnit.Framework;
using System.Reflection;

namespace Someta.Docs.Source.Samples;

[TestFixture]
public class InstancePreinitializerExample
{
    [Test]
    #region InstancePreinitializerExample
    public void PreinitializerExample()
    {
        var testClass = new PreinitializerTestClass();
        Console.WriteLine(testClass.ValueAsSeenInConstructor);         // Prints 1
    }

    [PreinitializerExtensionPoint]
    class PreinitializerTestClass
    {
        public int Value { get; set; }
        public int ValueAsSeenInConstructor { get; set; }

        public PreinitializerTestClass()
        {
            ValueAsSeenInConstructor = Value;
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    class PreinitializerExtensionPoint : Attribute, IInstancePreinitializer
    {
        public void Preinitialize(object instance, MemberInfo member)
        {
            ((PreinitializerTestClass)instance).Value = 1;
        }
    }
    #endregion
}