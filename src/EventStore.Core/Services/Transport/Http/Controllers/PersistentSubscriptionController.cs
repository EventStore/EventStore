using System;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Common.Log;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public class PersistentSubscriptionController : CommunicationController
    {
        private readonly IHttpForwarder _httpForwarder;
        private readonly IPublisher _networkSendQueue;
        private static readonly ICodec[] DefaultCodecs = {Codec.Json, Codec.Xml};
        private static readonly ILogger Log = LogManager.GetLoggerFor<PersistentSubscriptionController>();

        public PersistentSubscriptionController(IHttpForwarder httpForwarder, IPublisher publisher, IPublisher networkSendQueue)
            : base(publisher)
        {
            _httpForwarder = httpForwarder;
            _networkSendQueue = networkSendQueue;
        }

        protected override void SubscribeCore(IHttpService service)
        {
            Register(service, "/subscriptions/{stream}/{subscription}", HttpMethod.Post, PutSubscription, DefaultCodecs, DefaultCodecs);
            RegisterUrlBased(service, "/subscriptions/{stream}/{subscription}", HttpMethod.Delete, DeleteSubscription);

        }

        //private void GetUsers(HttpEntityManager http, UriTemplateMatch match)
        //{
        //    if (_httpForwarder.ForwardRequest(http))
        //        return;
        //    var envelope = CreateReplyEnvelope<UserManagementMessage.AllUserDetailsResult>(http);
        //    var message = new UserManagementMessage.GetAll(envelope, http.User);
        //    Publish(message);
        //}


        //private void GetUser(HttpEntityManager http, UriTemplateMatch match)
        //{
        //    if (_httpForwarder.ForwardRequest(http))
        //        return;
        //    var envelope = CreateReplyEnvelope<UserManagementMessage.UserDetailsResult>(http);
        //    var login = match.BoundVariables["login"];
        //    var message = new UserManagementMessage.Get(envelope, http.User, login);
        //    Publish(message);
        //}

        private void PutSubscription(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var envelope = new SendToHttpEnvelope(
                _networkSendQueue, http,
                (args, message) => http.ResponseCodec.To(message),
                (args,message) =>
                {
                    int code;
                    var m = message as ClientMessage.CreatePersistentSubscriptionCompleted;
                    if(m==null) throw new Exception("unexpected message " + message);
                    switch (m.Result)
                    {
                        case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.Success:
                            code = HttpStatusCode.OK;
                            break;
                        case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.AlreadyExists:
                            code = HttpStatusCode.Conflict;
                            break;
                        case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.AccessDenied:
                            code = HttpStatusCode.Forbidden;
                            break;
                        default:
                            code = HttpStatusCode.InternalServerError;
                            break;
                    }
                    return new ResponseConfiguration(code, http.ResponseCodec.ContentType,
                        http.ResponseCodec.Encoding);
                });
            http.ReadTextRequestAsync(
                (o, s) =>
                {
                    var groupname = match.BoundVariables["subscription"];
                    var stream = match.BoundVariables["stream"];
                    var data = http.RequestCodec.From<PutSubscriptionData>(s);
                    var message = new ClientMessage.CreatePersistentSubscription(Guid.NewGuid(), Guid.NewGuid(),
                                       envelope, stream, groupname, data == null || data.ResolveLinktos, http.User, "", "");
                    Publish(message);
                }, x => Log.DebugException(x, "Reply Text Content Failed."));
        }

        private void DeleteSubscription(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var envelope = new SendToHttpEnvelope(
                _networkSendQueue, http,
                (args, message) => http.ResponseCodec.To(message),
                (args, message) =>
                {
                    return new ResponseConfiguration(HttpStatusCode.OK, http.ResponseCodec.ContentType,
                        http.ResponseCodec.Encoding);
                });
            var groupname = match.BoundVariables["subscription"];
            var stream = match.BoundVariables["stream"];
            var cmd = new ClientMessage.DeletePersistentSubscription(Guid.NewGuid(), Guid.NewGuid(),envelope, stream, groupname, http.User);
            Publish(cmd);
        }

        private class PutSubscriptionData
        {
            public bool ResolveLinktos { get; set; }
        }

    }
}