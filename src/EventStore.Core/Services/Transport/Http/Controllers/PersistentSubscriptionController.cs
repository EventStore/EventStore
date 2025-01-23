// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using ClientMessages = EventStore.Core.Messages.ClientMessage.PersistentSubscriptionNackEvents;
using EventStore.Core.Services.PersistentSubscription;
using EventStore.Common.Utils;
using EventStore.Plugins.Authorization;
using EventStore.Core.Settings;
using EventStore.Transport.Http.Atom;
using Microsoft.Extensions.Primitives;
using static EventStore.Core.Messages.ClientMessage;
using static EventStore.Core.Messages.ClientMessage.CreatePersistentSubscriptionToStreamCompleted;
using static EventStore.Core.Messages.ClientMessage.DeletePersistentSubscriptionToStreamCompleted;
using static EventStore.Core.Messages.ClientMessage.ReadNextNPersistentMessagesCompleted;
using static EventStore.Core.Messages.ClientMessage.UpdatePersistentSubscriptionToStreamCompleted;
using static EventStore.Core.Messages.MonitoringMessage;
using static EventStore.Core.Messages.MonitoringMessage.GetPersistentSubscriptionStatsCompleted;
using static EventStore.Core.Messages.SubscriptionMessage;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services.Transport.Http.Controllers;

public class PersistentSubscriptionController(
	IHttpForwarder httpForwarder,
	IPublisher publisher,
	IPublisher networkSendQueue) : CommunicationController(publisher) {
	private const int DefaultNumberOfMessagesToGet = 1;
	private static readonly ICodec[] DefaultCodecs = [Codec.Json, Codec.Xml];
	static readonly char[] ETagSeparatorArray = [';'];

	private static readonly ICodec[] AtomCodecs = {
		Codec.CompetingXml,
		Codec.CompetingJson,
	};

	private static readonly ILogger Log = Serilog.Log.ForContext<PersistentSubscriptionController>();

	protected override void SubscribeCore(IUriRouter router) {
		Register(router, "/subscriptions", HttpMethod.Get, GetAllSubscriptionInfo, Codec.NoCodecs, DefaultCodecs, new Operation(Operations.Subscriptions.Statistics));
		Register(router, "/subscriptions/restart", HttpMethod.Post, RestartPersistentSubscriptions, Codec.NoCodecs, DefaultCodecs, new Operation(Operations.Subscriptions.Restart));
		Register(router, "/subscriptions/{stream}", HttpMethod.Get, GetSubscriptionInfoForStream, Codec.NoCodecs, DefaultCodecs, new Operation(Operations.Subscriptions.Statistics));
		Register(router, "/subscriptions/{stream}/{subscription}", HttpMethod.Put, PutSubscription, DefaultCodecs, DefaultCodecs, new Operation(Operations.Subscriptions.Create));
		Register(router, "/subscriptions/{stream}/{subscription}", HttpMethod.Post, PostSubscription, DefaultCodecs, DefaultCodecs, new Operation(Operations.Subscriptions.Update));
		RegisterUrlBased(router, "/subscriptions/{stream}/{subscription}", HttpMethod.Delete, new Operation(Operations.Subscriptions.Delete), DeleteSubscription);
		Register(router, "/subscriptions/{stream}/{subscription}", HttpMethod.Get, GetNextNMessages, Codec.NoCodecs, AtomCodecs, WithParameters(Operations.Subscriptions.ProcessMessages));
		Register(router, "/subscriptions/{stream}/{subscription}?embed={embed}", HttpMethod.Get, GetNextNMessages, Codec.NoCodecs, AtomCodecs,
			WithParameters(Operations.Subscriptions.ProcessMessages));
		Register(router, "/subscriptions/{stream}/{subscription}/{count}?embed={embed}", HttpMethod.Get, GetNextNMessages, Codec.NoCodecs, AtomCodecs,
			WithParameters(Operations.Subscriptions.ProcessMessages));
		Register(router, "/subscriptions/{stream}/{subscription}/info", HttpMethod.Get, GetSubscriptionInfo, Codec.NoCodecs, DefaultCodecs, new Operation(Operations.Subscriptions.Statistics));
		RegisterUrlBased(router, "/subscriptions/{stream}/{subscription}/replayParked?stopAt={stopAt}", HttpMethod.Post, WithParameters(Operations.Subscriptions.ReplayParked), ReplayParkedMessages);
		RegisterUrlBased(router, "/subscriptions/{stream}/{subscription}/ack/{messageid}", HttpMethod.Post, WithParameters(Operations.Subscriptions.ProcessMessages), AckMessage);
		RegisterUrlBased(router, "/subscriptions/{stream}/{subscription}/nack/{messageid}?action={action}", HttpMethod.Post, WithParameters(Operations.Subscriptions.ProcessMessages), NackMessage);
		RegisterUrlBased(router, "/subscriptions/{stream}/{subscription}/ack?ids={messageids}", HttpMethod.Post, WithParameters(Operations.Subscriptions.ProcessMessages), AckMessages);
		RegisterUrlBased(router, "/subscriptions/{stream}/{subscription}/nack?ids={messageids}&action={action}", HttpMethod.Post, WithParameters(Operations.Subscriptions.ProcessMessages),
			NackMessages);
		//map view parked messages
		Register(router, "/subscriptions/viewparkedmessages/{stream}/{group}/{event}/backward/{count}?embed={embed}", HttpMethod.Get, ViewParkedMessagesBackward, Codec.NoCodecs, AtomCodecs,
			WithParameters(Operations.Subscriptions.Statistics));
		RegisterCustom(router, "/subscriptions/viewparkedmessages/{stream}/{group}/{event}/forward/{count}?embed={embed}", HttpMethod.Get,
			ViewParkedMessagesForward, Codec.NoCodecs, AtomCodecs, WithParameters(Operations.Subscriptions.Statistics));
	}

	private RequestParams ViewParkedMessagesForward(HttpEntityManager manager, UriTemplateMatch match) {
		var stream = match.BoundVariables["stream"];
		var groupName = match.BoundVariables["group"];
		var evNum = match.BoundVariables["event"];
		var cnt = match.BoundVariables["count"];

		stream = BuildSubscriptionParkedStreamName(stream, groupName);

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

		GetStreamEventsForward(manager, stream, eventNumber, count, resolveLinkTos, requireLeader, etag,
			longPollTimeout, embed);
		return new RequestParams((longPollTimeout ?? TimeSpan.Zero) + ESConsts.HttpTimeout);
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

	private void GetStreamEventsForward(HttpEntityManager manager, string stream, long eventNumber, int count,
		bool resolveLinkTos, bool requireLeader, long? etag, TimeSpan? longPollTimeout, EmbedLevel embed) {
		var envelope = new SendToHttpEnvelope(networkSendQueue,
			manager,
			(ent, msg) => Format.GetStreamEventsForward(ent, msg, embed),
			Configure.GetStreamEventsForward);
		var corrId = Guid.NewGuid();
		Publish(new ReadStreamEventsForward(corrId, corrId, envelope, stream, eventNumber, count,
			resolveLinkTos, requireLeader, etag, manager.User,
			replyOnExpired: false,
			longPollTimeout: longPollTimeout));
	}

	private void ViewParkedMessagesBackward(HttpEntityManager http, UriTemplateMatch match) {
		var stream = match.BoundVariables["stream"];
		var groupName = match.BoundVariables["group"];
		var evNum = match.BoundVariables["event"];
		var cnt = match.BoundVariables["count"];

		stream = BuildSubscriptionParkedStreamName(stream, groupName);

		long eventNumber = -1;
		int count = AtomSpecs.FeedPageSize;
		var embed = GetEmbedLevel(http, match);

		if (stream.IsEmptyString()) {
			SendBadRequest(http, $"Invalid stream name '{stream}'");
			return;
		}

		if (evNum != null && evNum != "head" && (!long.TryParse(evNum, out eventNumber) || eventNumber < 0)) {
			SendBadRequest(http, $"'{evNum}' is not valid event number");
			return;
		}

		if (cnt.IsNotEmptyString() && (!int.TryParse(cnt, out count) || count <= 0)) {
			SendBadRequest(http, $"'{cnt}' is not valid count. Should be positive integer");
			return;
		}

		if (!GetResolveLinkTos(http, out var resolveLinkTos, true)) {
			SendBadRequest(http, $"{SystemHeaders.ResolveLinkTos} header in wrong format.");
			return;
		}

		if (!GetRequireLeader(http, out var requireLeader)) {
			SendBadRequest(http, $"{SystemHeaders.RequireLeader} header in wrong format.");
			return;
		}

		bool headOfStream = eventNumber == -1;
		GetStreamEventsBackward(http, stream, eventNumber, count, resolveLinkTos, requireLeader, headOfStream,
			embed);
	}

	private void GetStreamEventsBackward(HttpEntityManager manager, string stream, long eventNumber, int count,
		bool resolveLinkTos, bool requireLeader, bool headOfStream, EmbedLevel embed) {
		var envelope = new SendToHttpEnvelope(networkSendQueue,
			manager,
			(ent, msg) =>
				Format.GetStreamEventsBackward(ent, msg, embed, headOfStream),
			(args, msg) => Configure.GetStreamEventsBackward(args, msg, headOfStream));
		var corrId = Guid.NewGuid();
		Publish(new ReadStreamEventsBackward(corrId, corrId, envelope, stream, eventNumber, count,
			resolveLinkTos, requireLeader, GetETagStreamVersion(manager), manager.User));
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

	private static long? GetETagStreamVersion(HttpEntityManager manager) {
		var etag = manager.HttpEntity.Request.GetHeaderValues("If-None-Match");
		if (StringValues.IsNullOrEmpty(etag)) return null;
		// etag format is version;contenttypehash
		var splitted = etag.ToString().Trim('\"').Split(ETagSeparatorArray);
		if (splitted.Length != 2) return null;
		var typeHash = manager.ResponseCodec.ContentType.GetHashCode()
			.ToString(CultureInfo.InvariantCulture);
		var res = splitted[1] == typeHash && long.TryParse(splitted[0], out var streamVersion)
			? (long?)streamVersion
			: null;
		return res;
	}

	static Func<UriTemplateMatch, Operation> WithParameters(OperationDefinition definition) {
		return match => {
			var operation = new Operation(definition);
			var stream = match.BoundVariables["stream"];
			if (!string.IsNullOrEmpty(stream))
				operation = operation.WithParameter(Operations.Subscriptions.Parameters.StreamId(stream));
			var subscription = match.BoundVariables["subscription"];
			if (!string.IsNullOrEmpty(subscription))
				operation = operation.WithParameter(
					Operations.Subscriptions.Parameters.SubscriptionId(subscription));
			return operation;
		};
	}

	private static ClientMessages.NakAction GetNackAction(UriTemplateMatch match) {
		var rawValue = match.BoundVariables["action"] ?? string.Empty;
		return rawValue.ToLowerInvariant() switch {
			"park" => ClientMessages.NakAction.Park,
			"retry" => ClientMessages.NakAction.Retry,
			"skip" => ClientMessages.NakAction.Skip,
			"stop" => ClientMessages.NakAction.Stop,
			_ => ClientMessages.NakAction.Unknown
		};
	}

	private void AckMessages(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var envelope = new NoopEnvelope();
		var groupname = match.BoundVariables["subscription"];
		var stream = match.BoundVariables["stream"];
		var messageIds = match.BoundVariables["messageIds"];
		var ids = new List<Guid>();
		foreach (var messageId in messageIds.Split(new[] { ',' })) {
			if (!Guid.TryParse(messageId, out var id)) {
				http.ReplyStatus(HttpStatusCode.BadRequest, "messageid should be a properly formed guid", _ => { });
				return;
			}

			ids.Add(id);
		}

		var cmd = new PersistentSubscriptionAckEvents(
			Guid.NewGuid(),
			Guid.NewGuid(),
			envelope,
			BuildSubscriptionGroupKey(stream, groupname),
			ids.ToArray(),
			http.User);
		Publish(cmd);
		http.ReplyStatus(HttpStatusCode.Accepted, "", _ => { });
	}

	private void NackMessages(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var envelope = new NoopEnvelope();
		var groupname = match.BoundVariables["subscription"];
		var stream = match.BoundVariables["stream"];
		var messageIds = match.BoundVariables["messageIds"];
		var nakAction = GetNackAction(match);
		var ids = new List<Guid>();
		foreach (var messageId in messageIds.Split([','])) {
			if (!Guid.TryParse(messageId, out var id)) {
				http.ReplyStatus(HttpStatusCode.BadRequest, "messageid should be a properly formed guid",
					exception => { });
				return;
			}

			ids.Add(id);
		}

		var cmd = new PersistentSubscriptionNackEvents(
			Guid.NewGuid(),
			Guid.NewGuid(),
			envelope,
			BuildSubscriptionGroupKey(stream, groupname),
			"Nacked from HTTP",
			nakAction,
			ids.ToArray(),
			http.User);
		Publish(cmd);
		http.ReplyStatus(HttpStatusCode.Accepted, "", _ => { });
	}

	private static string BuildSubscriptionGroupKey(string stream, string groupName) => $"{stream}::{groupName}";

	private static string BuildSubscriptionParkedStreamName(string stream, string groupName)
		=> $"$persistentsubscription-{BuildSubscriptionGroupKey(stream, groupName)}-parked";

	private void AckMessage(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var envelope = new NoopEnvelope();
		var groupname = match.BoundVariables["subscription"];
		var stream = match.BoundVariables["stream"];
		var messageId = match.BoundVariables["messageId"];
		var id = Guid.NewGuid();
		if (!Guid.TryParse(messageId, out id)) {
			http.ReplyStatus(HttpStatusCode.BadRequest, "messageid should be a properly formed guid",
				exception => { });
			return;
		}

		var cmd = new PersistentSubscriptionAckEvents(
			Guid.NewGuid(),
			Guid.NewGuid(),
			envelope,
			BuildSubscriptionGroupKey(stream, groupname),
			[id],
			http.User);
		Publish(cmd);
		http.ReplyStatus(HttpStatusCode.Accepted, "", _ => { });
	}

	private void NackMessage(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var envelope = new NoopEnvelope();
		var groupname = match.BoundVariables["subscription"];
		var stream = match.BoundVariables["stream"];
		var messageId = match.BoundVariables["messageId"];
		var nakAction = GetNackAction(match);
		if (!Guid.TryParse(messageId, out var id)) {
			http.ReplyStatus(HttpStatusCode.BadRequest, "messageid should be a properly formed guid", _ => { });
			return;
		}

		var cmd = new PersistentSubscriptionNackEvents(
			Guid.NewGuid(),
			Guid.NewGuid(),
			envelope,
			BuildSubscriptionGroupKey(stream, groupname),
			"Nacked from HTTP",
			nakAction,
			[id],
			http.User);
		Publish(cmd);
		http.ReplyStatus(HttpStatusCode.Accepted, "", _ => { });
	}

	private void ReplayParkedMessages(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var envelope = new SendToHttpEnvelope(
			networkSendQueue, http,
			(_, message) => http.ResponseCodec.To(message),
			(_, message) => {
				if (message is not ReplayMessagesReceived m)
					throw new($"Unexpected message {message}");
				var code = m.Result switch {
					ReplayMessagesReceived.ReplayMessagesReceivedResult.Success => HttpStatusCode.OK,
					ReplayMessagesReceived.ReplayMessagesReceivedResult.DoesNotExist => HttpStatusCode.NotFound,
					ReplayMessagesReceived.ReplayMessagesReceivedResult.AccessDenied => HttpStatusCode.Unauthorized,
					_ => HttpStatusCode.InternalServerError
				};

				return new(code, http.ResponseCodec.ContentType, http.ResponseCodec.Encoding);
			});
		var groupname = match.BoundVariables["subscription"];
		var stream = match.BoundVariables["stream"];
		var stopAtStr = match.BoundVariables["stopAt"];

		long? stopAt;
		// if stopAt is declared...
		if (stopAtStr != null) {
			// check it is valid
			if (!long.TryParse(stopAtStr, out var stopAtLong) || stopAtLong < 0) {
				http.ReplyStatus(HttpStatusCode.BadRequest, "stopAt should be a properly formed positive long",
					exception => { });
				return;
			}

			stopAt = stopAtLong;
		} else {
			// else it's null
			stopAt = null;
		}

		var cmd = new ReplayParkedMessages(Guid.NewGuid(), Guid.NewGuid(), envelope, stream, groupname, stopAt, http.User);
		Publish(cmd);
	}

	private void PutSubscription(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var groupname = match.BoundVariables["subscription"];
		var stream = match.BoundVariables["stream"];
		var envelope = new SendToHttpEnvelope(
			networkSendQueue, http,
			(_, message) => http.ResponseCodec.To(message),
			(_, message) => {
				int code;
				if (message is not CreatePersistentSubscriptionToStreamCompleted m)
					throw new Exception($"Unexpected message {message}");
				code = m.Result switch {
					CreatePersistentSubscriptionToStreamResult.Success => HttpStatusCode.Created,
					CreatePersistentSubscriptionToStreamResult.AlreadyExists => HttpStatusCode.Conflict,
					CreatePersistentSubscriptionToStreamResult.AccessDenied => HttpStatusCode.Unauthorized,
					CreatePersistentSubscriptionToStreamResult.Fail => HttpStatusCode.BadRequest,
					_ => HttpStatusCode.InternalServerError
				};

				return new(code, http.ResponseCodec.ContentType,
					http.ResponseCodec.Encoding,
					new KeyValuePair<string, string>("location", MakeUrl(http, $"/subscriptions/{stream}/{groupname}")));
			});
		http.ReadTextRequestAsync(
			(_, s) => {
				var data = http.RequestCodec.From<SubscriptionConfigData>(s);
				var config = ParseConfig(data);
				if (!ValidateConfig(config, http)) return;
				var message = new CreatePersistentSubscriptionToStream(Guid.NewGuid(),
					Guid.NewGuid(),
					envelope,
					stream,
					groupname,
					config.ResolveLinktos,
#pragma warning disable 612
					config.StartPosition != null ? long.Parse(config.StartPosition) : config.StartFrom,
#pragma warning restore 612
					config.MessageTimeoutMilliseconds,
					config.ExtraStatistics,
					config.MaxRetryCount,
					config.BufferSize,
					config.LiveBufferSize,
					config.ReadBatchSize,
					config.CheckPointAfterMilliseconds,
					config.MinCheckPointCount,
					config.MaxCheckPointCount,
					config.MaxSubscriberCount,
					CalculateNamedConsumerStrategyForOldClients(data),
					http.User);
				Publish(message);
			}, x => Log.Debug(x, "Reply Text Content Failed."));
	}

	private void PostSubscription(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var groupname = match.BoundVariables["subscription"];
		var stream = match.BoundVariables["stream"];
		var envelope = new SendToHttpEnvelope(
			networkSendQueue, http,
			(_, message) => http.ResponseCodec.To(message),
			(_, message) => {
				int code;
				if (message is not UpdatePersistentSubscriptionToStreamCompleted m)
					throw new Exception($"Unexpected message {message}");
				code = m.Result switch {
					UpdatePersistentSubscriptionToStreamResult.Success => HttpStatusCode.OK,
					UpdatePersistentSubscriptionToStreamResult.DoesNotExist => HttpStatusCode.NotFound,
					UpdatePersistentSubscriptionToStreamResult.AccessDenied => HttpStatusCode.Unauthorized,
					UpdatePersistentSubscriptionToStreamResult.Fail => HttpStatusCode.BadRequest,
					_ => HttpStatusCode.InternalServerError
				};

				return new(code, http.ResponseCodec.ContentType,
					http.ResponseCodec.Encoding,
					new KeyValuePair<string, string>("location", MakeUrl(http, $"/subscriptions/{stream}/{groupname}")));
			});
		http.ReadTextRequestAsync(
			(_, s) => {
				var data = http.RequestCodec.From<SubscriptionConfigData>(s);
				var config = ParseConfig(data);
				if (!ValidateConfig(config, http)) return;
				var message = new UpdatePersistentSubscriptionToStream(Guid.NewGuid(),
					Guid.NewGuid(),
					envelope,
					stream,
					groupname,
					config.ResolveLinktos,
#pragma warning disable 612
					config.StartPosition != null ? long.Parse(config.StartPosition) : config.StartFrom,
#pragma warning restore 612
					config.MessageTimeoutMilliseconds,
					config.ExtraStatistics,
					config.MaxRetryCount,
					config.BufferSize,
					config.LiveBufferSize,
					config.ReadBatchSize,
					config.CheckPointAfterMilliseconds,
					config.MinCheckPointCount,
					config.MaxCheckPointCount,
					config.MaxSubscriberCount,
					CalculateNamedConsumerStrategyForOldClients(data),
					http.User);
				Publish(message);
			}, x => Log.Debug(x, "Reply Text Content Failed."));
	}

	private static SubscriptionConfigData ParseConfig(SubscriptionConfigData config) {
		if (config == null) {
			return new();
		}

		return new() {
			ResolveLinktos = config.ResolveLinktos,
#pragma warning disable 612
			StartFrom = config.StartFrom,
#pragma warning restore 612
			StartPosition = config.StartPosition,
			MessageTimeoutMilliseconds = config.MessageTimeoutMilliseconds,
			ExtraStatistics = config.ExtraStatistics,
			MaxRetryCount = config.MaxRetryCount,
			BufferSize = config.BufferSize,
			LiveBufferSize = config.LiveBufferSize,
			ReadBatchSize = config.ReadBatchSize,
			CheckPointAfterMilliseconds = config.CheckPointAfterMilliseconds,
			MinCheckPointCount = config.MinCheckPointCount,
			MaxCheckPointCount = config.MaxCheckPointCount,
			MaxSubscriberCount = config.MaxSubscriberCount
		};
	}

	private bool ValidateConfig(SubscriptionConfigData config, HttpEntityManager http) {
		if (config.BufferSize <= 0) {
			SendBadRequest(http, $"Buffer Size ({config.BufferSize}) must be positive");
			return false;
		}

		if (config.LiveBufferSize <= 0) {
			SendBadRequest(http, $"Live Buffer Size ({config.LiveBufferSize}) must be positive");
			return false;
		}

		if (config.ReadBatchSize <= 0) {
			SendBadRequest(http, $"Read Batch Size ({config.ReadBatchSize}) must be positive");
			return false;
		}

		if (!(config.BufferSize > config.ReadBatchSize)) {
			SendBadRequest(http, $"BufferSize ({config.BufferSize}) must be larger than ReadBatchSize ({config.ReadBatchSize})");
			return false;
		}

		return true;
	}

	private static string CalculateNamedConsumerStrategyForOldClients(SubscriptionConfigData data) {
		var namedConsumerStrategy = data?.NamedConsumerStrategy;
		if (string.IsNullOrEmpty(namedConsumerStrategy)) {
			var preferRoundRobin = data == null || data.PreferRoundRobin;
			namedConsumerStrategy = preferRoundRobin
				? SystemConsumerStrategies.RoundRobin
				: SystemConsumerStrategies.DispatchToSingle;
		}

		return namedConsumerStrategy;
	}

	private void DeleteSubscription(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var envelope = new SendToHttpEnvelope(
			networkSendQueue, http,
			(_, message) => http.ResponseCodec.To(message),
			(_, message) => {
				if (message is not DeletePersistentSubscriptionToStreamCompleted m)
					throw new Exception($"Unexpected message {message}");
				var code = m.Result switch {
					DeletePersistentSubscriptionToStreamResult.Success => HttpStatusCode.OK,
					DeletePersistentSubscriptionToStreamResult.DoesNotExist => HttpStatusCode.NotFound,
					DeletePersistentSubscriptionToStreamResult.AccessDenied => HttpStatusCode.Unauthorized,
					DeletePersistentSubscriptionToStreamResult.Fail => HttpStatusCode.BadRequest,
					_ => HttpStatusCode.InternalServerError
				};

				return new(code, http.ResponseCodec.ContentType, http.ResponseCodec.Encoding);
			});
		var groupname = match.BoundVariables["subscription"];
		var stream = match.BoundVariables["stream"];
		var cmd = new DeletePersistentSubscriptionToStream(Guid.NewGuid(), Guid.NewGuid(), envelope, stream, groupname, http.User);
		Publish(cmd);
	}

	private void RestartPersistentSubscriptions(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;

		var envelope = new SendToHttpEnvelope<PersistentSubscriptionsRestarting>(networkSendQueue, http,
			(e, _) => e.To("Restarting"),
			(e, message) => message switch {
				not null => Configure.Ok(e.ContentType),
				_ => Configure.InternalServerError()
			}, CreateErrorEnvelope(http)
		);

		Publish(new PersistentSubscriptionsRestart(envelope));
	}

	private void GetAllSubscriptionInfo(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var envelope = new SendToHttpEnvelope(
			networkSendQueue, http,
			(_, message) => http.ResponseCodec.To(ToSummaryDto(http, message as GetPersistentSubscriptionStatsCompleted).ToArray()),
			(_, message) => StatsConfiguration(http, message));
		var cmd = new GetAllPersistentSubscriptionStats(envelope);
		Publish(cmd);
	}

	private void GetSubscriptionInfoForStream(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var stream = match.BoundVariables["stream"];
		var envelope = new SendToHttpEnvelope(
			networkSendQueue, http,
			(_, message) => http.ResponseCodec.To(ToSummaryDto(http, message as GetPersistentSubscriptionStatsCompleted).ToArray()),
			(_, message) => StatsConfiguration(http, message));
		var cmd = new GetStreamPersistentSubscriptionStats(envelope, stream);
		Publish(cmd);
	}

	private void GetSubscriptionInfo(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var stream = match.BoundVariables["stream"];
		var groupName = match.BoundVariables["subscription"];
		var envelope = new SendToHttpEnvelope(
			networkSendQueue, http,
			(_, message) => http.ResponseCodec.To(ToDto(http, message as GetPersistentSubscriptionStatsCompleted).FirstOrDefault()),
			(_, message) => StatsConfiguration(http, message));
		var cmd = new GetPersistentSubscriptionStats(envelope, stream, groupName);
		Publish(cmd);
	}

	private SendToHttpEnvelope<InvalidPersistentSubscriptionsRestart> CreateErrorEnvelope(HttpEntityManager http) {
		return new(networkSendQueue, http, ErrorFormatter, ErrorConfigurator, null);
	}

	private static ResponseConfiguration ErrorConfigurator(ICodec codec, InvalidPersistentSubscriptionsRestart message) {
		return new(HttpStatusCode.BadRequest, "Bad Request", "text/plain", Helper.UTF8NoBom);
	}

	private static string ErrorFormatter(ICodec codec, InvalidPersistentSubscriptionsRestart message) {
		return message.Reason;
	}


	private static ResponseConfiguration StatsConfiguration(HttpEntityManager http, Message message) {
		if (message is not GetPersistentSubscriptionStatsCompleted m)
			throw new Exception($"Unexpected message {message}");
		var code = m.Result switch {
			OperationStatus.Success => HttpStatusCode.OK,
			OperationStatus.NotFound => HttpStatusCode.NotFound,
			OperationStatus.NotReady => HttpStatusCode.ServiceUnavailable,
			_ => HttpStatusCode.InternalServerError
		};

		return new(code, http.ResponseCodec.ContentType, http.ResponseCodec.Encoding);
	}

	private void GetNextNMessages(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var groupname = match.BoundVariables["subscription"];
		var stream = match.BoundVariables["stream"];
		var cnt = match.BoundVariables["count"];
		var embed = GetEmbedLevel(http, match);
		int count = DefaultNumberOfMessagesToGet;
		if (!cnt.IsEmptyString() && (!int.TryParse(cnt, out count) || count > 100 || count < 1)) {
			SendBadRequest(http, $"Message count must be an integer between 1 and 100 'count' ='{count}'");
			return;
		}

		var envelope = new SendToHttpEnvelope(
			networkSendQueue, http,
			(_, message) => Format.ReadNextNPersistentMessagesCompleted(http, message as ReadNextNPersistentMessagesCompleted, stream, groupname, count, embed),
			(_, message) => {
				if (message is not ReadNextNPersistentMessagesCompleted m)
					throw new Exception($"Unexpected message {message}");
				var code = m.Result switch {
					ReadNextNPersistentMessagesResult.Success => HttpStatusCode.OK,
					ReadNextNPersistentMessagesResult.DoesNotExist => HttpStatusCode.NotFound,
					ReadNextNPersistentMessagesResult.AccessDenied => HttpStatusCode.Unauthorized,
					ReadNextNPersistentMessagesResult.Fail => HttpStatusCode.BadRequest,
					_ => HttpStatusCode.InternalServerError
				};

				return new(code, http.ResponseCodec.ContentType, http.ResponseCodec.Encoding);
			});

		var cmd = new ReadNextNPersistentMessages(
			Guid.NewGuid(),
			Guid.NewGuid(),
			envelope,
			stream,
			groupname,
			count,
			http.User);
		Publish(cmd);
	}

	private static EmbedLevel GetEmbedLevel(HttpEntityManager manager, UriTemplateMatch match,
		EmbedLevel htmlLevel = EmbedLevel.PrettyBody) {
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

	readonly string _parkedMessageUriTemplate = $"/streams/{Uri.EscapeDataString("$persistentsubscription")}-{{0}}::{{1}}-parked";

	private IEnumerable<SubscriptionInfo> ToDto(HttpEntityManager manager, GetPersistentSubscriptionStatsCompleted message) {
		if (message?.SubscriptionStats == null) yield break;

		foreach (var stat in message.SubscriptionStats) {
			string escapedStreamId = Uri.EscapeDataString(stat.EventSource);
			string escapedGroupName = Uri.EscapeDataString(stat.GroupName);
			var info = new SubscriptionInfo {
				Links = [
					new RelLink(MakeUrl(manager, $"/subscriptions/{escapedStreamId}/{escapedGroupName}/info"), "detail"),
					new RelLink(MakeUrl(manager, $"/subscriptions/{escapedStreamId}/{escapedGroupName}/replayParked"), "replayParked")
				],
				EventStreamId = stat.EventSource,
				GroupName = stat.GroupName,
				Status = stat.Status,
				AverageItemsPerSecond = stat.AveragePerSecond,
				TotalItemsProcessed = stat.TotalItems,
				CountSinceLastMeasurement = stat.CountSinceLastMeasurement,
#pragma warning disable 612
				LastKnownEventNumber = long.TryParse(stat.LastKnownEventPosition, out var lastKnownMsg) ? lastKnownMsg : 0,
#pragma warning restore 612
				LastKnownEventPosition = stat.LastKnownEventPosition,
#pragma warning disable 612
				LastProcessedEventNumber = long.TryParse(stat.LastCheckpointedEventPosition, out var lastProcessedPos) ? lastProcessedPos : 0,
#pragma warning restore 612
				LastCheckpointedEventPosition = stat.LastCheckpointedEventPosition,
				ReadBufferCount = stat.ReadBufferCount,
				LiveBufferCount = stat.LiveBufferCount,
				RetryBufferCount = stat.RetryBufferCount,
				TotalInFlightMessages = stat.TotalInFlightMessages,
				OutstandingMessagesCount = stat.OutstandingMessagesCount,
				ParkedMessageUri = MakeUrl(manager, string.Format(_parkedMessageUriTemplate, escapedStreamId, escapedGroupName)),
				GetMessagesUri = MakeUrl(manager, $"/subscriptions/{escapedStreamId}/{escapedGroupName}/{DefaultNumberOfMessagesToGet}"),
				Config = new SubscriptionConfigData {
					CheckPointAfterMilliseconds = stat.CheckPointAfterMilliseconds,
					BufferSize = stat.BufferSize,
					LiveBufferSize = stat.LiveBufferSize,
					MaxCheckPointCount = stat.MaxCheckPointCount,
					MaxRetryCount = stat.MaxRetryCount,
					MessageTimeoutMilliseconds = stat.MessageTimeoutMilliseconds,
					MinCheckPointCount = stat.MinCheckPointCount,
					NamedConsumerStrategy = stat.NamedConsumerStrategy,
					PreferRoundRobin = stat.NamedConsumerStrategy == SystemConsumerStrategies.RoundRobin,
					ReadBatchSize = stat.ReadBatchSize,
					ResolveLinktos = stat.ResolveLinktos,
#pragma warning disable 612
					StartFrom = long.TryParse(stat.StartFrom, out var startFrom) ? startFrom : 0,
#pragma warning restore 612
					StartPosition = stat.StartFrom,
					ExtraStatistics = stat.ExtraStatistics,
					MaxSubscriberCount = stat.MaxSubscriberCount,
				},
				Connections = [],
				ParkedMessageCount = stat.ParkedMessageCount
			};
			if (stat.Connections != null) {
				foreach (var connection in stat.Connections) {
					info.Connections.Add(new ConnectionInfo {
						Username = connection.Username,
						From = connection.From,
						AverageItemsPerSecond = connection.AverageItemsPerSecond,
						CountSinceLastMeasurement = connection.CountSinceLastMeasurement,
						TotalItemsProcessed = connection.TotalItems,
						AvailableSlots = connection.AvailableSlots,
						InFlightMessages = connection.InFlightMessages,
						ExtraStatistics = connection.ObservedMeasurements ?? new List<Measurement>(),
						ConnectionName = connection.ConnectionName,
					});
				}
			}

			yield return info;
		}
	}

	private IEnumerable<SubscriptionSummary> ToSummaryDto(HttpEntityManager manager, GetPersistentSubscriptionStatsCompleted message) {
		if (message?.SubscriptionStats == null) yield break;

		foreach (var stat in message.SubscriptionStats) {
			string escapedStreamId = Uri.EscapeDataString(stat.EventSource);
			string escapedGroupName = Uri.EscapeDataString(stat.GroupName);
			var info = new SubscriptionSummary {
				Links = [new RelLink(MakeUrl(manager, $"/subscriptions/{escapedStreamId}/{escapedGroupName}/info"), "detail")],
				EventStreamId = stat.EventSource,
				GroupName = stat.GroupName,
				Status = stat.Status,
				AverageItemsPerSecond = stat.AveragePerSecond,
				TotalItemsProcessed = stat.TotalItems,
#pragma warning disable 612
				LastKnownEventNumber = long.TryParse(stat.LastKnownEventPosition, out var lastKnownMsg) ? lastKnownMsg : 0,
#pragma warning restore 612
				LastKnownEventPosition = stat.LastKnownEventPosition,
#pragma warning disable 612
				LastProcessedEventNumber = long.TryParse(stat.LastCheckpointedEventPosition, out var lastEventPos) ? lastEventPos : 0,
#pragma warning restore 612
				LastCheckpointedEventPosition = stat.LastCheckpointedEventPosition,
				ParkedMessageUri = MakeUrl(manager, string.Format(_parkedMessageUriTemplate, escapedStreamId, escapedGroupName)),
				GetMessagesUri = MakeUrl(manager, $"/subscriptions/{escapedStreamId}/{escapedGroupName}/{DefaultNumberOfMessagesToGet}"),
				TotalInFlightMessages = stat.TotalInFlightMessages,
			};
			if (stat.Connections != null) {
				info.ConnectionCount = stat.Connections.Count;
			}

			yield return info;
		}
	}

	public class SubscriptionConfigData {
		public bool ResolveLinktos { get; set; }
		[Obsolete] public long StartFrom { get; set; }
		public string StartPosition { get; set; }
		public int MessageTimeoutMilliseconds { get; set; }
		public bool ExtraStatistics { get; set; }
		public int MaxRetryCount { get; set; }
		public int LiveBufferSize { get; set; }
		public int BufferSize { get; set; }
		public int ReadBatchSize { get; set; }
		public bool PreferRoundRobin { get; set; }
		public int CheckPointAfterMilliseconds { get; set; }
		public int MinCheckPointCount { get; set; }
		public int MaxCheckPointCount { get; set; }
		public int MaxSubscriberCount { get; set; }
		public string NamedConsumerStrategy { get; set; }

		public SubscriptionConfigData() {
#pragma warning disable 612
			StartFrom = 0;
#pragma warning restore 612
			StartPosition = null;
			MessageTimeoutMilliseconds = 10000;
			MaxRetryCount = 10;
			CheckPointAfterMilliseconds = 1000;
			MinCheckPointCount = 10;
			MaxCheckPointCount = 500;
			MaxSubscriberCount = 10;
			NamedConsumerStrategy = "RoundRobin";

			BufferSize = 500;
			LiveBufferSize = 500;
			ReadBatchSize = 20;
		}
	}

	public class SubscriptionSummary {
		public List<RelLink> Links { get; set; }
		public string EventStreamId { get; set; }
		public string GroupName { get; set; }
		public string ParkedMessageUri { get; set; }
		public string GetMessagesUri { get; set; }
		public string Status { get; set; }
		public decimal AverageItemsPerSecond { get; set; }
		public long TotalItemsProcessed { get; set; }
		[Obsolete] public long LastProcessedEventNumber { get; set; }
		public string LastCheckpointedEventPosition { get; set; }
		[Obsolete] public long LastKnownEventNumber { get; set; }
		public string LastKnownEventPosition { get; set; }
		public int ConnectionCount { get; set; }
		public int TotalInFlightMessages { get; set; }
	}

	public class SubscriptionInfo {
		public List<RelLink> Links { get; set; }
		public SubscriptionConfigData Config { get; set; }
		public string EventStreamId { get; set; }
		public string GroupName { get; set; }
		public string Status { get; set; }
		public decimal AverageItemsPerSecond { get; set; }
		public string ParkedMessageUri { get; set; }
		public string GetMessagesUri { get; set; }
		public long TotalItemsProcessed { get; set; }
		public long CountSinceLastMeasurement { get; set; }
		[Obsolete] public long LastProcessedEventNumber { get; set; }
		public string LastCheckpointedEventPosition { get; set; }
		[Obsolete] public long LastKnownEventNumber { get; set; }
		public string LastKnownEventPosition { get; set; }
		public int ReadBufferCount { get; set; }
		public long LiveBufferCount { get; set; }
		public int RetryBufferCount { get; set; }
		public int TotalInFlightMessages { get; set; }
		public int OutstandingMessagesCount { get; set; }
		public List<ConnectionInfo> Connections { get; set; }
		public long ParkedMessageCount { get; set; }
	}

	public class ConnectionInfo {
		public string From { get; set; }
		public string Username { get; set; }
		public decimal AverageItemsPerSecond { get; set; }
		public long TotalItemsProcessed { get; set; }
		public long CountSinceLastMeasurement { get; set; }
		public List<Measurement> ExtraStatistics { get; set; }
		public int AvailableSlots { get; set; }
		public int InFlightMessages { get; set; }
		public string ConnectionName { get; set; }
	}
}
