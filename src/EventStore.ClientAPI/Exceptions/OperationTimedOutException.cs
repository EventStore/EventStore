using System;
using System.Runtime.Serialization;

namespace EventStore.ClientAPI.Exceptions
{
    /// <summary>
    /// Exception thrown if an operation times out.
    /// </summary>
    public class OperationTimedOutException : EventStoreConnectionException
    {
        /// <summary>
        /// Constructs a new <see cref="OperationTimedOutException"/>.
        /// </summary>
        public OperationTimedOutException()
        {
        }

        /// <summary>
        /// Constructs a new <see cref="OperationTimedOutException"/>.
        /// </summary>
        public OperationTimedOutException(string message) : base(message)
        {
        }

        /// <summary>
        /// Constructs a new <see cref="OperationTimedOutException"/>.
        /// </summary>
        public OperationTimedOutException(string message, Exception innerException) : base(message, innerException)
        {
        }

#if EVENTSTORE_CLIENT_NO_EXCEPTION_SERIALIZATION
#else
        /// <summary>
        /// Constructs a new <see cref="OperationTimedOutException"/>.
        /// </summary>
        protected OperationTimedOutException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#endif
    }
}
