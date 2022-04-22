using NUnit.Framework;
using System.Reflection;

namespace Someta.Docs.Samples;

[TestFixture]
public class EventInterceptorExample
{
#pragma warning disable CS0067

    [Test]
    #region EventInterceptorExample
    public void EventExample()
    {
        var handler = () => {};
        var testClass = new EventTestClass();
        testClass.TestEvent += handler;
        Console.WriteLine(testClass.TestEventHandlers.Count);       // Prints 1
        testClass.TestEvent -= handler;
        Console.WriteLine(testClass.TestEventHandlers.Count);       // Prints 0
    }

    [EventInterceptor]
    class EventTestClass
    {
        public event Action? TestEvent;

        public List<Action> TestEventHandlers = new();
    }

    [AttributeUsage(AttributeTargets.Class)]
    class EventInterceptor : Attribute, IEventAddInterceptor, IEventRemoveInterceptor
    {
        public void AddEventHandler(EventInfo eventInfo, object instance, Delegate handler, Action<Delegate> proceed)
        {
            ((EventTestClass)instance).TestEventHandlers.Add((Action)handler);
        }

        public void RemoveEventHandler(EventInfo eventInfo, object instance, Delegate handler, Action<Delegate> proceed)
        {
            ((EventTestClass)instance).TestEventHandlers.Remove((Action)handler);
        }
    }
    #endregion

#pragma warning restore CS0067
}