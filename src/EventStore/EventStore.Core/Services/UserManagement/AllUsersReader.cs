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
using System.Collections.Generic;
using System.Text;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using System.Linq;
using EventStore.Core.Util;

namespace EventStore.Core.Services.UserManagement
{
    public class AllUsersReader
    {
        private const string UserEventType = "$User";
        private const string UserStreamPrefix = "$user-";
        private readonly IODispatcher _ioDispatcher;
        private List<UserManagementMessage.UserData> _results = new List<UserManagementMessage.UserData>();
        private Action<UserManagementMessage.Error, UserManagementMessage.UserData[]> _onCompleted;
        private int _activeRequests;
        private bool _aborted;

        public AllUsersReader(IODispatcher ioDispatcher)
        {
            _ioDispatcher = ioDispatcher;
        }

        public void Run(Action<UserManagementMessage.Error, UserManagementMessage.UserData[]> completed)
        {
            if (completed == null) throw new ArgumentNullException("completed");
            if (_onCompleted != null) throw new InvalidOperationException("AllUsersReader cannot be re-used");

            _onCompleted = completed;

            BeginReadForward(0);
        }

        private void BeginReadForward(int fromEventNumber)
        {
            _activeRequests++;
            _ioDispatcher.ReadForward("$users", fromEventNumber, 1, false, SystemAccount.Principal, ReadUsersForwardCompleted);
        }

        private void ReadUsersForwardCompleted(ClientMessage.ReadStreamEventsForwardCompleted result)
        {
            if (_aborted) 
                return;
            switch (result.Result)
            {
                case ReadStreamResult.Success:
                    if (!result.IsEndOfStream)
                        BeginReadForward(result.NextEventNumber);

                    foreach (var loginName in from eventData in result.Events
                                              let @event = eventData.Event
                                              where @event.EventType == UserEventType
                                              let stringData = Encoding.UTF8.GetString(@event.Data)
                                              select stringData)
                        BeginReadUserDetails(loginName);

                    break;
                case ReadStreamResult.NoStream:
                    Abort(UserManagementMessage.Error.NotFound);
                    break;
                default:
                    Abort(UserManagementMessage.Error.Error);
                    break;
            }
            _activeRequests--;
            TryComplete();
        }

        private void Abort(UserManagementMessage.Error error)
        {
            _onCompleted(error, null);
            _onCompleted = null;
            _aborted = true;
        }

        private void BeginReadUserDetails(string loginName)
        {
            _activeRequests++;
            _ioDispatcher.ReadBackward(
                UserStreamPrefix + loginName, -1, 1, false, SystemAccount.Principal, 
                result => ReadUserDetailsBackwardCompleted(loginName, result));
        }

        private void ReadUserDetailsBackwardCompleted(string loginName, ClientMessage.ReadStreamEventsBackwardCompleted result)
        {
            if (_aborted) 
                return;
            switch (result.Result)
            {
                case ReadStreamResult.Success:
                    if (result.Events.Length != 1)
                    {
                        AddLoadedUserDetails(loginName, "", true, null);
                    }
                    else
                    {
                        try
                        {
                            var eventRecord = result.Events[0].Event;
                            var userData = eventRecord.Data.ParseJson<UserData>();
                            AddLoadedUserDetails(
                                userData.LoginName, userData.FullName, userData.Disabled, eventRecord.TimeStamp);
                        }
                        catch
                        {
                            Abort(UserManagementMessage.Error.Error);
                        }
                    }
                    break;
                case ReadStreamResult.NoStream:
                    AddLoadedUserDetails(loginName, "", true, null);
                    break;
                default:
                    Abort(UserManagementMessage.Error.Error);
                    break;
            }
            _activeRequests--;
            TryComplete();
        }

        private void TryComplete()
        {
            if (!_aborted && _activeRequests == 0)
                _onCompleted(UserManagementMessage.Error.Success, _results.ToArray());
        }

        private void AddLoadedUserDetails(string loginName, string fullName, bool disabled, DateTime? dateLastUpdated)
        {
            _results.Add(new UserManagementMessage.UserData(loginName, fullName, disabled, dateLastUpdated));
        }
    }
}
