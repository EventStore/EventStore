namespace EventStore.ClientAPI
{
    /// <summary>
    /// Represents the reason subscription drop happened
    /// </summary>
    public enum SubscriptionDropReason
    {
        /// <summary>
        /// Subscription dropped because the client called Close.
        /// </summary>
        UserInitiated,
        /// <summary>
        /// Subscription dropped because the client is not authenticated.
        /// </summary>
        NotAuthenticated,
        /// <summary>
        /// Subscription dropped because access to the stream was denied.
        /// </summary>
        AccessDenied,
        /// <summary>
        /// Subscription dropped because of an error in the subscription phase.
        /// </summary>
        SubscribingError,
        /// <summary>
        /// Subscription dropped because of a server error.
        /// </summary>
        ServerError,
        /// <summary>
        /// Subscription dropped because the connection was closed.
        /// </summary>
        ConnectionClosed,

        /// <summary>
        /// Subscription dropped because of an error during the catch-up phase.
        /// </summary>
        CatchUpError,
        /// <summary>
        /// Subscription dropped because it's queue overflowed.
        /// </summary>
        ProcessingQueueOverflow,
        /// <summary>
        /// Subscription dropped because an exception was thrown by a handler.
        /// </summary>
        EventHandlerException,

        /// <summary>
        /// Subscription was dropped for an unknown reason.
        /// </summary>
        Unknown = 100,

        /// <summary>
        /// Target of persistent subscription was not found. Needs to be created first
        /// </summary>
        NotFound
    }
}