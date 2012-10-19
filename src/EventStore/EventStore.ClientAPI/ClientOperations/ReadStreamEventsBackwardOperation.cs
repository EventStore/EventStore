using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.ClientOperations
{
    internal class ReadStreamEventsBackwardOperation : IClientOperation
    {
        private readonly TaskCompletionSource<EventStreamSlice> _source;
        private ClientMessages.ReadStreamEventsBackwardCompleted _result;
        private int _completed;

        private Guid _correlationId;
        private readonly object _corrIdLock = new object();

        private readonly string _stream;
        private readonly int _start;
        private readonly int _count;
        private readonly bool _resolveLinkTos;

        public Guid CorrelationId
        {
            get
            {
                lock (_corrIdLock)
                    return _correlationId;
            }
        }

        public ReadStreamEventsBackwardOperation(TaskCompletionSource<EventStreamSlice> source,
                                                 Guid corrId,
                                                 string stream,
                                                 int start,
                                                 int count,
                                                 bool resolveLinkTos)
        {
            _source = source;

            _correlationId = corrId;
            _stream = stream;
            _start = start;
            _count = count;
            _resolveLinkTos = resolveLinkTos;
        }

        public void SetRetryId(Guid correlationId)
        {
            lock (_corrIdLock)
                _correlationId = correlationId;
        }

        public TcpPackage CreateNetworkPackage()
        {
            lock (_corrIdLock)
            {
                var dto = new ClientMessages.ReadStreamEventsBackward(_stream, _start, _count, _resolveLinkTos);
                return new TcpPackage(TcpCommand.ReadStreamEventsBackward, _correlationId, dto.Serialize());
            }
        }

        public InspectionResult InspectPackage(TcpPackage package)
        {
            try
            {
                if (package.Command != TcpCommand.ReadStreamEventsBackwardCompleted)
                {
                    return new InspectionResult(InspectionDecision.NotifyError,
                                                new CommandNotExpectedException(TcpCommand.ReadStreamEventsBackwardCompleted.ToString(),
                                                                                package.Command.ToString()));
                }

                var data = package.Data;
                var dto = data.Deserialize<ClientMessages.ReadStreamEventsBackwardCompleted>();
                _result = dto;

                switch ((RangeReadResult)dto.Result)
                {
                    case RangeReadResult.Success:
                        return new InspectionResult(InspectionDecision.Succeed);
                    case RangeReadResult.StreamDeleted:
                        return new InspectionResult(InspectionDecision.NotifyError, new StreamDeletedException(_stream));
                    case RangeReadResult.NoStream:
                        return new InspectionResult(InspectionDecision.NotifyError, new StreamDoesNotExistException());
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception e)
            {
                return new InspectionResult(InspectionDecision.NotifyError, e);
            }
        }

        public void Complete()
        {
            if (Interlocked.CompareExchange(ref _completed, 1, 0) == 0)
            {
                if (_result != null)
                    _source.SetResult(new EventStreamSlice(_stream, _start, _count, _result.Events));
                else
                    _source.SetException(new NoResultException());
            }
        }

        public void Fail(Exception exception)
        {
            if (Interlocked.CompareExchange(ref _completed, 1, 0) == 0)
            {
                _source.SetException(exception);
            }
        }

        public override string ToString()
        {
            return string.Format("Stream: {0}, Start: {1}, Count: {2}, ResolveLinkTos: {3}, CorrelationId: {4}", 
                                 _stream,
                                 _start, 
                                 _count, 
                                 _resolveLinkTos, 
                                 CorrelationId);
        }
    }
}