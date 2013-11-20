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
using System.Security.Principal;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages
{
    public static class UserManagementMessage
    {
        public class RequestMessage : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly IEnvelope Envelope;
            public readonly IPrincipal Principal;

            public RequestMessage(IEnvelope envelope, IPrincipal principal)
            {
                Envelope = envelope;
                Principal = principal;
            }
        }

        public class ResponseMessage : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

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
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly string LoginName;

            protected UserManagementRequestMessage(IEnvelope envelope, IPrincipal principal, string loginName)
                : base(envelope, principal)
            {
                LoginName = loginName;
            }
        }

        public sealed class Create : UserManagementRequestMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly string FullName;
            public readonly string[] Groups;
            public readonly string Password;

            public Create(
                IEnvelope envelope, IPrincipal principal, string loginName, string fullName, string[] groups,
                string password)
                : base(envelope, principal, loginName)
            {
                FullName = fullName;
                Groups = groups;
                Password = password;
            }
        }

        public sealed class Update : UserManagementRequestMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly string FullName;
            public readonly string[] Groups;

            public Update(IEnvelope envelope, IPrincipal principal, string loginName, string fullName, string[] groups)
                : base(envelope, principal, loginName)
            {
                FullName = fullName;
                Groups = groups;
            }
        }

        public sealed class Disable : UserManagementRequestMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public Disable(IEnvelope envelope, IPrincipal principal, string loginName)
                : base(envelope, principal, loginName)
            {
            }
        }

        public sealed class Enable : UserManagementRequestMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public Enable(IEnvelope envelope, IPrincipal principal, string loginName)
                : base(envelope, principal, loginName)
            {
            }
        }

        public sealed class Delete : UserManagementRequestMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public Delete(IEnvelope envelope, IPrincipal principal, string loginName)
                : base(envelope, principal, loginName)
            {
            }
        }

        public sealed class ResetPassword : UserManagementRequestMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly string NewPassword;

            public ResetPassword(IEnvelope envelope, IPrincipal principal, string loginName, string newPassword)
                : base(envelope, principal, loginName)
            {
                NewPassword = newPassword;
            }
        }

        public sealed class ChangePassword : UserManagementRequestMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly string CurrentPassword;
            public readonly string NewPassword;

            public ChangePassword(
                IEnvelope envelope, IPrincipal principal, string loginName, string currentPassword, string newPassword)
                : base(envelope, principal, loginName)
            {
                CurrentPassword = currentPassword;
                NewPassword = newPassword;
            }
        }

        public sealed class GetAll : RequestMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public GetAll(IEnvelope envelope, IPrincipal principal)
                : base(envelope, principal)
            {
            }
        }

        public sealed class Get : UserManagementRequestMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public Get(IEnvelope envelope, IPrincipal principal, string loginName)
                : base(envelope, principal, loginName)
            {
            }
        }

        public enum Error
        {
            Success,
            NotFound,
            Conflict,
            Error,
            TryAgain,
            Unauthorized
        }

        public sealed class UserData
        {
            public readonly string LoginName;
            public readonly string FullName;
            public readonly string[] Groups;
            public readonly DateTimeOffset? DateLastUpdated;
            public readonly bool Disabled;

            public UserData(
                string loginName, string fullName, string[] groups, bool disabled, DateTimeOffset? dateLastUpdated)
            {
                LoginName = loginName;
                FullName = fullName;
                Groups = groups;
                Disabled = disabled;
                DateLastUpdated = dateLastUpdated;
            }

            internal UserData()
            {
            }
        }

        public sealed class UpdateResult : ResponseMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly string LoginName;


            public UpdateResult(string loginName)
                : base(true, Error.Success)
            {
                LoginName = loginName;
            }

            public UpdateResult(string loginName, Error error)
                : base(false, error)
            {
                LoginName = loginName;
            }
        }

        public sealed class UserDetailsResult : ResponseMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

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
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

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

        public sealed class UserManagementServiceInitialized : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }
        }
    }
}
