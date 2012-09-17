// Copyright (c) 2012, Event Store Ltd
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
// Neither the name of the Event Store Ltd nor the names of its
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
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Transport.Tcp;

namespace EventStore.TestClient.Commands.DvuAdvanced.Workers
{
    public class VerificationWorker
    {
        private readonly Worker _worker;

        public VerificationWorker(string name, ICoordinator coordinator, int maxConcurrentVerifications)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNull(coordinator, "coordinator");

            _worker = new Worker(name, coordinator, WorkerRole.Verifier, maxConcurrentVerifications, OnPackageArrived, Pack);
        }

        public void Start()
        {
            _worker.Start();
        }

        public void Stop()
        {
            _worker.Stop();
        }

        private TcpPackage Pack(Guid correlationId, VerificationEvent evnt)
        {
            var readDto = new ClientMessageDto.ReadEvent(correlationId, evnt.EventStreamId,
                                                         evnt.ShouldBeVersion);

            return new TcpPackage(TcpCommand.ReadEvent, readDto.Serialize());
        }

        private void OnPackageArrived(TcpTypedConnection<byte[]> connection, TcpPackage package)
        {
            if (package.Command != TcpCommand.ReadEventCompleted)
            {
                _worker.SignalWorkerFailed(
                    error: string.Format("Worker {0} received unexpected command. Expected: {1}, received: {2}.",
                                         _worker.Name,
                                         TcpCommand.ReadEventCompleted,
                                         package.Command));
                return;
            }

            var dto = package.Data.Deserialize<ClientMessageDto.ReadEventCompleted>();
            var corrId = new Guid(dto.CorrelationId);

            WorkerItem workItem;
            if (!_worker.TryGetWorkItem(corrId, out workItem))
            {
                _worker.SignalWorkerFailed(
                    error: string.Format("Worker {0} received unexpected CorrId: {1}, no event with such CorrId is in progress.",
                                         _worker.Name,
                                         corrId));
                return;
            }

            var readResult = (SingleReadResult) dto.Result;
            switch (readResult)
            {
                case SingleReadResult.Success:
                    var cmp = workItem.Event.VerifyThat(new Event(workItem.Event.Event.EventId, dto.EventType, false,  dto.Data, dto.Metadata),
                                                        dto.EventNumber);
                    switch (cmp.Status)
                    {
                        case ComparisonStatus.Success:
                            if (_worker.TryRemoveWorkItem(workItem))
                                _worker.NotifyItemProcessed(workItem);
                            break;
                        case ComparisonStatus.Fail:
                            _worker.SignalWorkerFailed(
                                error: string.Format("Worker {0} received invalid event. Details : {1}.",
                                                     _worker,
                                                     cmp.Description));
                            _worker.TryRemoveWorkItem(workItem);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    break;

                case SingleReadResult.NotFound:
                case SingleReadResult.NoStream:
                case SingleReadResult.StreamDeleted:
                    _worker.SignalWorkerFailed(
                        error: string.Format("Worker {0} received unexpected response : {1}.",
                                             _worker.Name,
                                             readResult));
                    _worker.TryRemoveWorkItem(workItem);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
