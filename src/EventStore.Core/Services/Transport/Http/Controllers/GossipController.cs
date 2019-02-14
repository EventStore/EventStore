using System;
using System.Net;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Client;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using HttpStatusCode = EventStore.Transport.Http.HttpStatusCode;

namespace EventStore.Core.Services.Transport.Http.Controllers {
	public class GossipController : CommunicationController,
		IHttpSender,
		ISender<GossipMessage.SendGossip> {
		private static readonly ILogger Log = LogManager.GetLoggerFor<GossipController>();

		private static readonly ICodec[] SupportedCodecs = new ICodec[]
			{Codec.Json, Codec.ApplicationXml, Codec.Xml, Codec.Text};

		private readonly IPublisher _networkSendQueue;
		private readonly HttpAsyncClient _client;
		private readonly TimeSpan _gossipTimeout;

		public GossipController(IPublisher publisher, IPublisher networkSendQueue, TimeSpan gossipTimeout)
			: base(publisher) {
			_networkSendQueue = networkSendQueue;
			_gossipTimeout = gossipTimeout;
			_client = new HttpAsyncClient(_gossipTimeout);
		}

		protected override void SubscribeCore(IHttpService service) {
			service.RegisterAction(new ControllerAction("/gossip", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs),
				OnGetGossip);
			if (service.Accessibility == ServiceAccessibility.Private)
				service.RegisterAction(
					new ControllerAction("/gossip", HttpMethod.Post, SupportedCodecs, SupportedCodecs), OnPostGossip);
		}

		public void SubscribeSenders(HttpMessagePipe pipe) {
// ReSharper disable RedundantTypeArgumentsOfMethod
			pipe.RegisterSender<GossipMessage.SendGossip>(this);
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

		private void OnPostGossip(HttpEntityManager entity, UriTemplateMatch match) {
			entity.ReadTextRequestAsync(OnPostGossipRequestRead,
				e => Log.Debug("Error while reading request (gossip): {e}", e.Message));
		}

		private void OnPostGossipRequestRead(HttpEntityManager manager, string body) {
			var clusterInfoDto = manager.RequestCodec.From<ClusterInfoDto>(body);
			if (clusterInfoDto == null) {
				var msg = string.Format(
					"Received as POST invalid ClusterInfo from [{0}]. Content-Type: {1}, Body:\n{2}.",
					manager.RequestedUrl, manager.RequestCodec.ContentType, body);
				Log.Error("Received as POST invalid ClusterInfo from [{requestedUrl}]. Content-Type: {contentType}.",
					manager.RequestedUrl, manager.RequestCodec.ContentType);
				Log.Error("Received as POST invalid ClusterInfo from [{requestedUrl}]. Body: {body}.",
					manager.RequestedUrl, body);
				SendBadRequest(manager, msg);
				return;
			}

			var sendToHttpEnvelope = new SendToHttpEnvelope(_networkSendQueue,
				manager,
				Format.SendGossip,
				(e, m) => Configure.Ok(e.ResponseCodec.ContentType));
			var serverEndPoint = TryGetServerEndPoint(clusterInfoDto);
			Publish(new GossipMessage.GossipReceived(sendToHttpEnvelope, new ClusterInfo(clusterInfoDto),
				serverEndPoint));
		}

		private static IPEndPoint TryGetServerEndPoint(ClusterInfoDto clusterInfoDto) {
			IPEndPoint serverEndPoint = null;
			IPAddress serverAddress;
			if (IPAddress.TryParse(clusterInfoDto.ServerIp, out serverAddress)
			    && clusterInfoDto.ServerPort > 0
			    && clusterInfoDto.ServerPort <= 65535) {
				serverEndPoint = new IPEndPoint(serverAddress, clusterInfoDto.ServerPort);
			}

			return serverEndPoint;
		}

		private void OnGetGossip(HttpEntityManager entity, UriTemplateMatch match) {
			var sendToHttpEnvelope = new SendToHttpEnvelope(
				_networkSendQueue, entity, Format.SendGossip,
				(e, m) => Configure.Ok(e.ResponseCodec.ContentType, Helper.UTF8NoBom, null, null, false));
			Publish(new GossipMessage.GossipReceived(sendToHttpEnvelope, new ClusterInfo(new MemberInfo[0]), null));
		}
	}
}
