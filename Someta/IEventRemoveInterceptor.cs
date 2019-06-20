using System;
using System.Reflection;

namespace Someta
{
    public interface IEventRemoveInterceptor : IExtensionPoint
    {
        void RemoveEventHandler(EventInfo eventInfo, object instance, Delegate handler, Action<Delegate> proceed);
    }
}