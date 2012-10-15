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
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.TestClient.Commands.DvuAdvanced
{
    public abstract class Task
    {
        public abstract TaskType Type { get; }
        public abstract TcpPackage CreateNetworkPackage(Guid correlationId);
        public abstract CheckResult CheckStepExpectations(TcpPackage package);
        public abstract bool MoveToNextStep();
    }

    public enum TaskType
    {
        Write,
        DeleteStream
    }

    public class CheckResult
    {
        public readonly Status Status;
        public readonly string Description;

        public CheckResult(Status status, string description)
        {
            Status = status;
            Description = description;
        }
    }

    public enum Status
    {
        MeetsExpectations,
        Retry,
        CheckStreamDeleted,
        Ignore,
        FailFast
    }

    public class WriteTask : Task
    {
        public override TaskType Type
        {
            get
            {
                return TaskType.Write;
            }
        }

        private byte _step = 0;
        private readonly object _stepMoveLock = new object();

        private readonly VerificationEvent _event;

        public WriteTask(VerificationEvent @event)
        {
            Ensure.NotNull(@event, "event");
            _event = @event;
        }

        public override TcpPackage CreateNetworkPackage(Guid correlationId)
        {
            lock (_stepMoveLock)
            {
                switch (_step)
                {
                    case 0:
                        var writeDto = new ClientMessageDto.WriteEvents(_event.EventStreamId,
                                                                        _event.ExpectedVersion,
                                                                        new[] { new ClientMessageDto.Event(_event.Event) });
                        return new TcpPackage(TcpCommand.WriteEvents, correlationId, writeDto.Serialize());
                    case 1:
                        var readDto = new ClientMessageDto.ReadEvent(_event.EventStreamId, 
                                                                     _event.ShouldBeVersion,
                                                                     resolveLinkTos: false);
                        return new TcpPackage(TcpCommand.ReadEvent, correlationId, readDto.Serialize());
                    default:
                        throw new ArgumentOutOfRangeException("_step", "step can be 0 or 1 for write task");
                }
            }
        }

        public override CheckResult CheckStepExpectations(TcpPackage package)
        {
            lock (_stepMoveLock)
            {
                switch (_step)
                {
                    case 0:
                        return CheckFirstStepExpectations(package);
                    case 1:
                        return CheckSecondStepExpectations(package);
                    default:
                        throw new ArgumentOutOfRangeException("_step", "Can be 0 or 1");
                }
            }
        }

        public override bool MoveToNextStep()
        {
            lock (_stepMoveLock)
            {
                if (_step == 0)
                {
                    _step++;
                    return true;
                }
                return false;
            }
        }

        private CheckResult CheckFirstStepExpectations(TcpPackage package)
        {
            if (package.Command != TcpCommand.WriteEventsCompleted)
                return new CheckResult(Status.FailFast, string.Format("Expected - {0}, received - {1}", 
                                                                      TcpCommand.WriteEventsCompleted, 
                                                                      package.Command));

            var dto = package.Data.Deserialize<ClientMessageDto.WriteEventsCompleted>();

            switch ((OperationErrorCode)dto.ErrorCode)
            {
                case OperationErrorCode.Success:
                        return new CheckResult(Status.MeetsExpectations, "Successfully written");
                case OperationErrorCode.PrepareTimeout:
                case OperationErrorCode.CommitTimeout:
                case OperationErrorCode.ForwardTimeout:
                    return new CheckResult(Status.Retry, "Retrying due to timeout");
                case OperationErrorCode.WrongExpectedVersion:
                    if (_event.ExpectedVersion == ExpectedVersion.Any && _event.ShouldBeVersion != 1)
                        return new CheckResult(Status.FailFast,
                                               string.Format(
                                               "Received wrong expected version while expected == any, should be = {0}",
                                               _event.ShouldBeVersion));
                    return new CheckResult(Status.Retry, "Wrong expected version. Retrying...");
                case OperationErrorCode.StreamDeleted:
                case OperationErrorCode.InvalidTransaction:
                    return new CheckResult(Status.FailFast, string.Format("Unexpected error - {0}", (OperationErrorCode)dto.ErrorCode));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private CheckResult CheckSecondStepExpectations(TcpPackage package)
        {
            if (package.Command != TcpCommand.ReadEventCompleted)
                return new CheckResult(Status.FailFast, 
                                       string.Format("Expected - {0}, received - {1}",
                                                     TcpCommand.ReadEventCompleted,
                                                     package.Command));

            var dto = package.Data.Deserialize<ClientMessageDto.ReadEventCompleted>();

            var readResult = (SingleReadResult)dto.Result;
            switch (readResult)
            {
                case SingleReadResult.Success:
                    var cmp = _event.VerifyThat(new Event(_event.Event.EventId, 
                                                          dto.EventType, 
                                                          false, 
                                                          dto.Data, 
                                                          dto.Metadata),
                                                dto.EventNumber);
                    switch (cmp.Status)
                    {
                        case ComparisonStatus.Success:
                            return new CheckResult(Status.MeetsExpectations, "Comparison went smoothly");
                        case ComparisonStatus.Fail:
                            return new CheckResult(Status.FailFast, cmp.Description);
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                case SingleReadResult.NotFound:
                case SingleReadResult.NoStream:
                    return new CheckResult(Status.FailFast, string.Format("Unexpected error - {0}", (SingleReadResult)dto.Result));
                case SingleReadResult.StreamDeleted:
                    return new CheckResult(Status.CheckStreamDeleted, dto.EventStreamId);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override string ToString()
        {
            return string.Format("Event: stream {0} - expected {1} - should be {2} - id - {3} - step - {4}",
                                 _event.EventStreamId,
                                 _event.ExpectedVersion,
                                 _event.ShouldBeVersion,
                                 _event.Event.EventId,
                                 _step);
        }
    }

    public class DeleteStreamTask : Task
    {
        public override TaskType Type
        {
            get
            {
                return TaskType.DeleteStream;
            }
        }

        private byte _step = 0;

        public readonly string EventStreamId;
        public readonly int ExpectedVersion;

        public DeleteStreamTask(string eventStreamId, int expectedVersion)
        {
            Ensure.NotNull(eventStreamId, "eventStreamId");

            EventStreamId = eventStreamId;
            ExpectedVersion = expectedVersion;
        }

        public override TcpPackage CreateNetworkPackage(Guid correlationId)
        {
            var dto = new ClientMessageDto.DeleteStream(EventStreamId, ExpectedVersion);
            return new TcpPackage(TcpCommand.DeleteStream, correlationId, dto.Serialize());
        }

        public override CheckResult CheckStepExpectations(TcpPackage package)
        {
            switch (_step)
            {
                case 0:
                    return CheckFirstStepExpectations(package);
                default:
                    throw new ArgumentOutOfRangeException("_step", "Can be 0");
            }
        }

        public override bool MoveToNextStep()
        {
            return false;
        }

        private CheckResult CheckFirstStepExpectations(TcpPackage package)
        {
            if (package.Command != TcpCommand.DeleteStreamCompleted)
                return new CheckResult(Status.FailFast,
                                       string.Format("Expected - {0}, received - {1}",
                                                     TcpCommand.DeleteStreamCompleted,
                                                     package.Command));

            var dto = package.Data.Deserialize<ClientMessageDto.DeleteStreamCompleted>();

            var code = (OperationErrorCode)dto.ErrorCode;
            switch (code)
            {
                case OperationErrorCode.Success:
                    return new CheckResult(Status.MeetsExpectations, "STREAM DELETED");
                case OperationErrorCode.PrepareTimeout:
                case OperationErrorCode.CommitTimeout:
                case OperationErrorCode.ForwardTimeout:
                    return new CheckResult(Status.Retry, "Deletion of streams failed due to timeout. Retrying...");
                case OperationErrorCode.WrongExpectedVersion:
                    return new CheckResult(Status.Retry, "Stream cannot be deleted. WrongExpectedVersion. Retrying...");
                case OperationErrorCode.StreamDeleted:
                case OperationErrorCode.InvalidTransaction:
                    return new CheckResult(Status.FailFast, string.Format("Unexpected error on delete - {0}", code));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override string ToString()
        {
            return string.Format("EventStreamId : {0}, ExpectedVersion : {1}", EventStreamId, ExpectedVersion);
        }
    }
}
