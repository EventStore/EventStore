using System;
using System.Net;
using System.Net.Http;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Client;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using HttpMethod = EventStore.Transport.Http.HttpMethod;
using HttpStatusCode = EventStore.Transport.Http.HttpStatusCode;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services.Transport.Http.Controllers {
	public class GossipController : CommunicationController,
		IHttpSender,
		ISender<GossipMessage.SendGossip>,
		ISender<GossipMessage.GetGossip>{
		private static readonly ILogger Log = Serilog.Log.ForContext<GossipController>();

		private static readonly ICodec[] SupportedCodecs = new ICodec[]
			{Codec.Json, Codec.ApplicationXml, Codec.Xml, Codec.Text};

		private readonly IPublisher _networkSendQueue;
		private readonly HttpAsyncClient _client;
		private readonly TimeSpan _gossipTimeout;

		public GossipController(IPublisher publisher, IPublisher networkSendQueue, TimeSpan gossipTimeout, HttpMessageHandler httpMessageHandler)
			: base(publisher) {
			_networkSendQueue = networkSendQueue;
			_gossipTimeout = gossipTimeout;
			_client = new HttpAsyncClient(_gossipTimeout, httpMessageHandler);
		}

		protected override void SubscribeCore(IHttpService service) {
			service.RegisterAction(new ControllerAction("/gossip", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, AuthorizationLevel.None),
				OnGetGossip);
		}

		public void SubscribeSenders(HttpMessagePipe pipe) {
// ReSharper disable RedundantTypeArgumentsOfMethod
			pipe.RegisterSender<GossipMessage.SendGossip>(this);
			pipe.RegisterSender<GossipMessage.GetGossip>(this);
// ReSharper restore RedundantTypeArgumentsOfMethod
		}

		public void Send(GossipMessage.SendGossip message, IPEndPoint endPoint) {
			Ensure.NotNull(message, "message");
			Ensure.NotNull(message, "endPoint");

			var url = endPoint.ToHttpUrl(EndpointExtensions.HTTP_SCHEMA, "/gossip");
			_client.Post(
				url,
				Codec.Json.To(new ClusterInfoDto(message.ClusterInfo, message.ServerEndPoint)),
				Codec.Json.ContentType,
				response => {
					if (response.HttpStatusCode != HttpStatusCode.OK) {
						Publish(new GossipMessage.GossipSendFailed(
							string.Format("Received HTTP status code {0}.", response.HttpStatusCode), endPoint));
						return;
					}

					var clusterInfo = Codec.Json.From<ClusterInfoDto>(response.Body);
					if (clusterInfo == null) {
						var msg = string.Format(
							"Received as RESPONSE invalid ClusterInfo from [{0}]. Content-Type: {1}, Body:\n{2}.",
							url, response.ContentType, response.Body);
						Log.Error("Received as RESPONSE invalid ClusterInfo from [{url}]. Content-Type: {contentType}.",
							url, response.ContentType);
						Log.Error("Received as RESPONSE invalid ClusterInfo from [{url}]. Body: {body}.",
							url, response.Body);
						Publish(new GossipMessage.GossipSendFailed(msg, endPoint));
						return;
					}

					Publish(
						new GossipMessage.GossipReceived(new NoopEnvelope(), new ClusterInfo(clusterInfo), endPoint));
				},
				error => Publish(new GossipMessage.GossipSendFailed(error.Message, endPoint)));
		}
		
		public void Send(GossipMessage.GetGossip message, IPEndPoint endPoint) {
			Ensure.NotNull(message, "message");
			Ensure.NotNull(message, "endPoint");

			var url = endPoint.ToHttpUrl(EndpointExtensions.HTTP_SCHEMA, "/gossip");
			_client.Get(
				url,
				response => {
					if (response.HttpStatusCode != HttpStatusCode.OK) {
						Publish(new GossipMessage.GetGossipFailed(
							string.Format("Received HTTP status code {0}.", response.HttpStatusCode), endPoint));
						return;
					}

					var clusterInfo = Codec.Json.From<ClusterInfoDto>(response.Body);
					if (clusterInfo == null) {
						var msg = string.Format(
							"Received as RESPONSE invalid ClusterInfo from [{0}]. Content-Type: {1}, Body:\n{2}.",
							url, response.ContentType, response.Body);
						Log.Error("Received as RESPONSE invalid ClusterInfo from [{url}]. Content-Type: {contentType}.",
							url, response.ContentType);
						Log.Error("Received as RESPONSE invalid ClusterInfo from [{url}]. Body: {body}.",
							url, response.Body);
						Publish(new GossipMessage.GetGossipFailed(msg, endPoint));
						return;
					}

					Publish(
						new GossipMessage.GetGossipReceived(new ClusterInfo(clusterInfo), endPoint));
				},
				error => Publish(new GossipMessage.GetGossipFailed(error.Message, endPoint)));
		}

		private void OnGetGossip(HttpEntityManager entity, UriTemplateMatch match) {
			var sendToHttpEnvelope = new SendToHttpEnvelope(
				_networkSendQueue, entity, Format.SendGossip,
				(e, m) => Configure.Ok(e.ResponseCodec.ContentType, Helper.UTF8NoBom, null, null, false));
			Publish(new GossipMessage.GossipReceived(sendToHttpEnvelope, new ClusterInfo(new MemberInfo[0]), null));
		}
	}
}
