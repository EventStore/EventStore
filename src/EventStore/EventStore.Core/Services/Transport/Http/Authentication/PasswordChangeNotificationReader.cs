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
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;
using Newtonsoft.Json;
using EventStore.Common.Utils;

namespace EventStore.Core.Services.Transport.Http.Authentication
{
    public class PasswordChangeNotificationReader : IHandle<SystemMessage.SystemStart>,
                                                    IHandle<SystemMessage.BecomeShutdown>
    {
        private readonly IPublisher _publisher;
        private readonly IODispatcher _ioDispatcher;
        private readonly ILogger _log;
        private bool _stopped;

        public PasswordChangeNotificationReader(IPublisher publisher, IODispatcher ioDispatcher)
        {
            _publisher = publisher;
            _ioDispatcher = ioDispatcher;
            _log = LogManager.GetLoggerFor<UserManagementService>();
        }

        private void Start()
        {
            _stopped = false;
            _ioDispatcher.ReadBackward(
                UserManagementService.UserPasswordNotificationsStreamId, -1, 1, false, SystemAccount.Principal,
                completed =>
                    {
                        switch (completed.Result)
                        {
                            case ReadStreamResult.NoStream:
                                ReadNotificationsFrom(0);
                                break;
                            case ReadStreamResult.Success:
                                if (completed.Events.Length == 0)
                                    ReadNotificationsFrom(0);
                                else
                                    ReadNotificationsFrom(completed.Events[0].Event.EventNumber + 1);
                                break;
                            default:
                                throw new Exception(
                                    "Failed to initialize password change notification reader. Cannot read "
                                    + UserManagementService.UserPasswordNotificationsStreamId + " Error: "
                                    + completed.Result);
                        }
                    });
        }

        private void ReadNotificationsFrom(int fromEventNumber)
        {
            if (_stopped) return;
            _ioDispatcher.ReadForward(
                UserManagementService.UserPasswordNotificationsStreamId, fromEventNumber, 100, false,
                SystemAccount.Principal, completed =>
                    {
                        if (_stopped) return;
                        switch (completed.Result)
                        {
                            case ReadStreamResult.AccessDenied:
                            case ReadStreamResult.Error:
                            case ReadStreamResult.NotModified:
                                throw new Exception(
                                    "Failed to read: " + UserManagementService.UserPasswordNotificationsStreamId);
                            case ReadStreamResult.NoStream:
                            case ReadStreamResult.StreamDeleted:
                                _ioDispatcher.Delay(
                                    TimeSpan.FromSeconds(1), () => ReadNotificationsFrom(completed.NextEventNumber));
                                break;
                            case ReadStreamResult.Success:
                                foreach (var @event in completed.Events)
                                    PublishPasswordChangeNotificationFrom(@event);
                                if (completed.IsEndOfStream)
                                    _ioDispatcher.Delay(
                                        TimeSpan.FromSeconds(1), () => ReadNotificationsFrom(completed.NextEventNumber));
                                else
                                    ReadNotificationsFrom(completed.NextEventNumber);
                                break;
                            default:
                                throw new NotSupportedException();
                        }
                    });
        }


        private class Notification
        {
            public string LoginName;
        }

        private void PublishPasswordChangeNotificationFrom(ResolvedEvent @event)
        {
            var data = @event.Event.Data;
            try
            {
                var notification = data.ParseJson<Notification>();
                _publisher.Publish(
                    new InternalAuthenticationProviderMessages.ResetPasswordCache(notification.LoginName));
            }
            catch (JsonException ex)
            {
                _log.Error("Failed to de-serialize event #{0}. Error: '{1}'", @event.OriginalEventNumber, ex.Message);
            }
        }

        public void Handle(SystemMessage.SystemStart message)
        {
            Start();
        }

        public void Handle(SystemMessage.BecomeShutdown message)
        {
            _stopped = true;
        }
    }
}
