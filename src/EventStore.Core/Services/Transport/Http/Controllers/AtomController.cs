// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Settings;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Atom;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using Newtonsoft.Json;
using System.Linq;
using System.Threading;
using EventStore.Client.Messages;
using EventStore.Common.Utils;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Util;
using EventStore.Plugins.Authorization;
using Microsoft.Extensions.Primitives;
using static EventStore.Core.Messages.MonitoringMessage;
using static EventStore.Plugins.Authorization.Operations;
using static EventStore.Transport.Http.HttpMethod;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services.Transport.Http.Controllers;

public enum EmbedLevel {
	None,
	Content,
	Rich,
	Body,
	PrettyBody,
	TryHarder
}

public class AtomController : CommunicationController {
	public const char ETagSeparator = ';';
	static readonly char[] ETagSeparatorArray = { ';' };

	private static readonly ILogger Log = Serilog.Log.ForContext<AtomController>();
	private static readonly HtmlFeedCodec HtmlFeedCodec = new HtmlFeedCodec(); // initialization order matters
	private static readonly Func<UriTemplateMatch, Operation> ReadStreamOperation = ForStream(Streams.Read);
	private static readonly Operation RedirectOperation = new Operation(Node.Redirect);
	private static readonly Operation ReadForAllOperation = new Operation(Streams.Read).WithParameter(Streams.Parameters.StreamId(SystemStreams.AllStream));

	private static readonly ICodec[] AtomCodecsWithoutBatches = [
		Codec.EventStoreXmlCodec,
		Codec.EventStoreJsonCodec,
		Codec.Xml,
		Codec.ApplicationXml,
		Codec.Json
	];

	private static readonly ICodec[] AtomCodecs = [
		Codec.DescriptionJson,
		Codec.EventStoreXmlCodec,
		Codec.EventStoreJsonCodec,
		Codec.Xml,
		Codec.ApplicationXml,
		Codec.Json,
		Codec.EventXml,
		Codec.EventJson,
		Codec.EventsXml,
		Codec.EventsJson,
		Codec.Raw
	];

	private static readonly ICodec[] AtomWithHtmlCodecs = [
		Codec.DescriptionJson,
		Codec.EventStoreXmlCodec,
		Codec.EventStoreJsonCodec,
		Codec.Xml,
		Codec.ApplicationXml,
		Codec.Json,
		Codec.EventXml,
		Codec.EventJson,
		Codec.EventsXml,
		Codec.EventsJson,
		HtmlFeedCodec // initialization order matters
	];

	private static readonly ICodec[] DefaultCodecs = [
		Codec.EventStoreXmlCodec,
		Codec.EventStoreJsonCodec,
		Codec.Xml,
		Codec.ApplicationXml,
		Codec.Json,
		Codec.EventXml,
		Codec.EventJson,
		Codec.Raw,
		HtmlFeedCodec // initialization order matters
	];

	private readonly IPublisher _networkSendQueue;
	private readonly TimeSpan _writeTimeout;

	public AtomController(IPublisher publisher, IPublisher networkSendQueue, bool disableHttpCaching, TimeSpan writeTimeout) : base(publisher) {
		_networkSendQueue = networkSendQueue;
		_writeTimeout = writeTimeout;

		if (disableHttpCaching) {
			// ReSharper disable once RedundantNameQualifier
			Transport.Http.Configure.DisableHTTPCaching = true;
		}
	}

	protected override void SubscribeCore(IUriRouter router) {
		// STREAMS
		Register(router, "/streams/{stream}", Post, PostEvents, AtomCodecs, AtomCodecs, ForStream(Streams.Write));
		Register(router, "/streams/{stream}", Delete, DeleteStream, Codec.NoCodecs, AtomCodecs, ForStream(Streams.Delete));
		Register(router, "/streams/{stream}/incoming/{guid}", Post, PostEventsIdempotent, AtomCodecsWithoutBatches, AtomCodecsWithoutBatches, ForStream(Streams.Write));
		Register(router, "/streams/{stream}/", Post, RedirectKeepVerb, AtomCodecs, AtomCodecs, RedirectOperation);
		Register(router, "/streams/{stream}/", Delete, RedirectKeepVerb, Codec.NoCodecs, AtomCodecs, RedirectOperation);
		Register(router, "/streams/{stream}/", Get, RedirectKeepVerb, Codec.NoCodecs, AtomCodecs, RedirectOperation);
		Register(router, "/streams/{stream}?embed={embed}", Get, GetStreamEventsBackward, Codec.NoCodecs, AtomWithHtmlCodecs, ReadStreamOperation);
		Register(router, "/streams/{stream}/{event}?embed={embed}", Get, GetStreamEvent, Codec.NoCodecs, DefaultCodecs, ReadStreamOperation);
		Register(router, "/streams/{stream}/{event}/{count}?embed={embed}", Get, GetStreamEventsBackward, Codec.NoCodecs, AtomWithHtmlCodecs, ReadStreamOperation);
		Register(router, "/streams/{stream}/{event}/backward/{count}?embed={embed}", Get, GetStreamEventsBackward, Codec.NoCodecs, AtomWithHtmlCodecs, ReadStreamOperation);
		RegisterCustom(router, "/streams/{stream}/{event}/forward/{count}?embed={embed}", Get, GetStreamEventsForward, Codec.NoCodecs, AtomWithHtmlCodecs, ReadStreamOperation);

		// METASTREAMS
		Register(router, "/streams/{stream}/metadata", Post, PostMetastreamEvent, AtomCodecs, AtomCodecs, ForStream(Streams.MetadataWrite));
		Register(router, "/streams/{stream}/metadata/", Post, RedirectKeepVerb, AtomCodecs, AtomCodecs, RedirectOperation);

		Register(router, "/streams/{stream}/metadata?embed={embed}", Get, GetMetastreamEvent, Codec.NoCodecs, DefaultCodecs, ForStream(Streams.MetadataRead));
		Register(router, "/streams/{stream}/metadata/?embed={embed}", Get, RedirectKeepVerb, Codec.NoCodecs, DefaultCodecs, RedirectOperation);
		Register(router, "/streams/{stream}/metadata/{event}?embed={embed}", Get, GetMetastreamEvent, Codec.NoCodecs, DefaultCodecs, ForStream(Streams.MetadataRead));

		Register(router, "/streams/{stream}/metadata/{event}/{count}?embed={embed}", Get, GetMetastreamEventsBackward, Codec.NoCodecs, AtomWithHtmlCodecs, ForStream(Streams.MetadataRead));
		Register(router, "/streams/{stream}/metadata/{event}/backward/{count}?embed={embed}", Get, GetMetastreamEventsBackward, Codec.NoCodecs, AtomWithHtmlCodecs, ForStream(Streams.MetadataRead));
		RegisterCustom(router, "/streams/{stream}/metadata/{event}/forward/{count}?embed={embed}", Get, GetMetastreamEventsForward, Codec.NoCodecs, AtomWithHtmlCodecs, ForStream(Streams.MetadataRead));

		// $ALL Filtered
		const string querystring =
			"?embed={embed}&context={context}&type={type}&data={data}&exclude-system-events={exclude-system-events}";

		Register(router, $"/streams/$all/filtered{querystring}", Get, GetAllEventsBackwardFiltered, Codec.NoCodecs, AtomWithHtmlCodecs, ReadForAllOperation);
		Register(router, $"/streams/$all/filtered/{{position}}/{{count}}{querystring}", Get, GetAllEventsBackwardFiltered, Codec.NoCodecs, AtomWithHtmlCodecs, ReadForAllOperation);
		Register(router, $"/streams/$all/filtered/{{position}}/backward/{{count}}{querystring}", Get, GetAllEventsBackwardFiltered, Codec.NoCodecs, AtomWithHtmlCodecs, ReadForAllOperation);
		RegisterCustom(router, $"/streams/$all/filtered/{{position}}/forward/{{count}}{querystring}", Get, GetAllEventsForwardFiltered, Codec.NoCodecs, AtomWithHtmlCodecs, ReadForAllOperation);
		Register(router, $"/streams/%24all/filtered{querystring}", Get, GetAllEventsBackwardFiltered, Codec.NoCodecs, AtomWithHtmlCodecs, ReadForAllOperation);
		Register(router, $"/streams/%24all/filtered/{{position}}/{{count}}{querystring}", Get, GetAllEventsBackwardFiltered, Codec.NoCodecs, AtomWithHtmlCodecs, ReadForAllOperation);
		Register(router, $"/streams/%24all/filtered/{{position}}/backward/{{count}}{querystring}", Get, GetAllEventsBackwardFiltered, Codec.NoCodecs, AtomWithHtmlCodecs, ReadForAllOperation);
		RegisterCustom(router, $"/streams/%24all/filtered/{{position}}/forward/{{count}}{querystring}", Get, GetAllEventsForwardFiltered, Codec.NoCodecs, AtomWithHtmlCodecs, ReadForAllOperation);

		// $ALL
		Register(router, "/streams/$all/", Get, RedirectKeepVerb, Codec.NoCodecs, AtomWithHtmlCodecs, RedirectOperation);
		Register(router, "/streams/%24all/", Get, RedirectKeepVerb, Codec.NoCodecs, AtomWithHtmlCodecs, RedirectOperation);
		Register(router, "/streams/$all?embed={embed}", Get, GetAllEventsBackward, Codec.NoCodecs, AtomWithHtmlCodecs, ReadForAllOperation);
		Register(router, "/streams/$all/{position}/{count}?embed={embed}", Get, GetAllEventsBackward, Codec.NoCodecs, AtomWithHtmlCodecs, ReadForAllOperation);
		Register(router, "/streams/$all/{position}/backward/{count}?embed={embed}", Get, GetAllEventsBackward, Codec.NoCodecs, AtomWithHtmlCodecs, ReadForAllOperation);
		RegisterCustom(router, "/streams/$all/{position}/forward/{count}?embed={embed}", Get, GetAllEventsForward, Codec.NoCodecs, AtomWithHtmlCodecs, ReadForAllOperation);
		Register(router, "/streams/%24all?embed={embed}", Get, GetAllEventsBackward, Codec.NoCodecs, AtomWithHtmlCodecs, ReadForAllOperation);
		Register(router, "/streams/%24all/{position}/{count}?embed={embed}", Get, GetAllEventsBackward, Codec.NoCodecs, AtomWithHtmlCodecs, ReadForAllOperation);
		Register(router, "/streams/%24all/{position}/backward/{count}?embed={embed}", Get, GetAllEventsBackward, Codec.NoCodecs, AtomWithHtmlCodecs, ReadForAllOperation);
		RegisterCustom(router, "/streams/%24all/{position}/forward/{count}?embed={embed}", Get, GetAllEventsForward, Codec.NoCodecs, AtomWithHtmlCodecs, ReadForAllOperation);
	}

	private static Func<UriTemplateMatch, Operation> ForStream(OperationDefinition definition) {
		return match => {
			var operation = new Operation(definition);
			var stream = match.BoundVariables["stream"];
			return !string.IsNullOrEmpty(stream) ? operation.WithParameter(Streams.Parameters.StreamId(stream)) : operation;
		};
	}

	private bool GetDescriptionDocument(HttpEntityManager manager, UriTemplateMatch match) {
		if (manager.ResponseCodec.ContentType != ContentType.DescriptionDocJson) return false;

		var stream = match.BoundVariables["stream"];
		var accepts = (manager.HttpEntity.Request.AcceptTypes?.Length ?? 0) == 0 ||
		              manager.HttpEntity.Request.AcceptTypes.Contains(ContentType.Any);
		var responseStatusCode = accepts ? HttpStatusCode.NotAcceptable : HttpStatusCode.OK;
		var responseMessage = manager.HttpEntity.Request.AcceptTypes == null
			? "We are unable to represent the stream in the format requested."
			: "Description Document";
		var envelope = new SendToHttpEnvelope(
			_networkSendQueue, manager,
			(_, message) => {
				if (message is not GetPersistentSubscriptionStatsCompleted m)
					throw new Exception("Could not get subscriptions for stream " + stream);

				string[] persistentSubscriptionGroups = null;
				if (m.Result == GetPersistentSubscriptionStatsCompleted.OperationStatus.Success) {
					persistentSubscriptionGroups = m.SubscriptionStats.Select(x => x.GroupName).ToArray();
				}

				manager.ReplyTextContent(
					Format.GetDescriptionDocument(manager, stream, persistentSubscriptionGroups),
					responseStatusCode, responseMessage,
					manager.ResponseCodec.ContentType,
					null,
					e => Log.Error(e, "Error while writing HTTP response"));
				return string.Empty;
			},
			(_, _) => new(HttpStatusCode.OK, manager.ResponseCodec.ContentType, manager.ResponseCodec.Encoding));
		var cmd = new GetStreamPersistentSubscriptionStats(envelope, stream);
		Publish(cmd);
		return true;

	}

	private static void RedirectKeepVerb(HttpEntityManager httpEntity, UriTemplateMatch uriTemplateMatch) {
		var original = uriTemplateMatch.RequestUri.ToString();
		KeyValuePair<string, string>[] header = [
			new("Location", original[..^1]),
			new("Cache-Control", "max-age=31536000, public")
		];
		httpEntity.ReplyTextContent("Moved Permanently", HttpStatusCode.RedirectKeepVerb, "", "", header, _ => { });
	}

	// STREAMS
	private void PostEvents(HttpEntityManager manager, UriTemplateMatch match) {
		var stream = match.BoundVariables["stream"];
		if (stream.IsEmptyString()) {
			SendBadRequest(manager, "Invalid request. Stream must be non-empty string");
			return;
		}

		if (!GetIncludedType(manager, out var includedType)) {
			SendBadRequest(manager, $"{SystemHeaders.EventType} header in wrong format.");
			return;
		}

		if (!manager.RequestCodec.HasEventTypes && includedType == null) {
			SendBadRequest(manager, "Must include an event type with the request either in body or as ES-EventType header.");
			return;
		}

		if (!GetIncludedId(manager, out var includedId)) {
			SendBadRequest(manager, $"{SystemHeaders.EventId} header in wrong format.");
			return;
		}

		if (!manager.RequestCodec.HasEventIds && includedId == Guid.Empty) {
			var uri = new Uri(new($"{match.RequestUri}/"), $"incoming/{Guid.NewGuid()}").ToString();
			KeyValuePair<string, string>[] header = [new("Location", uri)];
			manager.ReplyTextContent("Forwarding to idempotent URI", HttpStatusCode.RedirectKeepVerb, "Temporary Redirect", "text/plain", header, _ => { });
			return;
		}

		if (!GetExpectedVersion(manager, out var expectedVersion)) {
			SendBadRequest(manager, $"{SystemHeaders.ExpectedVersion} header in wrong format.");
			return;
		}

		if (!GetRequireLeader(manager, out var requireLeader)) {
			SendBadRequest(manager, $"{SystemHeaders.RequireLeader} header in wrong format.");
			return;
		}

		PostEntry(manager, expectedVersion, requireLeader, stream, includedId, includedType);
	}

	private void PostEventsIdempotent(HttpEntityManager manager, UriTemplateMatch match) {
		var stream = match.BoundVariables["stream"];
		var guid = match.BoundVariables["guid"];
		if (!Guid.TryParse(guid, out var id)) {
			SendBadRequest(manager, "Invalid request. Unable to parse guid");
			return;
		}

		if (stream.IsEmptyString()) {
			SendBadRequest(manager, "Invalid request. Stream must be non-empty string");
			return;
		}

		if (!GetIncludedType(manager, out var includedType)) {
			SendBadRequest(manager, $"{SystemHeaders.EventType} header in wrong format.");
			return;
		}

		if (!GetExpectedVersion(manager, out var expectedVersion)) {
			SendBadRequest(manager, $"{SystemHeaders.ExpectedVersion} header in wrong format.");
			return;
		}

		if (!GetRequireLeader(manager, out var requireLeader)) {
			SendBadRequest(manager, $"{SystemHeaders.RequireLeader} header in wrong format.");
			return;
		}

		PostEntry(manager, expectedVersion, requireLeader, stream, id, includedType);
	}

	private void DeleteStream(HttpEntityManager manager, UriTemplateMatch match) {
		var stream = match.BoundVariables["stream"];
		if (stream.IsEmptyString()) {
			SendBadRequest(manager, $"Invalid stream name '{stream}'");
			return;
		}

		if (!GetExpectedVersion(manager, out var expectedVersion)) {
			SendBadRequest(manager, $"{SystemHeaders.ExpectedVersion} header in wrong format.");
			return;
		}

		if (!GetRequireLeader(manager, out var requireLeader)) {
			SendBadRequest(manager, $"{SystemHeaders.RequireLeader} header in wrong format.");
			return;
		}

		if (!GetHardDelete(manager, out var hardDelete)) {
			SendBadRequest(manager, $"{SystemHeaders.HardDelete} header in wrong format.");
			return;
		}

		var cts = CancellationTokenSource.CreateLinkedTokenSource(manager.HttpEntity.Context.RequestAborted);
		var envelope = new SendToHttpEnvelope(_networkSendQueue, manager, Format.DeleteStreamCompleted, ConfigureResponse);
		var corrId = Guid.NewGuid();
		cts.CancelAfter(_writeTimeout);
		Publish(new ClientMessage.DeleteStream(corrId, corrId, envelope, requireLeader, stream, expectedVersion,
			hardDelete, manager.User, cancellationToken: manager.HttpEntity.Context.RequestAborted));

		ResponseConfiguration ConfigureResponse(HttpResponseConfiguratorArgs args, Message message) {
			cts.Dispose();
			return Configure.DeleteStreamCompleted(args, message);
		}
	}

	private void GetStreamEvent(HttpEntityManager manager, UriTemplateMatch match) {
		var stream = match.BoundVariables["stream"];
		var evNum = match.BoundVariables["event"];

		long eventNumber = -1;
		var embed = GetEmbedLevel(manager, match, EmbedLevel.TryHarder);

		if (stream.IsEmptyString()) {
			SendBadRequest(manager, $"Invalid stream name '{stream}'");
			return;
		}

		if (evNum != "head" && (!long.TryParse(evNum, out eventNumber) || eventNumber < 0)) {
			SendBadRequest(manager, $"'{evNum}' is not valid event number");
			return;
		}

		if (!GetResolveLinkTos(manager, out var resolveLinkTos, true)) {
			SendBadRequest(manager, $"{SystemHeaders.ResolveLinkTos} header in wrong format.");
			return;
		}

		if (!GetRequireLeader(manager, out var requireLeader)) {
			SendBadRequest(manager, $"{SystemHeaders.RequireLeader} header in wrong format.");
			return;
		}

		GetStreamEvent(manager, stream, eventNumber, resolveLinkTos, requireLeader, embed);
	}

	private void GetStreamEventsBackward(HttpEntityManager manager, UriTemplateMatch match) {
		if (GetDescriptionDocument(manager, match))
			return;

		var stream = match.BoundVariables["stream"];
		var evNum = match.BoundVariables["event"];
		var cnt = match.BoundVariables["count"];

		long eventNumber = -1;
		int count = AtomSpecs.FeedPageSize;
		var embed = GetEmbedLevel(manager, match);

		if (stream.IsEmptyString()) {
			SendBadRequest(manager, $"Invalid stream name '{stream}'");
			return;
		}

		if (evNum != null && evNum != "head" && (!long.TryParse(evNum, out eventNumber) || eventNumber < 0)) {
			SendBadRequest(manager, $"'{evNum}' is not valid event number");
			return;
		}

		if (cnt.IsNotEmptyString() && (!int.TryParse(cnt, out count) || count <= 0)) {
			SendBadRequest(manager, $"'{cnt}' is not valid count. Should be positive integer");
			return;
		}

		if (!GetResolveLinkTos(manager, out var resolveLinkTos, true)) {
			SendBadRequest(manager, $"{SystemHeaders.ResolveLinkTos} header in wrong format.");
			return;
		}

		if (!GetRequireLeader(manager, out var requireLeader)) {
			SendBadRequest(manager, $"{SystemHeaders.RequireLeader} header in wrong format.");
			return;
		}

		bool headOfStream = eventNumber == -1;
		GetStreamEventsBackward(manager, stream, eventNumber, count, resolveLinkTos, requireLeader, headOfStream, embed);
	}

	private RequestParams GetStreamEventsForward(HttpEntityManager manager, UriTemplateMatch match) {
		var stream = match.BoundVariables["stream"];
		var evNum = match.BoundVariables["event"];
		var cnt = match.BoundVariables["count"];

		var embed = GetEmbedLevel(manager, match);

		if (stream.IsEmptyString())
			return SendBadRequest(manager, $"Invalid stream name '{stream}'");
		if (evNum.IsEmptyString() || !long.TryParse(evNum, out var eventNumber) || eventNumber < 0)
			return SendBadRequest(manager, $"'{evNum}' is not valid event number");
		if (cnt.IsEmptyString() || !int.TryParse(cnt, out var count) || count <= 0)
			return SendBadRequest(manager, $"'{cnt}' is not valid count. Should be positive integer");
		if (!GetResolveLinkTos(manager, out var resolveLinkTos, true))
			return SendBadRequest(manager, $"{SystemHeaders.ResolveLinkTos} header in wrong format.");
		if (!GetRequireLeader(manager, out var requireLeader))
			return SendBadRequest(manager, $"{SystemHeaders.RequireLeader} header in wrong format.");
		if (!GetLongPoll(manager, out var longPollTimeout))
			return SendBadRequest(manager, $"{SystemHeaders.LongPoll} header in wrong format.");
		var etag = GetETagStreamVersion(manager);

		GetStreamEventsForward(manager, stream, eventNumber, count, resolveLinkTos, requireLeader, etag, longPollTimeout, embed);
		return new RequestParams((longPollTimeout ?? TimeSpan.Zero) + ESConsts.HttpTimeout);
	}

	// METASTREAMS
	private void PostMetastreamEvent(HttpEntityManager manager, UriTemplateMatch match) {
		var stream = match.BoundVariables["stream"];
		if (stream.IsEmptyString() || SystemStreams.IsMetastream(stream)) {
			SendBadRequest(manager, "Invalid request. Stream must be non-empty string and should not be metastream");
			return;
		}

		if (!GetIncludedId(manager, out var includedId)) {
			SendBadRequest(manager, $"{SystemHeaders.EventId} header in wrong format.");
			return;
		}

		GetIncludedType(manager, out var foo);
		if (foo is not (null or SystemEventTypes.StreamMetadata)) {
			SendBadRequest(manager, "Bad Request. You should not include an event type for metadata.");
			return;
		}

		const string includedType = SystemEventTypes.StreamMetadata;
		if (!GetExpectedVersion(manager, out var expectedVersion)) {
			SendBadRequest(manager, $"{SystemHeaders.ExpectedVersion} header in wrong format.");
			return;
		}

		if (!GetRequireLeader(manager, out var requireLeader)) {
			SendBadRequest(manager, $"{SystemHeaders.RequireLeader} header in wrong format.");
			return;
		}

		PostEntry(manager, expectedVersion, requireLeader, SystemStreams.MetastreamOf(stream), includedId, includedType);
	}

	private void GetMetastreamEvent(HttpEntityManager manager, UriTemplateMatch match) {
		var stream = match.BoundVariables["stream"];
		var evNum = match.BoundVariables["event"];

		long eventNumber = -1;
		var embed = GetEmbedLevel(manager, match, EmbedLevel.TryHarder);

		if (stream.IsEmptyString() || SystemStreams.IsMetastream(stream)) {
			SendBadRequest(manager, "Stream must be non-empty string and should not be metastream");
			return;
		}

		if (evNum != null && evNum != "head" && (!long.TryParse(evNum, out eventNumber) || eventNumber < 0)) {
			SendBadRequest(manager, $"'{evNum}' is not valid event number");
			return;
		}

		if (!GetResolveLinkTos(manager, out var resolveLinkTos)) {
			SendBadRequest(manager, $"{SystemHeaders.ResolveLinkTos} header in wrong format.");
			return;
		}

		if (!GetRequireLeader(manager, out var requireLeader)) {
			SendBadRequest(manager, $"{SystemHeaders.RequireLeader} header in wrong format.");
			return;
		}

		GetStreamEvent(manager, SystemStreams.MetastreamOf(stream), eventNumber, resolveLinkTos, requireLeader, embed);
	}

	private void GetMetastreamEventsBackward(HttpEntityManager manager, UriTemplateMatch match) {
		var stream = match.BoundVariables["stream"];
		var evNum = match.BoundVariables["event"];
		var cnt = match.BoundVariables["count"];

		long eventNumber = -1;
		int count = AtomSpecs.FeedPageSize;
		var embed = GetEmbedLevel(manager, match);

		if (stream.IsEmptyString() || SystemStreams.IsMetastream(stream)) {
			SendBadRequest(manager, $"Invalid stream name '{stream}'");
			return;
		}

		if (evNum != null && evNum != "head" && (!long.TryParse(evNum, out eventNumber) || eventNumber < 0)) {
			SendBadRequest(manager, $"'{evNum}' is not valid event number");
			return;
		}

		if (cnt.IsNotEmptyString() && (!int.TryParse(cnt, out count) || count <= 0)) {
			SendBadRequest(manager, $"'{cnt}' is not valid count. Should be positive integer");
			return;
		}

		if (!GetResolveLinkTos(manager, out var resolveLinkTos)) {
			SendBadRequest(manager, $"{SystemHeaders.ResolveLinkTos} header in wrong format.");
			return;
		}

		if (!GetRequireLeader(manager, out var requireLeader)) {
			SendBadRequest(manager, $"{SystemHeaders.RequireLeader} header in wrong format.");
			return;
		}

		bool headOfStream = eventNumber == -1;
		GetStreamEventsBackward(manager, SystemStreams.MetastreamOf(stream), eventNumber, count, resolveLinkTos, requireLeader, headOfStream, embed);
	}

	private RequestParams GetMetastreamEventsForward(HttpEntityManager manager, UriTemplateMatch match) {
		var stream = match.BoundVariables["stream"];
		var evNum = match.BoundVariables["event"];
		var cnt = match.BoundVariables["count"];

		var embed = GetEmbedLevel(manager, match);

		if (stream.IsEmptyString() || SystemStreams.IsMetastream(stream))
			return SendBadRequest(manager, $"Invalid stream name '{stream}'");
		if (evNum.IsEmptyString() || !long.TryParse(evNum, out var eventNumber) || eventNumber < 0)
			return SendBadRequest(manager, $"'{evNum}' is not valid event number");
		if (cnt.IsEmptyString() || !int.TryParse(cnt, out var count) || count <= 0)
			return SendBadRequest(manager, $"'{cnt}' is not valid count. Should be positive integer");
		if (!GetResolveLinkTos(manager, out var resolveLinkTos))
			return SendBadRequest(manager, $"{SystemHeaders.ResolveLinkTos} header in wrong format.");
		if (!GetRequireLeader(manager, out var requireLeader))
			return SendBadRequest(manager, $"{SystemHeaders.RequireLeader} header in wrong format.");
		if (!GetLongPoll(manager, out var longPollTimeout))
			return SendBadRequest(manager, $"{SystemHeaders.LongPoll} header in wrong format.");
		var etag = GetETagStreamVersion(manager);

		GetStreamEventsForward(manager, SystemStreams.MetastreamOf(stream), eventNumber, count, resolveLinkTos,
			requireLeader, etag, longPollTimeout, embed);
		return new RequestParams((longPollTimeout ?? TimeSpan.Zero) + ESConsts.HttpTimeout);
	}

	// $ALL
	private void GetAllEventsBackward(HttpEntityManager manager, UriTemplateMatch match) {
		var pos = match.BoundVariables["position"];
		var cnt = match.BoundVariables["count"];

		TFPos position = TFPos.HeadOfTf;
		int count = AtomSpecs.FeedPageSize;
		var embed = GetEmbedLevel(manager, match);

		if (pos != null && pos != "head" && (!TFPos.TryParse(pos, out position) || position.PreparePosition < 0 || position.CommitPosition < 0)) {
			SendBadRequest(manager, $"Invalid position argument: {pos}");
			return;
		}

		if (cnt.IsNotEmptyString() && (!int.TryParse(cnt, out count) || count <= 0)) {
			SendBadRequest(manager, $"Invalid count argument: {cnt}");
			return;
		}

		if (!GetResolveLinkTos(manager, out var resolveLinkTos)) {
			SendBadRequest(manager, $"{SystemHeaders.ResolveLinkTos} header in wrong format.");
			return;
		}

		if (!GetRequireLeader(manager, out var requireLeader)) {
			SendBadRequest(manager, $"{SystemHeaders.RequireLeader} header in wrong format.");
			return;
		}

		var envelope = new SendToHttpEnvelope(_networkSendQueue,
			manager,
			(args, msg) => Format.ReadAllEventsBackwardCompleted(args, msg, embed),
			(args, msg) => Configure.ReadAllEventsBackwardCompleted(args, msg, position == TFPos.HeadOfTf));
		var corrId = Guid.NewGuid();
		Publish(new ClientMessage.ReadAllEventsBackward(corrId, corrId, envelope,
			position.CommitPosition, position.PreparePosition, count, resolveLinkTos,
			requireLeader, GetETagTFPosition(manager), manager.User));
	}

	private void GetAllEventsBackwardFiltered(HttpEntityManager manager, UriTemplateMatch match) {
		var pos = match.BoundVariables["position"];
		var cnt = match.BoundVariables["count"];

		var (success, errorMessage) = GetFilterFromQueryString(match, true, out var filter);
		if (!success) {
			SendBadRequest(manager, errorMessage);
			return;
		}

		TFPos position = TFPos.HeadOfTf;
		int count = AtomSpecs.FeedPageSize;
		var embed = GetEmbedLevel(manager, match);

		if (pos != null && pos != "head" && (!TFPos.TryParse(pos, out position) || position.PreparePosition < 0 || position.CommitPosition < 0)) {
			SendBadRequest(manager, $"Invalid position argument: {pos}");
			return;
		}

		if (cnt.IsNotEmptyString() && (!int.TryParse(cnt, out count) || count <= 0)) {
			SendBadRequest(manager, $"Invalid count argument: {cnt}");
			return;
		}

		if (!GetRequireLeader(manager, out var requireLeader)) {
			SendBadRequest(manager, $"{SystemHeaders.RequireLeader} header in wrong format.");
			return;
		}

		var envelope = new SendToHttpEnvelope(_networkSendQueue,
			manager,
			(args, msg) => Format.ReadAllEventsBackwardFilteredCompleted(args, msg, embed),
			(args, msg) => Configure.ReadAllEventsBackwardFilteredCompleted(args, msg, position == TFPos.HeadOfTf));
		var corrId = Guid.NewGuid();
		Publish(new ClientMessage.FilteredReadAllEventsBackward(corrId, corrId, envelope,
			position.CommitPosition, position.PreparePosition, count,
			requireLeader, true, count, GetETagTFPosition(manager), filter, manager.User));
	}

	private RequestParams GetAllEventsForward(HttpEntityManager manager, UriTemplateMatch match) {
		var pos = match.BoundVariables["position"];
		var cnt = match.BoundVariables["count"];

		var embed = GetEmbedLevel(manager, match);

		if (!TFPos.TryParse(pos, out var position) || position.PreparePosition < 0 || position.CommitPosition < 0)
			return SendBadRequest(manager, $"Invalid position argument: {pos}");
		if (!int.TryParse(cnt, out var count) || count <= 0)
			return SendBadRequest(manager, $"Invalid count argument: {cnt}");
		if (!GetResolveLinkTos(manager, out var resolveLinkTos))
			return SendBadRequest(manager, $"{SystemHeaders.ResolveLinkTos} header in wrong format.");
		if (!GetRequireLeader(manager, out var requireLeader))
			return SendBadRequest(manager, $"{SystemHeaders.RequireLeader} header in wrong format.");
		if (!GetLongPoll(manager, out var longPollTimeout))
			return SendBadRequest(manager, $"{SystemHeaders.LongPoll} header in wrong format.");

		var envelope = new SendToHttpEnvelope(_networkSendQueue,
			manager,
			(args, msg) => Format.ReadAllEventsForwardCompleted(args, msg, embed),
			(args, msg) => Configure.ReadAllEventsForwardCompleted(args, msg, headOfTf: false));
		var corrId = Guid.NewGuid();
		Publish(new ClientMessage.ReadAllEventsForward(corrId, corrId, envelope,
			position.CommitPosition, position.PreparePosition, count, resolveLinkTos,
			requireLeader, GetETagTFPosition(manager), manager.User,
			replyOnExpired: false,
			longPollTimeout: longPollTimeout));
		return new RequestParams((longPollTimeout ?? TimeSpan.Zero) + ESConsts.HttpTimeout);
	}

	private RequestParams GetAllEventsForwardFiltered(HttpEntityManager manager, UriTemplateMatch match) {
		var pos = match.BoundVariables["position"];
		var cnt = match.BoundVariables["count"];

		var (success, errorMessage) = GetFilterFromQueryString(match, true, out var filter);
		if (!success) {
			return SendBadRequest(manager, errorMessage);
		}

		var embed = GetEmbedLevel(manager, match);

		if (!TFPos.TryParse(pos, out var position) || position.PreparePosition < 0 || position.CommitPosition < 0)
			return SendBadRequest(manager, $"Invalid position argument: {pos}");
		if (!int.TryParse(cnt, out var count) || count <= 0)
			return SendBadRequest(manager, $"Invalid count argument: {cnt}");
		if (!GetRequireLeader(manager, out var requireLeader))
			return SendBadRequest(manager, $"{SystemHeaders.RequireLeader} header in wrong format.");
		if (!GetLongPoll(manager, out var longPollTimeout))
			return SendBadRequest(manager, $"{SystemHeaders.LongPoll} header in wrong format.");

		var envelope = new SendToHttpEnvelope(_networkSendQueue,
			manager,
			(args, msg) => Format.ReadAllEventsForwardFilteredCompleted(args, msg, embed),
			(args, msg) => Configure.ReadAllEventsForwardFilteredCompleted(args, msg, headOfTf: false));
		var corrId = Guid.NewGuid();
		Publish(new ClientMessage.FilteredReadAllEventsForward(corrId, corrId, envelope,
			position.CommitPosition, position.PreparePosition, count, true,
			requireLeader, 1000, GetETagTFPosition(manager), filter, manager.User,
			replyOnExpired: false,
			longPollTimeout: longPollTimeout));
		return new RequestParams((longPollTimeout ?? TimeSpan.Zero) + ESConsts.HttpTimeout);
	}

	// HELPERS
	private (bool Success, string ErrorMessage) GetFilterFromQueryString(UriTemplateMatch match, bool isAllStream, out IEventFilter filter) {
		var context = match.BoundVariables["context"];
		var type = match.BoundVariables["type"];
		var data = match.BoundVariables["data"];
		var excludeSystemEvents = match.BoundVariables["exclude-system-events"];

		if (excludeSystemEvents != null) {
			if (!bool.TryParse(excludeSystemEvents, out var parsedExcludeSystemEvents)) {
				filter = null;
				return (false, "exclude-sytem-events should have a value of true.");
			}

			if (parsedExcludeSystemEvents == false) {
				filter = null;
				return (false, "exclude-sytem-events should have a value of true.");
			}

			filter = EventFilter.Get(isAllStream, new Filter(
				Filter.Types.FilterContext.EventType,
				Filter.Types.FilterType.Regex,
				[@"^[^\$].*"]
			));

			return (true, null);
		}

		var parsedFilterResult = EventFilter.TryParse(context, isAllStream, type, data, out filter);
		return !parsedFilterResult.Success ? (false, parsedFilterResult.Reason) : (true, null);
	}

	private static bool GetExpectedVersion(HttpEntityManager manager, out long expectedVersion) {
		var expVer = manager.HttpEntity.Request.GetHeaderValues(SystemHeaders.ExpectedVersion);
		if (StringValues.IsNullOrEmpty(expVer)) {
			expectedVersion = ExpectedVersion.Any;
			return true;
		}

		return long.TryParse(expVer, out expectedVersion) && expectedVersion >= ExpectedVersion.StreamExists;
	}

	private static bool GetIncludedId(HttpEntityManager manager, out Guid includedId) {
		var id = manager.HttpEntity.Request.GetHeaderValues(SystemHeaders.EventId);
		if (StringValues.IsNullOrEmpty(id)) {
			includedId = Guid.Empty;
			return true;
		}

		return Guid.TryParse(id, out includedId) && includedId != Guid.Empty;
	}

	private static bool GetIncludedType(HttpEntityManager manager, out string includedType) {
		var type = manager.HttpEntity.Request.GetHeaderValues(SystemHeaders.EventType);
		if (StringValues.IsNullOrEmpty(type)) {
			includedType = null;
			return true;
		}

		includedType = type;
		return true;
	}


	private static bool GetRequireLeader(HttpEntityManager manager, out bool requireLeader) {
		requireLeader = false;

		var onlyLeader = manager.HttpEntity.Request.GetHeaderValues(SystemHeaders.RequireLeader);
		var onlyMaster = manager.HttpEntity.Request.GetHeaderValues(SystemHeaders.RequireMaster);

		if (StringValues.IsNullOrEmpty(onlyLeader) && StringValues.IsNullOrEmpty(onlyMaster))
			return true;

		if (string.Equals(onlyLeader, "True", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(onlyMaster, "True", StringComparison.OrdinalIgnoreCase)) {
			requireLeader = true;
			return true;
		}

		return string.Equals(onlyLeader, "False", StringComparison.OrdinalIgnoreCase) ||
		       string.Equals(onlyMaster, "False", StringComparison.OrdinalIgnoreCase);
	}

	private static bool GetLongPoll(HttpEntityManager manager, out TimeSpan? longPollTimeout) {
		longPollTimeout = null;
		var longPollHeader = manager.HttpEntity.Request.GetHeaderValues(SystemHeaders.LongPoll);
		if (StringValues.IsNullOrEmpty(longPollHeader))
			return true;
		if (int.TryParse(longPollHeader, out var longPollSec) && longPollSec > 0) {
			longPollTimeout = TimeSpan.FromSeconds(longPollSec);
			return true;
		}

		return false;
	}

	private static bool GetResolveLinkTos(HttpEntityManager manager, out bool resolveLinkTos, bool defaultOption = false) {
		resolveLinkTos = defaultOption;
		var linkToHeader = manager.HttpEntity.Request.GetHeaderValues(SystemHeaders.ResolveLinkTos);
		if (StringValues.IsNullOrEmpty(linkToHeader))
			return true;
		if (string.Equals(linkToHeader, "False", StringComparison.OrdinalIgnoreCase)) {
			return true;
		}

		if (string.Equals(linkToHeader, "True", StringComparison.OrdinalIgnoreCase)) {
			resolveLinkTos = true;
			return true;
		}

		return false;
	}

	private static bool GetHardDelete(HttpEntityManager manager, out bool hardDelete) {
		hardDelete = false;
		var hardDel = manager.HttpEntity.Request.GetHeaderValues(SystemHeaders.HardDelete);
		if (StringValues.IsNullOrEmpty(hardDel))
			return true;
		if (string.Equals(hardDel, "True", StringComparison.OrdinalIgnoreCase)) {
			hardDelete = true;
			return true;
		}

		return string.Equals(hardDel, "False", StringComparison.OrdinalIgnoreCase);
	}

	void PostEntry(HttpEntityManager manager, long expectedVersion, bool requireLeader, string stream,
		Guid idIncluded, string typeIncluded) {
		//TODO GFY SHOULD WE MAKE THIS READ BYTE[] FOR RAW THEN CONVERT? AS OF NOW ITS ALL NO BOM UTF8
		manager.ReadRequestAsync(
			(man, body) => {
				Event[] events;
				try {
					events = AutoEventConverter.SmartParse(body, manager.RequestCodec, idIncluded, typeIncluded);
				} catch (Exception ex) {
					SendBadRequest(manager, ex.Message);
					return;
				}

				if (events.IsEmpty()) {
					SendBadRequest(manager, "Write request body invalid.");
					return;
				}

				foreach (var e in events) {
					if (e.Data.Length + e.Metadata.Length > 4 * 1024 * 1024) {
						SendTooBig(manager);
					}
				}

				var cts = CancellationTokenSource.CreateLinkedTokenSource(manager.HttpEntity.Context.RequestAborted);
				var envelope = new SendToHttpEnvelope(_networkSendQueue, manager, Format.WriteEventsCompleted, ConfigureResponse);
				var corrId = Guid.NewGuid();
				var msg = new ClientMessage.WriteEvents(corrId, corrId, envelope, requireLeader, stream,
					expectedVersion, events, manager.User,
					cancellationToken: manager.HttpEntity.Context.RequestAborted);
				cts.CancelAfter(_writeTimeout);
				Publish(msg);

				ResponseConfiguration ConfigureResponse(HttpResponseConfiguratorArgs a, Message m) {
					cts.Dispose();
					return Configure.WriteEventsCompleted(a, m, stream);
				}

			},
			e => Log.Debug("Error while reading request (POST entry): {e}.", e.Message));

	}

	private void GetStreamEvent(HttpEntityManager manager, string stream, long eventNumber,
		bool resolveLinkTos, bool requireLeader, EmbedLevel embed) {
		var envelope = new SendToHttpEnvelope(_networkSendQueue,
			manager,
			(args, message) => Format.EventEntry(args, message, embed),
			(args, message) => Configure.EventEntry(args, message, headEvent: eventNumber == -1));
		var corrId = Guid.NewGuid();
		Publish(new ClientMessage.ReadEvent(corrId, corrId, envelope, stream, eventNumber, resolveLinkTos,
			requireLeader, manager.User));
	}

	private void GetStreamEventsBackward(HttpEntityManager manager, string stream, long eventNumber, int count,
		bool resolveLinkTos, bool requireLeader, bool headOfStream, EmbedLevel embed) {
		var envelope = new SendToHttpEnvelope(_networkSendQueue,
			manager,
			(ent, msg) => Format.GetStreamEventsBackward(ent, msg, embed, headOfStream),
			(args, msg) => Configure.GetStreamEventsBackward(args, msg, headOfStream));
		var corrId = Guid.NewGuid();
		Publish(new ClientMessage.ReadStreamEventsBackward(corrId, corrId, envelope, stream, eventNumber, count,
			resolveLinkTos, requireLeader, GetETagStreamVersion(manager), manager.User));
	}

	private void GetStreamEventsForward(HttpEntityManager manager, string stream, long eventNumber, int count,
		bool resolveLinkTos, bool requireLeader, long? etag, TimeSpan? longPollTimeout, EmbedLevel embed) {
		var envelope = new SendToHttpEnvelope(_networkSendQueue,
			manager,
			(ent, msg) => Format.GetStreamEventsForward(ent, msg, embed),
			Configure.GetStreamEventsForward);
		var corrId = Guid.NewGuid();
		Publish(new ClientMessage.ReadStreamEventsForward(corrId, corrId, envelope, stream, eventNumber, count,
			resolveLinkTos, requireLeader, etag, manager.User,
			replyOnExpired: false,
			longPollTimeout: longPollTimeout));
	}

	private long? GetETagStreamVersion(HttpEntityManager manager) {
		var etag = manager.HttpEntity.Request.GetHeaderValues("If-None-Match");
		if (StringValues.IsNullOrEmpty(etag)) return null;
		// etag format is version;contenttypehash
		var splitted = etag.ToString().Trim('\"').Split(ETagSeparatorArray);
		if (splitted.Length != 2) return null;
		var typeHash = manager.ResponseCodec.ContentType.GetHashCode().ToString(CultureInfo.InvariantCulture);
		var res = splitted[1] == typeHash && long.TryParse(splitted[0], out var streamVersion) ? (long?)streamVersion : null;
		return res;
	}

	private static long? GetETagTFPosition(HttpEntityManager manager) {
		var etag = manager.HttpEntity.Request.GetHeaderValues("If-None-Match");
		if (StringValues.IsNullOrEmpty(etag)) return null;
		// etag format is version;contenttypehash
		var splitted = etag.ToString().Trim('\"').Split(ETagSeparatorArray);
		if (splitted.Length != 2) return null;
		var typeHash = manager.ResponseCodec.ContentType.GetHashCode().ToString(CultureInfo.InvariantCulture);
		return splitted[1] == typeHash && long.TryParse(splitted[0], out var tfEofPosition) ? tfEofPosition : null;
	}

	private static EmbedLevel GetEmbedLevel(HttpEntityManager manager, UriTemplateMatch match, EmbedLevel htmlLevel = EmbedLevel.PrettyBody) {
		if (manager.ResponseCodec is IRichAtomCodec)
			return htmlLevel;
		var rawValue = match.BoundVariables["embed"] ?? string.Empty;
		return rawValue.ToLowerInvariant() switch {
			"content" => EmbedLevel.Content,
			"rich" => EmbedLevel.Rich,
			"body" => EmbedLevel.Body,
			"pretty" => EmbedLevel.PrettyBody,
			"tryharder" => EmbedLevel.TryHarder,
			_ => EmbedLevel.None
		};
	}
}

internal class HtmlFeedCodec : ICodec, IRichAtomCodec {
	public string ContentType => "text/html";

	public Encoding Encoding => Helper.UTF8NoBom;

	public bool HasEventIds => false;

	public bool HasEventTypes => false;

	public bool CanParse(MediaType format) {
		throw new NotImplementedException();
	}

	public bool SuitableForResponse(MediaType component) {
		return component.Type == "*"
			   || (string.Equals(component.Type, "text", StringComparison.OrdinalIgnoreCase)
				   && (component.Subtype == "*" ||
					   string.Equals(component.Subtype, "html", StringComparison.OrdinalIgnoreCase)));
	}

	public T From<T>(string text) {
		throw new NotImplementedException();
	}

	public string To<T>(T value) =>
		$$"""
		  <!DOCTYPE html>
		  <html>
		  <head>
		  </head>
		  <body>
		  <script>
		  var data = {{JsonConvert.SerializeObject(value, Formatting.Indented, JsonCodec.ToSettings)}};
		  var newLocation = '/web/index.html#/streams/' + data.streamId;
		  if('positionEventNumber' in data){ newLocation = newLocation + '/' + data.positionEventNumber; }
		  window.location.replace(newLocation);
		  </script>
		  </body>
		  </html>
		  """;
}

interface IRichAtomCodec;
