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
        public readonly int BufferSize;
        public readonly UserCredentials UserCredentials;
        public readonly Action<PersistentEventStoreSubscription, ResolvedEvent> EventAppeared;
        public readonly Action<PersistentEventStoreSubscription, SubscriptionDropReason, Exception> SubscriptionDropped;
           
        public readonly int MaxRetries;
        public readonly TimeSpan Timeout;

        public StartPersistentSubscriptionMessage(TaskCompletionSource<PersistentEventStoreSubscription> source, string subscriptionId, string streamId, int bufferSize, UserCredentials userCredentials, Action<PersistentEventStoreSubscription, ResolvedEvent> eventAppeared, Action<PersistentEventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped, int maxRetries, TimeSpan timeout)
        {
            Ensure.NotNull(source, "source");
            Ensure.NotNull(eventAppeared, "eventAppeared");
            Ensure.NotNullOrEmpty(subscriptionId, "subscriptionId");
            Ensure.Nonnegative(bufferSize, "bufferSize");

            SubscriptionId = subscriptionId;
            BufferSize = bufferSize;
            Source = source;
            StreamId = streamId;
            UserCredentials = userCredentials;
            EventAppeared = eventAppeared;
            SubscriptionDropped = subscriptionDropped;
            MaxRetries = maxRetries;
            Timeout = timeout;
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