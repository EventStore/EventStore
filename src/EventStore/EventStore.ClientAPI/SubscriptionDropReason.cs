// Copyright (c) 2012, Event Store LLP
// All rights reserved.
//  
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//  
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

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
        Unknown = 100
    }
}