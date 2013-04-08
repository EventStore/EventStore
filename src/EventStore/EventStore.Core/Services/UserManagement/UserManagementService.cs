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
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Util;

namespace EventStore.Core.Services.UserManagement
{
    class UserManagementService : IHandle<UserManagementMessage.Create>,
                                  IHandle<UserManagementMessage.Update>,
                                  IHandle<UserManagementMessage.Enable>,
                                  IHandle<UserManagementMessage.Disable>,
                                  IHandle<UserManagementMessage.ResetPassword>,
                                  IHandle<UserManagementMessage.ChangePassword>,
                                  IHandle<UserManagementMessage.Delete>,
                                  IHandle<UserManagementMessage.Get>
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

        private static void WriteUserCreatedCompleted(
            ClientMessage.WriteEventsCompleted completed, UserManagementMessage.UserManagementRequestMessage message)
        {
            if (completed.Result == OperationResult.Success)
                ReplyUpdated(message);

            switch (completed.Result)
            {
                case OperationResult.CommitTimeout:
                case OperationResult.ForwardTimeout:
                case OperationResult.PrepareTimeout:
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

        private static void ReplyInternalError(UserManagementMessage.UserManagementRequestMessage message)
        {
            message.Envelope.ReplyWith(
                new UserManagementMessage.UpdateResult(message.LoginName, UserManagementMessage.Error.Error));
        }

        private static void ReplyConflict(UserManagementMessage.UserManagementRequestMessage message)
        {
            message.Envelope.ReplyWith(
                new UserManagementMessage.UpdateResult(message.LoginName, UserManagementMessage.Error.Conflict));
        }

        private static void ReplyUnauthorized(UserManagementMessage.UserManagementRequestMessage message)
        {
            //NOTE: probably unauthorized iis not correct reply here.  
            // been converted to http 401 status code it will prompt for authorization
            message.Envelope.ReplyWith(
                new UserManagementMessage.UpdateResult(message.LoginName, UserManagementMessage.Error.Unauthorized));
        }

        private static void ReplyTryAgain(UserManagementMessage.UserManagementRequestMessage message)
        {
            message.Envelope.ReplyWith(
                new UserManagementMessage.UpdateResult(message.LoginName, UserManagementMessage.Error.TryAgain));
        }

        private static void ReplyUpdated(UserManagementMessage.UserManagementRequestMessage message)
        {
            message.Envelope.ReplyWith(new UserManagementMessage.UpdateResult(message.LoginName));
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
                streamId, -1, 1, false, completed =>
                    {
                        switch (completed.Result)
                        {
                            case ReadStreamResult.NoStream:
                                message.Envelope.ReplyWith(
                                    new UserManagementMessage.UpdateResult(
                                        message.LoginName, UserManagementMessage.Error.NotFound));
                                break;
                            case ReadStreamResult.StreamDeleted:
                                message.Envelope.ReplyWith(
                                    new UserManagementMessage.UpdateResult(
                                        message.LoginName, UserManagementMessage.Error.NotFound));
                                break;
                            case ReadStreamResult.Success:
                                var data = completed.Events[0].Event.Data.ParseJson<UserData>();
                                action(completed, data);
                                break;
                            default:
                                message.Envelope.ReplyWith(
                                    new UserManagementMessage.UpdateResult(
                                        message.LoginName, UserManagementMessage.Error.Error));
                                break;
                        }
                    });
        }

        private void WriteUserEvent(
            UserManagementMessage.UserManagementRequestMessage message, UserData userData, string eventType,
            int expectedVersion)
        {
            var userCreatedEvent = new Event(Guid.NewGuid(), eventType, true, userData.ToJsonBytes(), null);
            _ioDispatcher.WriteEvents(
                "$user-" + message.LoginName, completed => WriteUserCreatedCompleted(completed, message),
                expectedVersion, new[] {userCreatedEvent});
        }

        private void ReadUpdateWriteReply(
            UserManagementMessage.UserManagementRequestMessage message, Func<UserData, UserData> update)
        {
            ReadUserDetailsAnd(
                message, (completed, data) =>
                    {
                        var updated = update(data);
                        if (updated != null)
                            WriteUserEvent(message, updated, "$UserUpdated", completed.FromEventNumber);
                    });
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
            throw new NotImplementedException();
            ReadUserDetailsAnd(message, (completed, data) => { });
        }

        public void Handle(UserManagementMessage.Get message)
        {
            throw new NotImplementedException();
            ReadUserDetailsAnd(message, (completed, data) => { });
        }
    }
}
