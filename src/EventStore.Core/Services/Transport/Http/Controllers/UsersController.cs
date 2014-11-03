﻿using System;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Common.Log;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public class UsersController : CommunicationController
    {
        private readonly IHttpForwarder _httpForwarder;
        private readonly IPublisher _networkSendQueue;
        private static readonly ICodec[] DefaultCodecs = new ICodec[] { Codec.Json, Codec.Xml };
        private static readonly ILogger Log = LogManager.GetLoggerFor<UsersController>();


        public UsersController(IHttpForwarder httpForwarder, IPublisher publisher, IPublisher networkSendQueue)
            : base(publisher)
        {
            _httpForwarder = httpForwarder;
            _networkSendQueue = networkSendQueue;
        }

        protected override void SubscribeCore(IHttpService service)
        {
            RegisterUrlBased(service, "/users/", HttpMethod.Get, GetUsers);
            RegisterUrlBased(service, "/users/{login}", HttpMethod.Get, GetUser);
            RegisterUrlBased(service, "/users/$current", HttpMethod.Get, GetCurrentUser);
            Register(service, "/users/", HttpMethod.Post, PostUser, DefaultCodecs, DefaultCodecs);
            Register(service, "/users/{login}", HttpMethod.Put, PutUser, DefaultCodecs, DefaultCodecs);
            RegisterUrlBased(service, "/users/{login}", HttpMethod.Delete, DeleteUser);
            RegisterUrlBased(service, "/users/{login}/command/enable", HttpMethod.Post, PostCommandEnable);
            RegisterUrlBased(service, "/users/{login}/command/disable", HttpMethod.Post, PostCommandDisable);
            Register(service, "/users/{login}/command/reset-password", HttpMethod.Post, PostCommandResetPassword, DefaultCodecs, DefaultCodecs);
            Register(service, "/users/{login}/command/change-password", HttpMethod.Post, PostCommandChangePassword, DefaultCodecs, DefaultCodecs);
        }

        private void GetUsers(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var envelope = CreateReplyEnvelope<UserManagementMessage.AllUserDetailsResult>(http);
            var message = new UserManagementMessage.GetAll(envelope, http.User);
            Publish(message);
        }


        private void GetUser(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var envelope = CreateReplyEnvelope<UserManagementMessage.UserDetailsResult>(http);
            var login = match.BoundVariables["login"];
            var message = new UserManagementMessage.Get(envelope, http.User, login);
            Publish(message);
        }

        private void GetCurrentUser(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var envelope = CreateReplyEnvelope<UserManagementMessage.UserDetailsResult>(http);
            if (http.User == null)
            {
                envelope.ReplyWith(
                    new UserManagementMessage.UserDetailsResult(UserManagementMessage.Error.Unauthorized));
                return;
            }

            var message = new UserManagementMessage.Get(envelope, http.User, http.User.Identity.Name);
            Publish(message);
        }

        private void PostUser(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var envelope = CreateReplyEnvelope<UserManagementMessage.UpdateResult>(
                http, configurator: (codec, result) =>
                    {
                        var configuration = AutoConfigurator(codec, result);
                        return configuration.Code == HttpStatusCode.OK
                                   ? configuration.SetCreated(
                                       MakeUrl(http, "/users/" + Uri.EscapeDataString(result.LoginName)))
                                   : configuration;
                    });
            http.ReadTextRequestAsync(
                (o, s) =>
                {
                    var data = http.RequestCodec.From<PostUserData>(s);
                    var message = new UserManagementMessage.Create(
                        envelope, http.User, data.LoginName, data.FullName, data.Groups, data.Password);
                    Publish(message);
                }, x => Log.DebugException(x, "Reply Text Content Failed."));
        }

        private void PutUser(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var envelope = CreateReplyEnvelope<UserManagementMessage.UpdateResult>(http);
            http.ReadTextRequestAsync(
                (o, s) =>
                {
                    var login = match.BoundVariables["login"];
                    var data = http.RequestCodec.From<PutUserData>(s);
                    var message = new UserManagementMessage.Update(envelope, http.User, login, data.FullName, data.Groups);
                    Publish(message);
                }, x => Log.DebugException(x, "Reply Text Content Failed."));
        }

        private void DeleteUser(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var envelope = CreateReplyEnvelope<UserManagementMessage.UpdateResult>(http);
            var login = match.BoundVariables["login"];
            var message = new UserManagementMessage.Delete(envelope, http.User, login);
            Publish(message);
        }

        private void PostCommandEnable(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var envelope = CreateReplyEnvelope<UserManagementMessage.UpdateResult>(http);
            var login = match.BoundVariables["login"];
            var message = new UserManagementMessage.Enable(envelope, http.User, login);
            Publish(message);
        }

        private void PostCommandDisable(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var envelope = CreateReplyEnvelope<UserManagementMessage.UpdateResult>(http);
            var login = match.BoundVariables["login"];
            var message = new UserManagementMessage.Disable(envelope, http.User, login);
            Publish(message);
        }

        private void PostCommandResetPassword(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var envelope = CreateReplyEnvelope<UserManagementMessage.UpdateResult>(http);
            http.ReadTextRequestAsync(
                (o, s) =>
                {
                    var login = match.BoundVariables["login"];
                    var data = http.RequestCodec.From<ResetPasswordData>(s);
                    var message = new UserManagementMessage.ResetPassword(envelope, http.User, login, data.NewPassword);
                    Publish(message);
                }, x => Log.DebugException(x, "Reply Text Content Failed."));
        }

        private void PostCommandChangePassword(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var envelope = CreateReplyEnvelope<UserManagementMessage.UpdateResult>(http);
            http.ReadTextRequestAsync(
                (o, s) =>
                    {
                        var login = match.BoundVariables["login"];
                        var data = http.RequestCodec.From<ChangePasswordData>(s);
                        var message = new UserManagementMessage.ChangePassword(
                            envelope, http.User, login, data.CurrentPassword, data.NewPassword);
                        Publish(message);

                    },
                x => Log.DebugException(x, "Reply Text Content Failed."));
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
            public string[] Groups { get; set; }
            public string Password { get; set; }
        }

        private class PutUserData
        {
            public string FullName { get; set; }
            public string[] Groups { get; set; }
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
