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
            public readonly bool Success;
            public readonly Error Error;

            public ResponseMessage(bool success, Error error)
            {
                Success = success;
                Error = error;
            }
        }

        public class UserManagementRequestMessage : RequestMessage
        {
            public readonly string LoginName;

            protected UserManagementRequestMessage(IEnvelope envelope, string loginName)
                : base(envelope)
            {
                LoginName = loginName;
            }
        }

        public sealed class Create : UserManagementRequestMessage
        {
            public readonly string FullName;
            public readonly string Password;

            public Create(IEnvelope envelope, string loginName, string fullName, string password)
                : base(envelope, loginName)
            {
                FullName = fullName;
                Password = password;
            }
        }

        public sealed class Update : UserManagementRequestMessage
        {
            public readonly string FullName;

            public Update(IEnvelope envelope, string loginName, string fullName)
                : base(envelope, loginName)
            {
                FullName = fullName;
            }
        }

        public sealed class Disable : UserManagementRequestMessage
        {
            public Disable(IEnvelope envelope, string loginName)
                : base(envelope, loginName)
            {
            }
        }

        public sealed class Enable : UserManagementRequestMessage
        {
            public Enable(IEnvelope envelope, string loginName)
                : base(envelope, loginName)
            {
            }
        }

        public sealed class Delete : UserManagementRequestMessage
        {
            public Delete(IEnvelope envelope, string loginName)
                : base(envelope, loginName)
            {
            }
        }

        public sealed class ResetPassword : UserManagementRequestMessage
        {
            public readonly string NewPassword;

            public ResetPassword(IEnvelope envelope, string loginName, string newPassword)
                : base(envelope, loginName)
            {
                NewPassword = newPassword;
            }
        }

        public sealed class ChangePassword : UserManagementRequestMessage
        {
            public readonly string CurrentPassword;
            public readonly string NewPassword;

            public ChangePassword(IEnvelope envelope, string loginName, string currentPassword, string newPassword)
                : base(envelope, loginName)
            {
                CurrentPassword = currentPassword;
                NewPassword = newPassword;
            }
        }

        public sealed class GetAll : RequestMessage
        {
            public GetAll(IEnvelope envelope)
                : base(envelope)
            {
            }
        }

        public sealed class Get : UserManagementRequestMessage
        {
            public Get(IEnvelope envelope, string loginName)
                : base(envelope, loginName)
            {
            }
        }
        public enum Error
        {
            Success, NotFound, Conflict,
            Error, TryAgain,
            Unauthorized
        }

        public sealed class UserData
        {
            public readonly string LoginName;
            public readonly string FullName;
            public readonly DateTimeOffset? DateLastUpdated;
            public readonly bool Disabled;

            public UserData(string loginName, string fullName, bool disabled, DateTimeOffset? dateLastUpdated)
            {
                LoginName = loginName;
                FullName = fullName;
                Disabled = disabled;
                DateLastUpdated = dateLastUpdated;
            }

            internal UserData()
            {
            }
        }

        public sealed class UpdateResult : ResponseMessage
        {
            public readonly string LoginName;


            public UpdateResult(string loginName)
                : base (true, Error.Success)
            {
                LoginName = loginName;
            }

            public UpdateResult(string loginName, Error error)
                : base (false, error)
            {
                LoginName = loginName;
            }
        }

        public sealed class UserDetailsResult : ResponseMessage
        {
            public readonly UserData Data;

            public UserDetailsResult(UserData data)
                : base(true, Error.Success)
            {
                Data = data;
            }

            public UserDetailsResult(Error error)
                : base(false, error)
            {
                Data = null;
            }
        }

        public sealed class AllUserDetailsResult : ResponseMessage
        {
            public readonly UserData[] Data;

            internal AllUserDetailsResult()
                : base(true, Error.Success)
            {
            }

            public AllUserDetailsResult(UserData[] data)
                : base(true, Error.Success)
            {
                Data = data;
            }

            public AllUserDetailsResult(Error error)
                : base(false, error)
            {
                Data = null;
            }
        }
    }
}
