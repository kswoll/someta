using System;
using System.Reflection;

namespace Someta
{
    public interface IEventAddInterceptor : IExtensionPoint
    {
        void AddEventHandler(EventInfo eventInfo, object instance, Delegate handler, Action<Delegate> proceed);
    }
}