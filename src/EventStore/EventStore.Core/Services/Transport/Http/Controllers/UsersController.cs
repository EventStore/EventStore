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
using EventStore.Core.Messages;
using EventStore.Transport.Http;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public class UsersController : CommunicationController
    {
        private readonly IPublisher _networkSendQueue;

        public UsersController(IPublisher publisher, IPublisher networkSendQueue)
            : base(publisher)
        {
            _networkSendQueue = networkSendQueue;
        }

        protected override void SubscribeCore(IHttpService service, HttpMessagePipe pipe)
        {
            RegisterUrlBased(service, "/users/", HttpMethod.Get, GetUsers);
            RegisterUrlBased(service, "/users/{login}", HttpMethod.Get, GetUser);
            RegisterTextBody(service, "/users/", HttpMethod.Post, PostUser);
            RegisterTextBody(service, "/users/{login}", HttpMethod.Put, PutUser);
            RegisterUrlBased(service, "/users/{login}", HttpMethod.Delete, DeleteUser);
            RegisterUrlBased(service, "/users/{login}/command/enable", HttpMethod.Post, PostCommandEnable);
            RegisterUrlBased(service, "/users/{login}/command/disable", HttpMethod.Post, PostCommandDisable);
            RegisterTextBody(
                service, "/users/{login}/command/reset-password", HttpMethod.Post, PostCommandResetPassword);
            RegisterTextBody(
                service, "/users/{login}/command/change-password", HttpMethod.Post, PostCommandChangePassword);
        }

        private void GetUsers(HttpEntityManager http, UriTemplateMatch match)
        {
            var envelope = CreateReplyEnvelope<UserManagementMessage.AllUserDetailsResult>(http);
            var message = new UserManagementMessage.GetAll(envelope);
            Publish(message);
        }


        private void GetUser(HttpEntityManager http, UriTemplateMatch match)
        {
            var envelope = CreateReplyEnvelope<UserManagementMessage.UserDetailsResult>(http);
            var login = match.BoundVariables["login"];
            var message = new UserManagementMessage.Get(envelope, login);
            Publish(message);
        }

        private void PostUser(HttpEntityManager http, string s)
        {
            var envelope = CreateReplyEnvelope<UserManagementMessage.UpdateResult>(
                http,
                configurator:
                    (codec, result) =>
                    AutoConfigurator(codec, result)
                        .SetCreated(
                            MakeUrl(http, "/users/" + Uri.EscapeDataString(result.LoginName))));
            var data = http.RequestCodec.From<PostUserData>(s);
            var message = new UserManagementMessage.Create(
                envelope, data.LoginName, data.FullName, data.Password);
            Publish(message);
        }

        private void PutUser(HttpEntityManager http, UriTemplateMatch match, string s)
        {
            var envelope = CreateReplyEnvelope<UserManagementMessage.UpdateResult>(http);
            var login = match.BoundVariables["login"];
            var data = http.RequestCodec.From<PutUserData>(s);
            var message = new UserManagementMessage.Update(envelope, login, data.FullName);
            Publish(message);
        }

        private void DeleteUser(HttpEntityManager http, UriTemplateMatch match)
        {
            var envelope = CreateReplyEnvelope<UserManagementMessage.UpdateResult>(http);
            var login = match.BoundVariables["login"];
            var message = new UserManagementMessage.Delete(envelope, login);
            Publish(message);
        }

        private void PostCommandEnable(HttpEntityManager http, UriTemplateMatch match)
        {
            var envelope = CreateReplyEnvelope<UserManagementMessage.UpdateResult>(http);
            var login = match.BoundVariables["login"];
            var message = new UserManagementMessage.Enable(envelope, login);
            Publish(message);
        }

        private void PostCommandDisable(HttpEntityManager http, UriTemplateMatch match)
        {
            var envelope = CreateReplyEnvelope<UserManagementMessage.UpdateResult>(http);
            var login = match.BoundVariables["login"];
            var message = new UserManagementMessage.Disable(envelope, login);
            Publish(message);
        }

        private void PostCommandResetPassword(HttpEntityManager http, UriTemplateMatch match, string s)
        {
            var envelope = CreateReplyEnvelope<UserManagementMessage.UpdateResult>(http);
            var login = match.BoundVariables["login"];
            var data = http.RequestCodec.From<ResetPasswordData>(s);
            var message = new UserManagementMessage.ResetPassword(envelope, login, data.NewPassword);
            Publish(message);
        }

        private void PostCommandChangePassword(HttpEntityManager http, UriTemplateMatch match, string s)
        {
            var envelope = CreateReplyEnvelope<UserManagementMessage.UpdateResult>(http);
            var login = match.BoundVariables["login"];
            var data = http.RequestCodec.From<ChangePasswordData>(s);
            var message = new UserManagementMessage.ChangePassword(
                envelope, login, data.CurrentPassword, data.NewPassword);
            Publish(message);
        }

        private SendToHttpEnvelope<T> CreateReplyEnvelope<T>(
            HttpEntityManager http, Func<ICodec, T, string> formatter = null,
            Func<ICodec, T, ResponseConfiguration> configurator = null) where T : UserManagementMessage.ResponseMessage
        {
            return new SendToHttpEnvelope<T>(
                _networkSendQueue, http, formatter ?? AutoFormatter, configurator ?? AutoConfigurator, null);
        }

        private ResponseConfiguration AutoConfigurator<T>(ICodec codec, T result)
            where T : UserManagementMessage.ResponseMessage
        {
            return result.Success
                       ? new ResponseConfiguration(HttpStatusCode.OK, codec.ContentType, codec.Encoding)
                       : new ResponseConfiguration(
                             ErrorToHttpStatusCode(result.Error), codec.ContentType, codec.Encoding);
        }

        private string AutoFormatter<T>(ICodec codec, T result)
        {
            return codec.To(result);
        }

        private int ErrorToHttpStatusCode(UserManagementMessage.Error error)
        {
            switch (error)
            {
                case UserManagementMessage.Error.Success:
                    return HttpStatusCode.OK;
                case UserManagementMessage.Error.Conflict:
                    return HttpStatusCode.Conflict;
                case UserManagementMessage.Error.NotFound:
                    return HttpStatusCode.NotFound;
                case UserManagementMessage.Error.Error:
                    return HttpStatusCode.InternalServerError;
                case UserManagementMessage.Error.TryAgain:
                    return HttpStatusCode.RequestTimeout;
                case UserManagementMessage.Error.Unauthorized:
                    return HttpStatusCode.Unauthorized;
                default:
                    return HttpStatusCode.InternalServerError;
            }
        }

        private class PostUserData
        {
            public string LoginName { get; set; }
            public string FullName { get; set; }
            public string Password { get; set; }
        }

        private class PutUserData
        {
            public string FullName { get; set; }
        }

        private class ResetPasswordData
        {
            public string NewPassword { get; set; }
        }

        private class ChangePasswordData
        {
            public string CurrentPassword { get; set; }
            public string NewPassword { get; set; }
        }
    }
}
