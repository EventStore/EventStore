using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Messaging;

namespace EventStore.ClientAPI.Embedded
{
    internal interface IEmbeddedResponder
    {
        void InspectMessage(Message message);
        void NotAuthenticated();
        void ServerError();
        void NotReady();
    }

    internal abstract class EmbeddedResponderBase<TResult, TResponse> : IEmbeddedResponder where TResponse : Message
    {
        private readonly TaskCompletionSource<TResult> _source;
        private int _completed;

        protected EmbeddedResponderBase(TaskCompletionSource<TResult> source)
        {
            _source = source;
        }

        public void InspectMessage(Message message)
        {
            try
            {
                Ensure.NotNull(message, "message");

                var response = message as TResponse;

                if (response != null)
                    InspectResponse(response);
                else
                    Fail(new NoResultException(String.Format("Expected response of {0}, received {1} instead.", typeof(TResponse), message.GetType())));

            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        public void NotAuthenticated()
        {
            Fail(new NotAuthenticatedException());
        }

        public void ServerError()
        {
            Fail(new ServerErrorException());
        }

        public void NotReady()
        {
            Fail(new ServerErrorException("The server is not ready."));
        }

        protected abstract void InspectResponse(TResponse response);

        protected abstract TResult TransformResponse(TResponse response);

        protected void Succeed(TResponse response)
        {
            if (Interlocked.CompareExchange(ref _completed, 1, 0) == 0)
            {
                _source.SetResult(TransformResponse(response));
            }
        }

        protected void Fail(Exception exception)
        {
            if (Interlocked.CompareExchange(ref _completed, 1, 0) == 0)
            {
                _source.SetException(exception);
            }
        }
    }
}