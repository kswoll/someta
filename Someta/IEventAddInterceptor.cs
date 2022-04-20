using System;
using System.Reflection;

namespace Someta
{
    /// <summary>
    /// Have your extension point implement this interface to be able to intercept the adding of an event
    /// handler. You can call the original implementation of the add method via the provided proceed delegate.
    /// </summary>
    public interface IEventAddInterceptor : IExtensionPoint
    {
        /// <summary>
        /// Called when the caller adds an event handler to an event decorated with this extension point.
        /// </summary>
        /// <param name="eventInfo">The EventInfo that represents the event that this extension was applied to.</param>
        /// <param name="instance">The instance on which the event was added (or null for static methods).</param>
        /// <param name="handler">The event handler being added.</param>
        /// <param name="proceed">A delegate you can call to invoke the original add function.</param>
        void AddEventHandler(EventInfo eventInfo, object instance, Delegate handler, Action<Delegate> proceed);
    }
}