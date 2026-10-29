using System;
using System.Collections.Generic;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// Typed game events with struct payloads (e.g. VisitorHealed, StageReached). Publishing allocates nothing.
    /// Subscribe in OnEnable and unsubscribe in OnDisable; a forgotten unsubscribe keeps a destroyed object alive.
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.TryGetValue(typeof(T), out Delegate existing);
            _handlers[typeof(T)] = Delegate.Combine(existing, handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null || !_handlers.TryGetValue(typeof(T), out Delegate existing)) return;

            Delegate remaining = Delegate.Remove(existing, handler);
            if (remaining == null) _handlers.Remove(typeof(T));
            else _handlers[typeof(T)] = remaining;
        }

        public void Publish<T>(T evt) where T : struct
        {
            if (_handlers.TryGetValue(typeof(T), out Delegate handlers))
                ((Action<T>)handlers).Invoke(evt);
        }

        public int SubscriberCount<T>() where T : struct =>
            _handlers.TryGetValue(typeof(T), out Delegate handlers) ? handlers.GetInvocationList().Length : 0;
    }
}
