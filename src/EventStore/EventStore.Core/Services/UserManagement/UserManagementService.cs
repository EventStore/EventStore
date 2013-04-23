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
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Util;
using System.Linq;

namespace EventStore.Core.Services.UserManagement
{
    public class UserManagementService : IHandle<UserManagementMessage.Get>,
                                         IHandle<UserManagementMessage.GetAll>,
                                         IHandle<UserManagementMessage.Create>,
                                         IHandle<UserManagementMessage.Update>,
                                         IHandle<UserManagementMessage.Enable>,
                                         IHandle<UserManagementMessage.Disable>,
                                         IHandle<UserManagementMessage.ResetPassword>,
                                         IHandle<UserManagementMessage.ChangePassword>,
                                         IHandle<UserManagementMessage.Delete>
    {
        private readonly IPublisher _publisher;
        private readonly IODispatcher _ioDispatcher;
        private readonly PasswordHashAlgorithm _passwordHashAlgorithm;

        public UserManagementService(
            IPublisher publisher, IODispatcher ioDispatcher, PasswordHashAlgorithm passwordHashAlgorithm)
        {
            _publisher = publisher;
            _ioDispatcher = ioDispatcher;
            _passwordHashAlgorithm = passwordHashAlgorithm;
        }

        public void Handle(UserManagementMessage.Create message)
        {
            var userData = CreateUserData(message);
            WriteUserEvent(message, userData, "$UserCreated", ExpectedVersion.NoStream);
        }

        public void Handle(UserManagementMessage.Update message)
        {
            ReadUpdateWriteReply(message, data => data.SetFullName(message.FullName));
        }

        public void Handle(UserManagementMessage.Enable message)
        {
            ReadUpdateWriteReply(message, data => data.SetEnabled());
        }

        public void Handle(UserManagementMessage.Disable message)
        {
            ReadUpdateWriteReply(message, data => data.SetDisabled());
        }

        public void Handle(UserManagementMessage.ResetPassword message)
        {
            string hash;
            string salt;
            _passwordHashAlgorithm.Hash(message.NewPassword, out hash, out salt);
            ReadUpdateWriteReply(message, data => data.SetPassword(hash, salt));
        }

        public void Handle(UserManagementMessage.ChangePassword message)
        {
            string hash;
            string salt;
            _passwordHashAlgorithm.Hash(message.NewPassword, out hash, out salt);
            ReadUpdateWriteReply(
                message, data =>
                    {
                        if (_passwordHashAlgorithm.Verify(message.CurrentPassword, data.Hash, data.Salt))
                            return data.SetPassword(hash, salt);

                        ReplyUnauthorized(message);
                        return null;
                    });
        }

        public void Handle(UserManagementMessage.Delete message)
        {
            ReadUpdateCheckAnd(
                message,
                (completed, data) =>
                _ioDispatcher.DeleteStream(
                    "$user-" + message.LoginName, completed.FromEventNumber, SystemAccount.Principal,
                    streamCompleted => ReplyByWriteResult(message, streamCompleted.Result)));
        }

        public void Handle(UserManagementMessage.Get message)
        {
            ReadUserDetailsAnd(
                message, (completed, data) =>
                    {
                        if (completed.Result == ReadStreamResult.Success && completed.Events.Length == 1)
                            message.Envelope.ReplyWith(
                                new UserManagementMessage.UserDetailsResult(
                                    new UserManagementMessage.UserData(
                                        message.LoginName, data.FullName, data.Disabled,
                                        new DateTimeOffset(completed.Events[0].Event.TimeStamp, TimeSpan.FromHours(0)))));
                        else
                        {
                            switch (completed.Result)
                            {
                                case ReadStreamResult.NoStream:
                                case ReadStreamResult.StreamDeleted:
                                    message.Envelope.ReplyWith(
                                        new UserManagementMessage.UserDetailsResult(
                                            UserManagementMessage.Error.NotFound));
                                    break;
                                default:
                                    message.Envelope.ReplyWith(
                                        new UserManagementMessage.UserDetailsResult(UserManagementMessage.Error.Error));
                                    break;
                            }
                        }
                    });
        }

        public void Handle(UserManagementMessage.GetAll message)
        {
            var allUsersReader = new AllUsersReader(_ioDispatcher);
            allUsersReader.Run(
                (error, data) =>
                message.Envelope.ReplyWith(
                    error == UserManagementMessage.Error.Success
                        ? new UserManagementMessage.AllUserDetailsResult(data.OrderBy(v => v.LoginName).ToArray())
                        : new UserManagementMessage.AllUserDetailsResult(error)));
        }

        private UserData CreateUserData(UserManagementMessage.Create message)
        {
            string hash;
            string salt;
            _passwordHashAlgorithm.Hash(message.Password, out hash, out salt);
            return new UserData(message.LoginName, message.FullName, hash, salt, disabled: false);
        }

        private void ReadUserDetailsAnd(
            UserManagementMessage.UserManagementRequestMessage message,
            Action<ClientMessage.ReadStreamEventsBackwardCompleted, UserData> action)
        {
            var streamId = "$user-" + message.LoginName;
            _ioDispatcher.ReadBackward(
                streamId, -1, 1, false, SystemAccount.Principal, completed =>
                    {
                        switch (completed.Result)
                        {
                            case ReadStreamResult.NoStream:
                                ReplyNotFound(message);
                                break;
                            case ReadStreamResult.StreamDeleted:
                                ReplyNotFound(message);
                                break;
                            case ReadStreamResult.Success:
                                if (completed.Events.Length == 0)
                                    ReplyNotFound(message);
                                else
                                {
                                    var data = completed.Events[0].Event.Data.ParseJson<UserData>();
                                    action(completed, data);
                                }
                                break;
                            default:
                                ReplyInternalError(message);
                                break;
                        }
                    });
        }

        private void ReadUpdateWriteReply(
            UserManagementMessage.UserManagementRequestMessage message, Func<UserData, UserData> update)
        {
            ReadUpdateCheckAnd(
                message, (completed, data) =>
                    {
                        var updated = update(data);
                        if (updated != null)
                            WriteUserEvent(message, updated, "$UserUpdated", completed.FromEventNumber);
                    });
        }

        private void ReadUpdateCheckAnd(
            UserManagementMessage.UserManagementRequestMessage message,
            Action<ClientMessage.ReadStreamEventsBackwardCompleted, UserData> action)
        {
            ReadUserDetailsAnd(message, action);
        }

        private void WriteUserEvent(
            UserManagementMessage.UserManagementRequestMessage message, UserData userData, string eventType,
            int expectedVersion)
        {
            var userCreatedEvent = new Event(Guid.NewGuid(), eventType, true, userData.ToJsonBytes(), null);
            _ioDispatcher.WriteEvents(
                "$user-" + message.LoginName, expectedVersion, new[] { userCreatedEvent }, SystemAccount.Principal,
                completed => WriteUserCreatedCompleted(completed, message));
        }

        private static void WriteUserCreatedCompleted(
            ClientMessage.WriteEventsCompleted completed, UserManagementMessage.UserManagementRequestMessage message)
        {
            if (completed.Result == OperationResult.Success)
            {
                ReplyUpdated(message);
                return;
            }
            ReplyByWriteResult(message, completed.Result);
        }

        private static void ReplyInternalError(UserManagementMessage.UserManagementRequestMessage message)
        {
            ReplyError(message, UserManagementMessage.Error.Error);
        }

        private static void ReplyNotFound(UserManagementMessage.UserManagementRequestMessage message)
        {
            ReplyError(message, UserManagementMessage.Error.NotFound);
        }

        private static void ReplyConflict(UserManagementMessage.UserManagementRequestMessage message)
        {
            ReplyError(message, UserManagementMessage.Error.Conflict);
        }

        private static void ReplyUnauthorized(UserManagementMessage.UserManagementRequestMessage message)
        {
            //NOTE: probably unauthorized iis not correct reply here.  
            // been converted to http 401 status code it will prompt for authorization
            ReplyError(message, UserManagementMessage.Error.Unauthorized);
        }

        private static void ReplyTryAgain(UserManagementMessage.UserManagementRequestMessage message)
        {
            ReplyError(message, UserManagementMessage.Error.TryAgain);
        }

        private static void ReplyUpdated(UserManagementMessage.UserManagementRequestMessage message)
        {
            message.Envelope.ReplyWith(new UserManagementMessage.UpdateResult(message.LoginName));
        }

        private static void ReplyError(
            UserManagementMessage.UserManagementRequestMessage message, UserManagementMessage.Error error)
        {
            //TODO: avoid 'is'
            if (message is UserManagementMessage.Get)
                message.Envelope.ReplyWith(new UserManagementMessage.UserDetailsResult(error));
            else
                message.Envelope.ReplyWith(new UserManagementMessage.UpdateResult(message.LoginName, error));
        }

        private static void ReplyByWriteResult(
            UserManagementMessage.UserManagementRequestMessage message, OperationResult operationResult)
        {
            switch (operationResult)
            {
                case OperationResult.Success:
                    ReplyUpdated(message);
                    break;
                case OperationResult.PrepareTimeout:
                case OperationResult.CommitTimeout:
                case OperationResult.ForwardTimeout:
                    ReplyTryAgain(message);
                    break;
                case OperationResult.WrongExpectedVersion:
                case OperationResult.StreamDeleted:
                    ReplyConflict(message);
                    break;
                default:
                    ReplyInternalError(message);
                    break;
            }
        }
    }
}
