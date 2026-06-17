using System;
using System.Collections.Generic;

namespace YoonseulFishing.Core
{
    /// <summary>
    /// Lightweight stand-in for Kotlin's <c>StateFlow</c>: holds a value and
    /// raises <see cref="Changed"/> whenever it is assigned a *different* value
    /// (distinct-until-changed semantics, matching StateFlow).
    ///
    /// UI/observers subscribe to <see cref="Changed"/>; game logic writes to
    /// <see cref="Value"/>. Unity game logic runs single-threaded on the main
    /// thread, so no synchronization is needed (unlike the Android coroutine
    /// version, which used MutableStateFlow for thread-safety).
    /// </summary>
    public class Observable<T>
    {
        private T _value;

        /// <summary>Raised with the new value after it changes.</summary>
        public event Action<T> Changed;

        public Observable(T initial = default)
        {
            _value = initial;
        }

        public T Value
        {
            get => _value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_value, value)) return;
                _value = value;
                Changed?.Invoke(_value);
            }
        }

        /// <summary>
        /// Assigns and always notifies, even if the value compares equal. Use for
        /// reference types whose *contents* mutated in place (e.g. a list edited
        /// rather than replaced).
        /// </summary>
        public void SetAndForceNotify(T value)
        {
            _value = value;
            Changed?.Invoke(_value);
        }

        public override string ToString() => _value?.ToString() ?? "null";
    }
}
