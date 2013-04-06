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

using EventStore.Core.Messaging;

namespace EventStore.Core.Messages
{
    public static class UserManagementMessage
    {

        public class RequestMessage : Message
        {
            public readonly IEnvelope Envelope;

            public RequestMessage(IEnvelope envelope)
            {
                Envelope = envelope;
            }
        }

        public class ResponseMessage : Message
        {
        }

        public class UserManagementRequestMessage : RequestMessage
        {
            public string LoginName;

            protected UserManagementRequestMessage(IEnvelope envelope)
                : base(envelope)
            {
            }
        }

        public class Create : UserManagementRequestMessage
        {
            public readonly string FullName;
            public readonly string Password;

            public Create(IEnvelope envelope, string loginName, string fullName, string password)
                : base(envelope)
            {
                LoginName = loginName;
                FullName = fullName;
                Password = password;
            }
        }

        public enum Error
        {
            Success, NotFound, Conflict,
            Error, TryAgain
        }

        public class UpdateResult : ResponseMessage
        {
            public readonly string _loginName;
            public readonly bool Success;
            public readonly Error Error;


            public UpdateResult(string loginName)
            {
                _loginName = loginName;
                Success = true;
            }

            public UpdateResult(string loginName, Error error)
            {
                Success = false;
                _loginName = loginName;
                Error = error;
            }
        }
    }
}
