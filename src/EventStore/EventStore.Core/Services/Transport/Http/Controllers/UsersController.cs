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
            RegisterTextBody(service, "/users/", HttpMethod.Post, PostUser);
        }

        private void PostUser(HttpEntityManager http, string s)
        {
            var envelope = CreateReplyEnvelope(http);
            var data = http.RequestCodec.From<PostUserData>(s);
            var createUserMessage = new UserManagementMessage.Create(
                envelope, data.LoginName, data.FullName, data.Password);
            Publish(createUserMessage);
        }

        private SendToHttpEnvelope<UserManagementMessage.UpdateResult> CreateReplyEnvelope(HttpEntityManager http)
        {
            return new SendToHttpEnvelope<UserManagementMessage.UpdateResult>(
                _networkSendQueue, http, AutoFormatter, AutoConfigurator, null);
        }

        private ResponseConfiguration AutoConfigurator(ICodec codec, UserManagementMessage.UpdateResult result)
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
    }
}
