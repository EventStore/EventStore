using System;
using System.Runtime.Serialization;

namespace EventStore.ClientAPI.Exceptions
{
    /// <summary>
    /// Base type for exceptions thrown by an <see cref="IEventStoreConnection"/>,
    /// thrown in circumstances which do not have a specific derived exception.
    /// </summary>
    public class EventStoreConnectionException : Exception
    {
        /// <summary>
        /// Constructs a new <see cref="EventStoreConnectionException"/>.
        /// </summary>
        public EventStoreConnectionException()
        {
        }

        /// <summary>
        /// Constructs a new <see cref="EventStoreConnectionException"/>.
        /// </summary>
        public EventStoreConnectionException(string message): base(message)
        {
        }

        /// <summary>
        /// Constructs a new <see cref="EventStoreConnectionException"/>.
        /// </summary>
        public EventStoreConnectionException(string message, Exception innerException): base(message, innerException)
        {
        }

#if EVENTSTORE_CLIENT_NO_EXCEPTION_SERIALIZATION
#else
        /// <summary>
        /// Constructs a new <see cref="EventStoreConnectionException"/>.
        /// </summary>
        protected EventStoreConnectionException(SerializationInfo info, StreamingContext context): base(info, context)
        {
        }
#endif
    }
}
