using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.ClientOperations
{
    internal class ReadAllEventsBackwardOperation : IClientOperation
    {
        private readonly TaskCompletionSource<AllEventsSlice> _source;
        private ClientMessages.ReadAllEventsBackwardCompleted _result;
        private int _completed;

        private Guid _corrId;
        private readonly object _corrIdLock = new object();

        private readonly Position _position;
        private readonly int _maxCount;
        private readonly bool _resolveLinkTos;

        public Guid CorrelationId
        {
            get
            {
                lock (_corrIdLock)
                    return _corrId;
            }
        }

        public ReadAllEventsBackwardOperation(TaskCompletionSource<AllEventsSlice> source,
                                              Guid corrId,
                                              Position position,
                                              int maxCount,
                                              bool resolveLinkTos)
        {
            _source = source;

            _corrId = corrId;
            _position = position;
            _maxCount = maxCount;
            _resolveLinkTos = resolveLinkTos;
        }

        public void SetRetryId(Guid correlationId)
        {
            lock (_corrIdLock)
                _corrId = correlationId;
        }

        public TcpPackage CreateNetworkPackage()
        {
            lock (_corrIdLock)
            {
                var dto = new ClientMessages.ReadAllEventsBackward(_position.CommitPosition,
                                                                   _position.PreparePosition,
                                                                   _maxCount,
                                                                   _resolveLinkTos);
                return new TcpPackage(TcpCommand.ReadAllEventsBackward, _corrId, dto.Serialize());
            }
        }

        public InspectionResult InspectPackage(TcpPackage package)
        {
            try
            {
                if (package.Command != TcpCommand.ReadAllEventsBackwardCompleted)
                {
                    return new InspectionResult(InspectionDecision.NotifyError,
                                                new CommandNotExpectedException(TcpCommand.ReadAllEventsBackwardCompleted.ToString(),
                                                                                package.Command.ToString()));
                }

                var data = package.Data;
                var dto = data.Deserialize<ClientMessages.ReadAllEventsBackwardCompleted>();
                _result = dto;
                return new InspectionResult(InspectionDecision.Succeed);
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
                    _source.SetResult(new AllEventsSlice(new Position(_result.NextCommitPosition, _result.NextPreparePosition), _result.Events));
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
            return string.Format("Position: {0}, MaxCount: {1}, ResolveLinkTos: {2}, CorrelationId: {3}", 
                                 _position,
                                 _maxCount, 
                                 _resolveLinkTos, 
                                 CorrelationId);
        }
    }
}