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
using System.Threading.Tasks;
using EventStore.ClientAPI.Defines;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.Tcp;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.TaskWrappers
{
    public class CreateStreamResult
    {
        public bool IsSuccessful { get; private set; }
        public OperationErrorCode LastErrorCode { get; private set; }

        public CreateStreamResult(OperationErrorCode operationErrorCode)
        {
            IsSuccessful = operationErrorCode == OperationErrorCode.Success;
            LastErrorCode = operationErrorCode;
        }
    }

    class CreateStreamCompletionWrapper : ITaskCompletionWrapper
    {
        private const int MaxRetriesCount = 10;

        private readonly TaskCompletionSource<CreateStreamResult> _completion;

        public CreateStreamCompletionWrapper(TaskCompletionSource<CreateStreamResult> completion)
        {
            _completion = completion;
        }

        public TcpPackage SentPackage { get; set; }
        public int Attempt { get; private set; }

        private ClientMessages.CreateStreamCompleted _resultDto;

        public bool UpdateForNextAttempt()
        {
            _resultDto = null;

            var correlationId = Guid.NewGuid();
            Attempt += 1;
            var package = new TcpPackage(SentPackage.Command, correlationId, SentPackage.Data);
            SentPackage = package;

            return true;
        }

        public ProcessResult Process(TcpPackage package)
        {
            try
            {
                if (package.Command != TcpCommand.CreateStreamCompleted)
                {
                    return new ProcessResult(ProcessResultStatus.NotifyError, 
                                             new Exception(string.Format("Not expected command, expected {0}, received {1}",  
                                                                         TcpCommand.CreateStreamCompleted, package.Command)));
                }

                var data = package.Data;
                var dto = data.Deserialize<ClientMessages.CreateStreamCompleted>();
                _resultDto = dto;

                switch ((OperationErrorCode)dto.ErrorCode)
                {
                    case OperationErrorCode.Success:
                        return new ProcessResult(ProcessResultStatus.Success);

                    case OperationErrorCode.PrepareTimeout:
                    case OperationErrorCode.CommitTimeout:
                    case OperationErrorCode.ForwardTimeout:
                        if (Attempt < MaxRetriesCount)
                        {
                            return new ProcessResult(ProcessResultStatus.Retry);
                        }
                        else
                        {
                            return new ProcessResult(ProcessResultStatus.NotifyError, 
                                                     new Exception(string.Format("Max retries count reached, last error: {0}", 
                                                                  (OperationErrorCode)dto.ErrorCode)));
                        }
                    case OperationErrorCode.WrongExpectedVersion:
                    case OperationErrorCode.StreamDeleted:
                    case OperationErrorCode.InvalidTransaction:
                        return new ProcessResult(ProcessResultStatus.NotifyError, 
                                                 new Exception(string.Format("{0}", (OperationErrorCode)dto.ErrorCode)));
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception ex)
            {
                return new ProcessResult(ProcessResultStatus.NotifyError, ex);
            }
        }

        public void Fail(Exception exception)
        {
            _completion.SetException(exception);
        }

        public void Complete()
        {
            if (_resultDto != null)
            {
                _completion.SetResult(new CreateStreamResult((OperationErrorCode)_resultDto.ErrorCode));
            }
            else
            {
                _completion.SetException(new Exception("Failed to set empty result"));
            }
        }
    }
}