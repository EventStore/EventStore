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
//  

using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.Core
{
    internal abstract class Message
    {
    }

    internal class TimerTickMessage: Message
    {
    }

    internal class StartConnectionMessage : Message
    {
        public readonly TaskCompletionSource<object> Task;
        public readonly IEndPointDiscoverer EndPointDiscoverer;

        public StartConnectionMessage(TaskCompletionSource<object> task, IEndPointDiscoverer endPointDiscoverer)
        {
            Ensure.NotNull(task, "task");
            Ensure.NotNull(endPointDiscoverer, "endendPointDiscoverer");
            
            Task = task;
            EndPointDiscoverer = endPointDiscoverer;
        }
    }

    internal class CloseConnectionMessage: Message
    {
        public readonly string Reason;
        public readonly Exception Exception;

        public CloseConnectionMessage(string reason, Exception exception)
        {
            Reason = reason;
            Exception = exception;
        }
    }

    internal class EstablishTcpConnectionMessage: Message
    {
        public readonly NodeEndPoints EndPoints;

        public EstablishTcpConnectionMessage(NodeEndPoints endPoints)
        {
            EndPoints = endPoints;
        }
    }

    internal class TcpConnectionEstablishedMessage : Message
    {
        public readonly TcpPackageConnection Connection;

        public TcpConnectionEstablishedMessage(TcpPackageConnection connection)
        {
            Ensure.NotNull(connection, "connection");
            Connection = connection;
        }
    }

    internal class TcpConnectionClosedMessage : Message
    {
        public readonly TcpPackageConnection Connection;
        public readonly SocketError Error;

        public TcpConnectionClosedMessage(TcpPackageConnection connection, SocketError error)
        {
            Ensure.NotNull(connection, "connection");
            Connection = connection;
            Error = error;
        }
    }

    internal class StartOperationMessage: Message
    {
        public readonly IClientOperation Operation;
        public readonly int MaxRetries;
        public readonly TimeSpan Timeout;

        public StartOperationMessage(IClientOperation operation, int maxRetries, TimeSpan timeout)
        {
            Ensure.NotNull(operation, "operation");
            Operation = operation;
            MaxRetries = maxRetries;
            Timeout = timeout;
        }
    }

    internal class StartSubscriptionMessage : Message
    {
        public readonly TaskCompletionSource<EventStoreSubscription> Source;

        public readonly string StreamId;
        public readonly bool ResolveLinkTos;
        public readonly UserCredentials UserCredentials;
        public readonly Action<EventStoreSubscription, ResolvedEvent> EventAppeared;
        public readonly Action<EventStoreSubscription, SubscriptionDropReason, Exception> SubscriptionDropped;
           
        public readonly int MaxRetries;
        public readonly TimeSpan Timeout;

        public StartSubscriptionMessage(TaskCompletionSource<EventStoreSubscription> source,
                                        string streamId,
                                        bool resolveLinkTos, 
                                        UserCredentials userCredentials,
                                        Action<EventStoreSubscription, ResolvedEvent> eventAppeared, 
                                        Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped, 
                                        int maxRetries, 
                                        TimeSpan timeout)
        {
            Ensure.NotNull(source, "source");
            Ensure.NotNull(eventAppeared, "eventAppeared");

            Source = source;
            StreamId = streamId;
            ResolveLinkTos = resolveLinkTos;
            UserCredentials = userCredentials;
            EventAppeared = eventAppeared;
            SubscriptionDropped = subscriptionDropped;
            MaxRetries = maxRetries;
            Timeout = timeout;
        }
    }
    
    internal class StartPersistentSubscriptionMessage : Message
    {
        public readonly TaskCompletionSource<PersistentEventStoreSubscription> Source;

        public readonly string SubscriptionId;
        public readonly string StreamId;
        public readonly bool ResolveLinkTos;
        public readonly int BufferSize;
        public readonly UserCredentials UserCredentials;
        public readonly Action<PersistentEventStoreSubscription, ResolvedEvent> EventAppeared;
        public readonly Action<PersistentEventStoreSubscription, SubscriptionDropReason, Exception> SubscriptionDropped;
           
        public readonly int MaxRetries;
        public readonly TimeSpan Timeout;

        public StartPersistentSubscriptionMessage(TaskCompletionSource<PersistentEventStoreSubscription> source, string subscriptionId, string streamId, bool resolveLinkTos, int bufferSize, UserCredentials userCredentials, Action<PersistentEventStoreSubscription, ResolvedEvent> eventAppeared, Action<PersistentEventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped, int maxRetries, TimeSpan timeout)
        {
            Ensure.NotNull(source, "source");
            Ensure.NotNull(eventAppeared, "eventAppeared");
            Ensure.NotNullOrEmpty(subscriptionId, "subscriptionId");
            Ensure.Nonnegative(bufferSize, "bufferSize");

            SubscriptionId = subscriptionId;
            BufferSize = bufferSize;
            Source = source;
            StreamId = streamId;
            ResolveLinkTos = resolveLinkTos;
            UserCredentials = userCredentials;
            EventAppeared = eventAppeared;
            SubscriptionDropped = subscriptionDropped;
            MaxRetries = maxRetries;
            Timeout = timeout;
        }
    }

    internal class NotifyEventsProcessed : Message
    {
        public readonly Guid SubscriptionId;
        public readonly int FreeSlots;
        public readonly Guid[] ProcessedEventIds;

        public NotifyEventsProcessed(Guid subscriptionId, int freeSlots, Guid[] processedEventIds)
        {
            Ensure.NotEmptyGuid(subscriptionId, "subscriptionId");
            Ensure.Nonnegative(freeSlots, "freeSlots");
            Ensure.NotNull(processedEventIds, "processedEventIds");

            SubscriptionId = subscriptionId;
            FreeSlots = freeSlots;
            ProcessedEventIds = processedEventIds;
        }
    }

    internal class HandleTcpPackageMessage: Message
    {
        public readonly TcpPackageConnection Connection;
        public readonly TcpPackage Package;

        public HandleTcpPackageMessage(TcpPackageConnection connection, TcpPackage package)
        {
            Connection = connection;
            Package = package;
        }
    }

    internal class TcpConnectionErrorMessage : Message
    {
        public readonly TcpPackageConnection Connection;
        public readonly Exception Exception;

        public TcpConnectionErrorMessage(TcpPackageConnection connection, Exception exception)
        {
            Connection = connection;
            Exception = exception;
        }
    }
}