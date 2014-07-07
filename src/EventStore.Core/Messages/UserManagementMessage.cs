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
