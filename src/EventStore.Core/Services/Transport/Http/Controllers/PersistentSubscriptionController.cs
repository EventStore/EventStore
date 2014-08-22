using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Common.Log;
using EventStore.Core.Messaging;
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
            Register(service, "/subscriptions/{stream}/{subscription}", HttpMethod.Get, GetSubscriptionInfo, Codec.NoCodecs, DefaultCodecs);
            Register(service, "/subscriptions/{stream}", HttpMethod.Get, GetSubscriptionInfoForStream, Codec.NoCodecs, DefaultCodecs);
            Register(service, "/subscriptions", HttpMethod.Get, GetAllSubscriptionInfo, Codec.NoCodecs, DefaultCodecs);
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
            var groupname = match.BoundVariables["subscription"];
            var stream = match.BoundVariables["stream"];
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
                            code = HttpStatusCode.Created;
                            //TODO competing return uri to subscription
                            break;
                        case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.AlreadyExists:
                            code = HttpStatusCode.Conflict;
                            break;
                        case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.AccessDenied:
                            code = HttpStatusCode.Unauthorized;
                            break;
                        default:
                            code = HttpStatusCode.InternalServerError;
                            break;
                    }
                    return new ResponseConfiguration(code, http.ResponseCodec.ContentType,
                        http.ResponseCodec.Encoding, new KeyValuePair<string, string>("location", MakeUrl(http, "/subscriptions/" + stream + "/" + groupname)));
                });
            http.ReadTextRequestAsync(
                (o, s) =>
                {
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
                    int code;
                    var m = message as ClientMessage.DeletePersistentSubscriptionCompleted;
                    if (m == null) throw new Exception("unexpected message " + message);
                    switch (m.Result)
                    {
                        case ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.Success:
                            code = HttpStatusCode.OK;
                            break;
                        case ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.DoesNotExist:
                            code = HttpStatusCode.NotFound;
                            break;
                        case ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.AccessDenied:
                            code = HttpStatusCode.Unauthorized;
                            break;
                        default:
                            code = HttpStatusCode.InternalServerError;
                            break;
                    }

                    return new ResponseConfiguration(code, http.ResponseCodec.ContentType,
                        http.ResponseCodec.Encoding);
                });
            var groupname = match.BoundVariables["subscription"];
            var stream = match.BoundVariables["stream"];
            var cmd = new ClientMessage.DeletePersistentSubscription(Guid.NewGuid(), Guid.NewGuid(),envelope, stream, groupname, http.User);
            Publish(cmd);
        }

        private void GetAllSubscriptionInfo(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var envelope = new SendToHttpEnvelope(
                _networkSendQueue, http,
                (args, message) => http.ResponseCodec.To(ToDto(message as MonitoringMessage.GetPersistentSubscriptionStatsCompleted)),
                (args, message) => StatsConfiguration(http, message));
            var cmd = new MonitoringMessage.GetAllPersistentSubscriptionStats(envelope);
            Publish(cmd);
        }



        private void GetSubscriptionInfoForStream(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var stream = match.BoundVariables["stream"];
            var envelope = new SendToHttpEnvelope(
                _networkSendQueue, http,
                (args, message) => http.ResponseCodec.To(ToDto(message as MonitoringMessage.GetPersistentSubscriptionStatsCompleted)),
                (args, message) => StatsConfiguration(http, message));
            var cmd = new MonitoringMessage.GetStreamPersistentSubscriptionStats(envelope, stream);
            Publish(cmd);
        }

        private void GetSubscriptionInfo(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var stream = match.BoundVariables["stream"];
            var groupName = match.BoundVariables["subscription"];
            var envelope = new SendToHttpEnvelope(
                _networkSendQueue, http,
                (args, message) => http.ResponseCodec.To(ToDto(message as MonitoringMessage.GetPersistentSubscriptionStatsCompleted).FirstOrDefault()),
                (args, message) => StatsConfiguration(http, message));
            var cmd = new MonitoringMessage.GetPersistentSubscriptionStats(envelope, stream, groupName);
            Publish(cmd);
        }


        private static ResponseConfiguration StatsConfiguration(HttpEntityManager http, Message message)
        {
            int code;
            var m = message as MonitoringMessage.GetPersistentSubscriptionStatsCompleted;
            if (m == null) throw new Exception("unexpected message " + message);
            switch (m.Result)
            {
                case MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.Success:
                    code = HttpStatusCode.OK;
                    break;
                case MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.NotFound:
                    code = HttpStatusCode.NotFound;
                    break;
                case MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.NotReady:
                    code = HttpStatusCode.ServiceUnavailable;
                    break;
                default:
                    code = HttpStatusCode.InternalServerError;
                    break;
            }

            return new ResponseConfiguration(code, http.ResponseCodec.ContentType,
                http.ResponseCodec.Encoding);
        }

        private IEnumerable<SubscriptionInfo> ToDto(MonitoringMessage.GetPersistentSubscriptionStatsCompleted message)
        {
            if (message == null) yield break;
            if (message.SubscriptionStats == null) yield break;
            foreach (var stat in message.SubscriptionStats)
            {
                var info = new SubscriptionInfo
                {
                    EventStreamId = stat.EventStreamId,
                    GroupName = stat.GroupName,
                    Status = stat.Status,
                    AverageItemsPerSecond = stat.AveragePerSecond,
                    TotalItemsProcessed = stat.TotalItems,
                    CountSinceLastMeasurement = stat.CountSinceLastMeasurement,
                    Connections = new List<ConnectionInfo>()
                };
                if (stat.Connections != null)
                {
                    foreach (var connection in stat.Connections)
                    {
                        info.Connections.Add(new ConnectionInfo
                        {
                            Username = connection.Username, 
                            From = connection.From,
                            AverageItemsPerSecond = connection.AverageItemsPerSecond,
                            CountSinceLastMeasurement = connection.CountSinceLastMeasurement,
                            TotalItemsProcessed = connection.TotalItems
                        });
                    }
                }
                yield return info;
            }
        }

        private class PutSubscriptionData
        {
            public bool ResolveLinktos { get; set; }
        }

        private class SubscriptionInfo
        {
            public string EventStreamId { get; set; }
            public string GroupName { get; set; }
            public string Status { get; set; }
            public decimal AverageItemsPerSecond { get; set; }
            public long TotalItemsProcessed { get; set; }
            public long CountSinceLastMeasurement { get; set; }
            public List<ConnectionInfo> Connections { get; set; } 
        }

        private class ConnectionInfo
        {
            public string From { get; set; }
            public string Username { get; set; }
            public decimal AverageItemsPerSecond { get; set; }
            public long TotalItemsProcessed { get; set; }
            public long CountSinceLastMeasurement { get; set; }
        }
    }
}