using NUnit.Framework;

namespace Someta.Fody.Tests
{
    [TestFixture]
    public class InstanceInitializerTests
    {

        public class TestClass
        {
            public object field = new object();

            public TestClass()
            {
            }

            public TestClass(object _)
            {
            }

            public TestClass(object _, object __) : this()
            {
            }
        }
    }
}