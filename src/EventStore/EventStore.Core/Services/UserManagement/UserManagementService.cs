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
    class UserManagementService : IHandle<UserManagementMessage.Create>
    {
        private readonly IPublisher _publisher;
        private readonly IODispatcher _ioDispatcher;
        private readonly PasswordHashAlgorithm _passwordHashAlgorithm;

        public UserManagementService(IPublisher publisher, IODispatcher ioDispatcher, PasswordHashAlgorithm passwordHashAlgorithm)
        {
            _publisher = publisher;
            _ioDispatcher = ioDispatcher;
            _passwordHashAlgorithm = passwordHashAlgorithm;
        }

        public void Handle(UserManagementMessage.Create message)
        {
            var userData = CreateUserData(message);
            var userCreatedEvent = new Event(Guid.NewGuid(), "$UserCreated", true, userData.ToJsonBytes(), null);
            _ioDispatcher.WriteEvents(
                "$user-" + message.LoginName, completed => WriteUserCreatedCompleted(completed, message), ExpectedVersion.NoStream, new[] {userCreatedEvent});
        }

        private static void WriteUserCreatedCompleted(
            ClientMessage.WriteEventsCompleted completed, UserManagementMessage.Create message)
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
            message.Envelope.ReplyWith(new UserManagementMessage.UpdateResult(message.LoginName, UserManagementMessage.Error.Error));
        }

        private static void ReplyConflict(UserManagementMessage.UserManagementRequestMessage message)
        {
            message.Envelope.ReplyWith(new UserManagementMessage.UpdateResult(message.LoginName, UserManagementMessage.Error.Conflict));
        }

        private static void ReplyTryAgain(UserManagementMessage.UserManagementRequestMessage message)
        {
            message.Envelope.ReplyWith(new UserManagementMessage.UpdateResult(message.LoginName, UserManagementMessage.Error.TryAgain));
        }

        private static void ReplyUpdated(UserManagementMessage.UserManagementRequestMessage message)
        {
            message.Envelope.ReplyWith(new UserManagementMessage.UpdateResult(message.LoginName));
        }

        private UserData CreateUserData(UserManagementMessage.Create message)
        {
            var hashedPassword = _passwordHashAlgorithm.Hash(message.Password);
            var hash = hashedPassword.Item1;
            var salt = hashedPassword.Item2;
            return new UserData {LoginName = message.LoginName, FullName = message.FullName, Salt = salt, Hash = hash};
        }
    }
}
