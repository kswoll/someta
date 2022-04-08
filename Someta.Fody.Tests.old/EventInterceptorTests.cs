using System;
using System.Reflection;
using NUnit.Framework;
using Shouldly;

namespace Someta.Fody.Tests
{
    [TestFixture]
    public class EventInterceptorTests
    {
        [Test]
        public void AddEvent()
        {
            var o = new EventAddTestClass();
            bool added = false;
            o.TestEvent += () => added = true;
            o.Fire();

            added.ShouldBeTrue();
            o.Log.ShouldBe("Added");
        }

        [Test]
        public void RemoveEvent()
        {
            var o = new EventRemoveTestClass();
            bool removed = false;

            void Handler()
            {
                removed = true;
            }

            o.TestEvent += Handler;
            o.Fire();

            removed.ShouldBeTrue();

            removed = false;
            o.TestEvent -= Handler;
            o.Fire();

            o.Log.ShouldBe("Removed");
            removed.ShouldBeTrue();
        }

        [EventAdd]
        public class EventAddTestClass
        {
            public string Log { get; set; }

            public event Action TestEvent;

            public void Fire()
            {
                TestEvent?.Invoke();
            }
        }

        [EventRemove]
        public class EventRemoveTestClass
        {
            public string Log { get; set; }

            public event Action TestEvent;

            public void Fire()
            {
                TestEvent?.Invoke();
            }
        }

        public class EventAddAttribute : Attribute, IEventAddInterceptor
        {
            public void AddEventHandler(EventInfo eventInfo, object instance, Delegate handler, Action<Delegate> proceed)
            {
                ((EventAddTestClass)instance).Log += "Added";
                proceed(handler);
            }
        }

        public class EventRemoveAttribute : Attribute, IEventRemoveInterceptor
        {
            public void RemoveEventHandler(EventInfo eventInfo, object instance, Delegate handler, Action<Delegate> proceed)
            {
                ((EventRemoveTestClass)instance).Log += "Removed";
            }
        }
    }
}