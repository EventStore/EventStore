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

using System.Collections.Generic;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Services.UserManagement;
using NUnit.Framework;
using System.Linq;

namespace EventStore.Core.Tests.Services.UserManagementService
{
    namespace password_change_notification_reader
    {
        public abstract class with_password_change_notification_reader :
            user_management_service.TestFixtureWithUserManagementService
        {
            protected PasswordChangeNotificationReader _passwordChangeNotificationReader;

            protected override void Given()
            {
                base.Given();
                _passwordChangeNotificationReader = new PasswordChangeNotificationReader(_bus, _ioDispatcher);
                _bus.Subscribe<SystemMessage.SystemStart>(_passwordChangeNotificationReader);
            }

            protected override IEnumerable<WhenStep> PreWhen()
            {
                foreach (var m in base.PreWhen()) yield return m;
                yield return new SystemMessage.SystemStart();
                yield return
                    new UserManagementMessage.Create(
                        Envelope, SystemAccount.Principal, "user1", "UserOne", new string[] {}, "password");
            }

            [TearDown]
            public void TearDown()
            {
                _passwordChangeNotificationReader = null;
            }
        }

        [TestFixture]
        public class when_notification_has_been_written : with_password_change_notification_reader
        {
            protected override void Given()
            {
                base.Given();
                NoOtherStreams();
                AllWritesSucceed();
            }



            protected override IEnumerable<WhenStep> When()
            {
                yield return
                    new UserManagementMessage.ChangePassword(
                        Envelope, SystemAccount.Principal, "user1", "password", "drowssap");
            }

            [Test]
            public void publishes_reset_password_cache()
            {
                Assert.AreEqual(
                    1, HandledMessages.OfType<InternalAuthenticationProviderMessages.ResetPasswordCache>().Count());
            }
        }
    }
}
