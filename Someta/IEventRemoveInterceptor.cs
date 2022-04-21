using System;
using System.Reflection;

namespace Someta
{
    /// <summary>
    /// Have your extension point implement this interface to be able to intercept the removing of an event
    /// handler. You can call the original implementation of the remove method via the provided proceed delegate.
    /// </summary>
    public interface IEventRemoveInterceptor : IExtensionPoint
    {
        /// <summary>
        /// Called when the caller removes an event handler from an event decorated with this extension point.
        /// </summary>
        /// <param name="eventInfo">The EventInfo that represents the event that this extension was applied to.</param>
        /// <param name="instance">The instance on which the event was removed (or null for static methods).</param>
        /// <param name="handler">The event handler being removed.</param>
        /// <param name="proceed">A delegate you can call to invoke the original remove function.</param>
        #region EventRemoveInterceptor
        void RemoveEventHandler(EventInfo eventInfo, object instance, Delegate handler, Action<Delegate> proceed);
        #endregion
    }
}