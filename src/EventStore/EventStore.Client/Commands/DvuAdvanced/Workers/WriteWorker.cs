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
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Transport.Tcp;

namespace EventStore.TestClient.Commands.DvuAdvanced.Workers
{
    public class WriteWorker
    {
        private readonly Worker _worker;

        public WriteWorker(string name, ICoordinator coordinator, int maxconcurrentWrites)
        {
            _worker = new Worker(name, coordinator, WorkerRole.Writer, maxconcurrentWrites, OnPackageArrived, Pack);
        }

        public void Start()
        {
            _worker.Start();
        }

        public void Stop()
        {
            _worker.Stop();
        }

        private TcpPackage Pack(Guid corrId, VerificationEvent evnt)
        {
            var writeDto = new ClientMessageDto.WriteEvents(corrId,
                                                            evnt.EventStreamId,
                                                            evnt.ExpectedVersion,
                                                            new[] {new ClientMessageDto.Event(evnt.Event)});

            return new TcpPackage(TcpCommand.WriteEvents, writeDto.Serialize());
        }

        private void OnPackageArrived(TcpTypedConnection<byte[]> connection, TcpPackage package)
        {
            if (package.Command != TcpCommand.WriteEventsCompleted)
            {
                _worker.SignalWorkerFailed(
                    error: string.Format("Worker {0} received unexpected command. Expected: {1}, received: {2}.",
                                         _worker.Name,
                                         TcpCommand.WriteEventsCompleted,
                                         package.Command));
                return;
            }

            var dto = package.Data.Deserialize<ClientMessageDto.WriteEventsCompleted>();
            var corrId = new Guid(dto.CorrelationId);

            WorkerItem workItem;
            if (!_worker.TryGetWorkItem(corrId, out workItem))
            {
                _worker.SignalWorkerFailed(
                    error:
                        string.Format(
                            "Worker {0} received unexpected CorrId: {1}, no event with such CorrId is in progress.",
                            _worker.Name,
                            corrId));
                return;
            }

            switch ((OperationErrorCode) dto.ErrorCode)
            {
                case OperationErrorCode.Success:
                    {
                        if (_worker.TryRemoveWorkItem(workItem))
                            _worker.NotifyItemProcessed(workItem);
                        break;
                    }
                case OperationErrorCode.PrepareTimeout:
                case OperationErrorCode.CommitTimeout:
                case OperationErrorCode.ForwardTimeout:
                    _worker.Retry(workItem);
                    break;
                case OperationErrorCode.WrongExpectedVersion:
                    if (workItem.Event.ExpectedVersion == ExpectedVersion.Any)
                    {
                        if (workItem.Event.ShouldBeVersion != 1)
                        {
                            _worker.SignalWorkerFailed(
                                error: string.Format("Worker {0} received WrongExpectedVersion for event with "
                                                     + "ExpectedVersion = EventNumber.Any and ShouldBeVersion = 1, "
                                                     + "which shouldn't happen ever!",
                                                     _worker.Name));

                            _worker.TryRemoveWorkItem(workItem);
                            return;
                        }
                    }
                    _worker.Retry(workItem);
                    break;
                case OperationErrorCode.StreamDeleted:
                case OperationErrorCode.InvalidTransaction:
                    _worker.SignalWorkerFailed(
                        error: string.Format("Worker {0} received unexpected OperationErrorCode: {1}.",
                                             _worker.Name,
                                             (OperationErrorCode) dto.ErrorCode));
                    _worker.TryRemoveWorkItem(workItem);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
