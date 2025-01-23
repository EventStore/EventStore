// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Messages;
using EventStore.Core.Metrics;
using EventStore.Plugins.Authorization;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Client;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using HttpMethod = EventStore.Transport.Http.HttpMethod;
using HttpStatusCode = EventStore.Transport.Http.HttpStatusCode;

namespace EventStore.Core.Services.Transport.Http.Controllers;

public class GossipController(IPublisher publisher, IPublisher networkSendQueue, IDurationTracker tracker) : CommunicationController(publisher) {
	private static readonly ICodec[] SupportedCodecs = [Codec.Json, Codec.ApplicationXml, Codec.Xml, Codec.Text];

	protected override void SubscribeCore(IUriRouter router) {
		router.RegisterAction(new("/gossip", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, new Operation(Operations.Node.Gossip.ClientRead)), OnGetGossip);
	}

	private void OnGetGossip(HttpEntityManager entity, UriTemplateMatch match) {
		var duration = tracker.Start();
		var sendToHttpEnvelope = new SendToHttpEnvelope(
			networkSendQueue, entity, Format.SendPublicGossip,
			(e, _) => {
				duration.Dispose();
				return Configure.Ok(e.ResponseCodec.ContentType, Helper.UTF8NoBom, null, null, false);
			});
		Publish(new GossipMessage.ClientGossip(sendToHttpEnvelope));
	}
}
