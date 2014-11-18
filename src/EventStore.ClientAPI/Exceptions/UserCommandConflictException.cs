using System;
using System.Runtime.Serialization;

namespace EventStore.ClientAPI.Exceptions
{
    /// <summary>
    /// Exception thrown if a user command fails.
    /// </summary>
    public class UserCommandConflictException : ProjectionCommandFailedException
    {
        /// <summary>
        /// Constructs a new <see cref="UserCommandFailedException"/>.
        /// </summary>
        public UserCommandConflictException()
        {
        }

        /// <summary>
        /// Constructs a new <see cref="UserCommandFailedException"/>.
        /// </summary>
        public UserCommandConflictException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructs a new <see cref="UserCommandFailedException"/>.
        /// </summary>
        public UserCommandConflictException(string message,
                                                  Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Constructs a new <see cref="UserCommandFailedException"/>.
        /// </summary>
        protected UserCommandConflictException(SerializationInfo info,
                                                     StreamingContext context)
            : base(info, context)
        {
        }
    }
}