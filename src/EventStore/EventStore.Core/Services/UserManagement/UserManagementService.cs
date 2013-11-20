﻿// Copyright (c) 2012, Event Store LLP
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

using System;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;

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
                                         IHandle<UserManagementMessage.Delete>,
                                         IHandle<SystemMessage.BecomeMaster>
    {
        public const string UserUpdated = "$UserUpdated";
        public const string PasswordChanged = "$PasswordChanged";
        public const string UserPasswordNotificationsStreamId = "$users-password-notifications";
        private readonly IPublisher _publisher;
        private readonly IODispatcher _ioDispatcher;
        private readonly PasswordHashAlgorithm _passwordHashAlgorithm;
        private readonly bool _skipInitializeStandardUsersCheck;
        private readonly ILogger _log;

        public UserManagementService(
            IPublisher publisher, IODispatcher ioDispatcher, PasswordHashAlgorithm passwordHashAlgorithm,
            bool skipInitializeStandardUsersCheck)
        {
            _log = LogManager.GetLoggerFor<UserManagementService>();
            _publisher = publisher;
            _ioDispatcher = ioDispatcher;
            _passwordHashAlgorithm = passwordHashAlgorithm;
            _skipInitializeStandardUsersCheck = skipInitializeStandardUsersCheck;
        }

        public void Handle(UserManagementMessage.Create message)
        {
            if (!DemandAdmin(message)) return;
            var userData = CreateUserData(message);
            WriteStreamAcl(
                message, message.LoginName,
                () => WriteUserEvent(message, userData, "$UserCreated", ExpectedVersion.NoStream));
        }

        public void Handle(UserManagementMessage.Update message)
        {
            if (!DemandAdmin(message)) return;
            ReadUpdateWriteReply(
                message, data => data.SetFullName(message.FullName).SetGroups(message.Groups), resetPasswordCache: false);
        }

        public void Handle(UserManagementMessage.Enable message)
        {
            if (!DemandAdmin(message)) return;
            ReadUpdateWriteReply(message, data => data.SetEnabled(), resetPasswordCache: false);
        }

        public void Handle(UserManagementMessage.Disable message)
        {
            if (!DemandAdmin(message)) return;
            ReadUpdateWriteReply(message, data => data.SetDisabled(), resetPasswordCache: true);
        }

        public void Handle(UserManagementMessage.ResetPassword message)
        {
            if (!DemandAdmin(message)) return;
            string hash;
            string salt;
            _passwordHashAlgorithm.Hash(message.NewPassword, out hash, out salt);
            ReadUpdateWriteReply(message, data => data.SetPassword(hash, salt), resetPasswordCache: true);
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
                    }, resetPasswordCache: true);
        }

        public void Handle(UserManagementMessage.Delete message)
        {
            if (!DemandAdmin(message)) return;
            ReadUpdateCheckAnd(
                message,
                (completed, data) =>
                _ioDispatcher.DeleteStream(
                    "$user-" + message.LoginName, completed.FromEventNumber, true, SystemAccount.Principal,
                    streamCompleted =>
                    WritePasswordChangedEventConditionalAnd(
                        message, true, () => ReplyByWriteResult(message, streamCompleted.Result))));
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
                                        message.LoginName, data.FullName, data.Groups, data.Disabled,
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

        public void Handle(SystemMessage.BecomeMaster message)
        {
            if (!_skipInitializeStandardUsersCheck)
                BeginReadUserDetails(
                    "admin", completed =>
                        {
                            if (completed.Result == ReadStreamResult.NoStream)
                                CreateAdminUser();
                            else
                                NotifyInitialized();
                        });
            else
                NotifyInitialized();
        }

        private void NotifyInitialized()
        {
            _publisher.Publish(new UserManagementMessage.UserManagementServiceInitialized());
        }

        private UserData CreateUserData(UserManagementMessage.Create message)
        {
            return CreateUserData(message.LoginName, message.FullName, message.Groups, message.Password);
        }

        private UserData CreateUserData(string loginName, string fullName, string[] groups, string password)
        {
            string hash;
            string salt;
            _passwordHashAlgorithm.Hash(password, out hash, out salt);
            return new UserData(loginName, fullName, groups, hash, salt, disabled: false);
        }

        private void ReadUserDetailsAnd(
            UserManagementMessage.UserManagementRequestMessage message,
            Action<ClientMessage.ReadStreamEventsBackwardCompleted, UserData> action)
        {
            string loginName = message.LoginName;
            BeginReadUserDetails(
                loginName, completed =>
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
                                    var data1 = completed.Events[0].Event.Data.ParseJson<UserData>();
                                    action(completed, data1);
                                }
                                break;
                            default:
                                ReplyInternalError(message);
                                break;
                        }
                    });
        }

        private void BeginReadUserDetails(
            string loginName, Action<ClientMessage.ReadStreamEventsBackwardCompleted> completed)
        {
            var streamId = "$user-" + loginName;
            _ioDispatcher.ReadBackward(streamId, -1, 1, false, SystemAccount.Principal, completed);
        }

        private void ReadUpdateWriteReply(
            UserManagementMessage.UserManagementRequestMessage message, Func<UserData, UserData> update,
            bool resetPasswordCache)
        {
            ReadUpdateCheckAnd(
                message, (completed, data) =>
                    {
                        var updated = update(data);
                        if (updated != null)
                        {
                            WritePasswordChangedEventConditionalAnd(
                                message, resetPasswordCache,
                                () => WriteUserEvent(message, updated, UserUpdated, completed.FromEventNumber));
                        }
                    });
        }

        private void WritePasswordChangedEventConditionalAnd(
            UserManagementMessage.UserManagementRequestMessage message, bool resetPasswordCache, Action onCompleted)
        {
            if (resetPasswordCache)
                BeginWritePasswordChangedEvent(
                    message.LoginName,
                    eventsCompleted => WritePasswordChangedEventCompleted(message, eventsCompleted, onCompleted));
            else
            {
                onCompleted();
            }
        }

        private void BeginWritePasswordChangedEvent(
            string loginName, Action<ClientMessage.WriteEventsCompleted> completed)
        {
            var streamMetadata =
                new Lazy<StreamMetadata>(() => new StreamMetadata(null, TimeSpan.FromHours(1), null, null));
            _ioDispatcher.ConfigureStreamAndWriteEvents(
                UserPasswordNotificationsStreamId, ExpectedVersion.Any, streamMetadata,
                new[] {CreatePasswordChangedEvent(loginName)}, SystemAccount.Principal, completed);
        }

        private void WritePasswordChangedEventCompleted(
            UserManagementMessage.UserManagementRequestMessage message,
            ClientMessage.WriteEventsCompleted eventsCompleted, Action onCompleted)
        {
            switch (eventsCompleted.Result)
            {
                case OperationResult.Success:
                    onCompleted();
                    break;
                case OperationResult.CommitTimeout:
                case OperationResult.ForwardTimeout:
                case OperationResult.PrepareTimeout:
                    ReplyTryAgain(message);
                    break;
                case OperationResult.AccessDenied:
                    ReplyUnauthorized(message);
                    break;
                case OperationResult.WrongExpectedVersion:
                    ReplyConflict(message);
                    break;
                default:
                    ReplyInternalError(message);
                    break;
            }
        }

        private static Event CreatePasswordChangedEvent(string loginName)
        {
            return new Event(Guid.NewGuid(), PasswordChanged, true, new {LoginName = loginName}.ToJsonBytes(), null);
        }

        private void ReadUpdateCheckAnd(
            UserManagementMessage.UserManagementRequestMessage message,
            Action<ClientMessage.ReadStreamEventsBackwardCompleted, UserData> action)
        {
            ReadUserDetailsAnd(message, action);
        }

        private void WriteStreamAcl(
            UserManagementMessage.UserManagementRequestMessage message, string loginName, Action onSucceeded)
        {
            WriteStreamAcl(
                loginName, completed =>
                    {
                        switch (completed.Result)
                        {
                            case OperationResult.Success:
                                onSucceeded();
                                break;
                            case OperationResult.CommitTimeout:
                            case OperationResult.ForwardTimeout:
                            case OperationResult.PrepareTimeout:
                            case OperationResult.WrongExpectedVersion:
                                ReplyTryAgain(message);
                                break;
                            default:
                                ReplyInternalError(message);
                                break;
                        }
                    });
        }

        private void WriteStreamAcl(string loginName, Action<ClientMessage.WriteEventsCompleted> onCompleted)
        {
            _ioDispatcher.UpdateStreamAcl(
                "$user-" + loginName, ExpectedVersion.Any, SystemAccount.Principal,
                new StreamMetadata(
                    null, null, null, null, null, new StreamAcl(null, SystemRoles.Admins, SystemRoles.Admins, null, SystemRoles.Admins)),
                onCompleted);
        }

        private void WriteUserEvent(
            UserManagementMessage.UserManagementRequestMessage message, UserData userData, string eventType,
            int expectedVersion)
        {
            WriteUserEvent(
                userData, eventType, expectedVersion, completed => WriteUserCreatedCompleted(completed, message));
        }

        private void WriteUserEvent(
            UserData userData, string eventType, int expectedVersion,
            Action<ClientMessage.WriteEventsCompleted> onCompleted)
        {
            var userCreatedEvent = new Event(Guid.NewGuid(), eventType, true, userData.ToJsonBytes(), null);
            _ioDispatcher.WriteEvents(
                "$user-" + userData.LoginName, expectedVersion, new[] {userCreatedEvent}, SystemAccount.Principal,
                onCompleted);
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

        private void CreateAdminUser()
        {
            var userData = CreateUserData(
                SystemUsers.Admin, "Event Store Administrator", new[] {SystemRoles.Admins},
                SystemUsers.DefaultAdminPassword);
            WriteStreamAcl(
                SystemUsers.Admin, completed1 =>
                {
                    switch (completed1.Result)
                    {
                        case OperationResult.CommitTimeout:
                        case OperationResult.PrepareTimeout:
                            CreateAdminUser();
                            break;
                        default:
                            _log.Error("'admin' user account could not be created");
                            NotifyInitialized();
                            break;
                        case OperationResult.Success:
                            WriteUserEvent(
                                userData, "$UserCreated", ExpectedVersion.NoStream, completed =>
                                    {
                                        switch (completed.Result)
                                        {
                                            case OperationResult.Success:
                                                _log.Info("'admin' user account has been created");
                                                NotifyInitialized();
                                                break;
                                            case OperationResult.CommitTimeout:
                                            case OperationResult.PrepareTimeout:
                                                CreateAdminUser();
                                                break;
                                            default:
                                                _log.Error("'admin' user account could not be created");
                                                NotifyInitialized();
                                                break;
                                        }
                                    });
                            break;
                    }
                });
        }

        private bool DemandAdmin(UserManagementMessage.UserManagementRequestMessage message)
        {
            if (message.Principal == null || !message.Principal.IsInRole(SystemRoles.Admins))
            {
                ReplyUnauthorized(message);
                return false;
            }
            return true;
        }

    }
}
