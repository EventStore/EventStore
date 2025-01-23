// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Plugins.Authorization;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Atom;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using Microsoft.Extensions.Primitives;
using Serilog;
using static EventStore.Core.Messages.ClientMessage;
using static EventStore.Core.Messages.MonitoringMessage;
using static EventStore.Plugins.Authorization.Operations;

namespace EventStore.Core.Services.Transport.Http.Controllers;

public class AdminController(IPublisher publisher, IPublisher networkSendQueue) : CommunicationController(publisher) {
	private static readonly ILogger Log = Serilog.Log.ForContext<AdminController>();

	private static readonly ICodec[] SupportedCodecs = [Codec.Text, Codec.Json, Codec.Xml, Codec.ApplicationXml];

	public static readonly char[] ETagSeparatorArray = { ';' };

	private static readonly Func<UriTemplateMatch, Operation> ReadStreamOperationForScavengeStream = ForScavengeStream(Streams.Read);

	protected override void SubscribeCore(IUriRouter router) {
		router.RegisterAction(new("/admin/shutdown", HttpMethod.Post, Codec.NoCodecs, SupportedCodecs, new Operation(Node.Shutdown)), OnPostShutdown);
		router.RegisterAction(new("/admin/reloadconfig", HttpMethod.Post, Codec.NoCodecs, SupportedCodecs, new Operation(Node.ReloadConfiguration)), OnPostReloadConfig);
		router.RegisterAction(
			new("/admin/scavenge?startFromChunk={startFromChunk}&threads={threads}&threshold={threshold}&throttlePercent={throttlePercent}&syncOnly={syncOnly}", HttpMethod.Post, Codec.NoCodecs,
				SupportedCodecs, new Operation(Node.Scavenge.Start)), OnPostScavenge);
		router.RegisterAction(new("/admin/scavenge/{scavengeId}", HttpMethod.Delete, Codec.NoCodecs, SupportedCodecs, new Operation(Node.Scavenge.Stop)), OnStopScavenge);
		router.RegisterAction(new("/admin/scavenge/current", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, new Operation(Node.Scavenge.Read)), OnGetCurrentScavenge);
		router.RegisterAction(new("/admin/scavenge/last", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, new Operation(Node.Scavenge.Read)), OnGetLastScavenge);
		router.RegisterAction(new("/admin/mergeindexes", HttpMethod.Post, Codec.NoCodecs, SupportedCodecs, new Operation(Node.MergeIndexes)), OnPostMergeIndexes);
		router.RegisterAction(new("/admin/node/priority/{nodePriority}", HttpMethod.Post, Codec.NoCodecs, SupportedCodecs, new Operation(Node.SetPriority)), OnSetNodePriority);
		router.RegisterAction(new("/admin/node/resign", HttpMethod.Post, Codec.NoCodecs, SupportedCodecs, new Operation(Node.Resign)), OnResignNode);
		router.RegisterAction(new("/admin/login", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, new Operation(Node.Login)), OnGetLogin);
		Register(router, "/streams/$scavenges/{scavengeId}/{event}/{count}?embed={embed}", HttpMethod.Get, GetStreamEventsBackwardScavenges, Codec.NoCodecs,
			SupportedCodecs, ReadStreamOperationForScavengeStream);
		Register(router, "/streams/$scavenges?embed={embed}", HttpMethod.Get, GetStreamEventsBackwardScavenges, Codec.NoCodecs, SupportedCodecs, ReadStreamOperationForScavengeStream);
	}

	private static Func<UriTemplateMatch, Operation> ForScavengeStream(OperationDefinition definition) {
		return match => {
			var operation = new Operation(definition);
			var stream = "$scavenges";
			var scavengeId = match.BoundVariables["scavengeId"];
			if (scavengeId != null)
				stream = $"{stream}-{scavengeId}";

			return !string.IsNullOrEmpty(stream) ? operation.WithParameter(Streams.Parameters.StreamId(stream)) : operation;
		};
	}

	static bool IsUnauthorized(HttpEntityManager entity)
		=> entity.User == null || (!entity.User.LegacyRoleCheck(SystemRoles.Admins) && !entity.User.LegacyRoleCheck(SystemRoles.Operations));

	static void ReplyUnauthorised(HttpEntityManager entity)
		=> entity.ReplyStatus(HttpStatusCode.Unauthorized, "Unauthorized", LogReplyError);

	private void OnPostShutdown(HttpEntityManager entity, UriTemplateMatch match) {
		if (IsUnauthorized(entity)) {
			ReplyUnauthorised(entity);
			return;
		}

		Log.Information("Request shut down of node because shutdown command has been received.");
		Publish(new RequestShutdown(exitProcess: true, shutdownHttp: true));
		entity.ReplyStatus(HttpStatusCode.OK, "OK", LogReplyError);
	}

	private void OnPostReloadConfig(HttpEntityManager entity, UriTemplateMatch match) {
		if (IsUnauthorized(entity)) {
			ReplyUnauthorised(entity);
			return;
		}

		Log.Information("Reloading the node's configuration since a request has been received on /admin/reloadconfig.");
		Publish(new ReloadConfig());
		entity.ReplyStatus(HttpStatusCode.OK, "OK", LogReplyError);
	}

	private void OnPostMergeIndexes(HttpEntityManager entity, UriTemplateMatch match) {
		Log.Debug("Request merge indexes because /admin/mergeindexes request has been received.");

		var correlationId = Guid.NewGuid();
		var envelope = new SendToHttpEnvelope(networkSendQueue, entity,
			(e, _) => e.ResponseCodec.To(new MergeIndexesResultDto(correlationId.ToString())),
			(e, message) => {
				var completed = message as MergeIndexesResponse;
				return completed?.Result switch {
					MergeIndexesResponse.MergeIndexesResult.Started => Configure.Ok(e.ResponseCodec.ContentType),
					_ => Configure.InternalServerError()
				};
			}
		);

		Publish(new MergeIndexes(envelope, correlationId, entity.User));
	}

	private void OnPostScavenge(HttpEntityManager entity, UriTemplateMatch match) {
		int startFromChunk = 0;
		var startFromChunkVariable = match.BoundVariables["startFromChunk"];
		if (startFromChunkVariable != null) {
			if (!int.TryParse(startFromChunkVariable, out startFromChunk) || startFromChunk < 0) {
				SendBadRequest(entity, "startFromChunk must be a positive integer");
				return;
			}
		}

		int threads = 1;
		var threadsVariable = match.BoundVariables["threads"];
		if (threadsVariable != null) {
			if (!int.TryParse(threadsVariable, out threads) || threads < 1) {
				SendBadRequest(entity, "threads must be a 1 or above");
				return;
			}
		}

		int? threshold = null;
		var thresholdVariable = match.BoundVariables["threshold"];
		if (thresholdVariable != null) {
			if (!int.TryParse(thresholdVariable, out var x)) {
				SendBadRequest(entity, "threshold must be an integer");
				return;
			}

			threshold = x;
		}

		int? throttlePercent = null;
		var throttlePercentVariable = match.BoundVariables["throttlePercent"];
		if (throttlePercentVariable != null) {
			if (!int.TryParse(throttlePercentVariable, out var x) || x <= 0 || x > 100) {
				SendBadRequest(entity, "throttlePercent must be between 1 and 100 inclusive");
				return;
			}

			if (x != 100 && threads > 1) {
				SendBadRequest(entity, "throttlePercent must be 100 for a multi-threaded scavenge");
				return;
			}

			throttlePercent = x;
		}

		var syncOnly = false;
		var syncOnlyVariable = match.BoundVariables["syncOnly"];
		if (syncOnlyVariable != null) {
			if (!bool.TryParse(syncOnlyVariable, out var x)) {
				SendBadRequest(entity, "syncOnly must be a boolean");
				return;
			}

			syncOnly = x;
		}

		var sb = new StringBuilder();
		var args = new List<object>();

		sb.Append("Request scavenging because /admin/scavenge");
		sb.Append("?startFromChunk={chunkStartNumber}");
		args.Add(startFromChunk);
		sb.Append("&threads={numThreads}");
		args.Add(threads);

		if (threshold != null) {
			sb.Append("&threshold={threshold}");
			args.Add(threshold);
		}

		if (throttlePercent != null) {
			sb.Append("&throttlePercent={throttlePercent}");
			args.Add(throttlePercent);
		}

		sb.Append("&syncOnly={syncOnly}");
		args.Add(syncOnly);

		sb.Append(" request has been received.");
		Log.Information(sb.ToString(), args.ToArray());

		var envelope = new SendToHttpEnvelope<ScavengeDatabaseStartedResponse>(
			networkSendQueue, entity,
			(e, message) => e.To(new ScavengeResultDto(message?.ScavengeId)),
			(e, _) => Configure.Ok(e.ContentType),
			CreateErrorEnvelope(entity)
		);

		Publish(new ScavengeDatabase(
			envelope: envelope,
			correlationId: Guid.Empty,
			user: entity.User,
			startFromChunk: startFromChunk,
			threads: threads,
			threshold: threshold,
			throttlePercent: throttlePercent,
			syncOnly: syncOnly));
	}

	private void OnStopScavenge(HttpEntityManager entity, UriTemplateMatch match) {
		var scavengeId = match.BoundVariables["scavengeId"];

		Log.Information("Stopping scavenge because /admin/scavenge/{scavengeId} DELETE request has been received.", scavengeId);

		var envelope = new SendToHttpEnvelope<ScavengeDatabaseStoppedResponse>(
			networkSendQueue, entity,
			(e, message) => e.To(message?.ScavengeId),
			(e, _) => Configure.Ok(e.ContentType),
			CreateErrorEnvelope(entity)
		);

		Publish(new StopDatabaseScavenge(envelope, Guid.Empty, entity.User, scavengeId));
	}

	private void OnGetCurrentScavenge(HttpEntityManager entity, UriTemplateMatch match) {
		Log.Verbose("/admin/scavenge/current GET request has been received.");

		var envelope = new SendToHttpEnvelope<ScavengeDatabaseGetCurrentResponse>(
			networkSendQueue,
			entity,
			(e, message) => {
				var result = new ScavengeGetCurrentResultDto();

				if (message is not null &&
				    message.Result == ScavengeDatabaseGetCurrentResponse.ScavengeResult.InProgress &&
				    message.ScavengeId is not null) {
					result.ScavengeId = message.ScavengeId;
					result.ScavengeLink = $"/admin/scavenge/{message.ScavengeId}";
				}

				return e.To(result);
			},
			(e, _) => Configure.Ok(e.ContentType),
			CreateErrorEnvelope(entity)
		);

		Publish(new GetCurrentDatabaseScavenge(envelope, Guid.Empty, entity.User));
	}

	private void OnGetLastScavenge(HttpEntityManager entity, UriTemplateMatch match) {
		Log.Verbose("/admin/scavenge/last GET request has been received.");

		var envelope = new SendToHttpEnvelope<ScavengeDatabaseGetLastResponse>(
			networkSendQueue,
			entity,
			(e, message) => {
				var result = new ScavengeGetLastResultDto();
				if (message.ScavengeId is not null) {
					result.ScavengeId = message.ScavengeId;
					result.ScavengeLink = $"/admin/scavenge/{message.ScavengeId}";
				}

				result.ScavengeResult = message.Result.ToString();

				return e.To(result);
			},
			(e, _) => Configure.Ok(e.ContentType),
			CreateErrorEnvelope(entity)
		);

		Publish(new GetLastDatabaseScavenge(envelope, Guid.Empty, entity.User));
	}

	private void OnSetNodePriority(HttpEntityManager entity, UriTemplateMatch match) {
		if (IsUnauthorized(entity)) {
			ReplyUnauthorised(entity);
			return;
		}

		Log.Information("Request to set node priority.");

		var nodePriorityVariable = match.BoundVariables["nodePriority"];
		if (nodePriorityVariable == null) {
			SendBadRequest(entity, "Could not find expected `nodePriority` in the request body.");
			return;
		}

		if (!int.TryParse(nodePriorityVariable, out var nodePriority)) {
			SendBadRequest(entity, "nodePriority must be an integer.");
			return;
		}

		Publish(new SetNodePriority(nodePriority));
		entity.ReplyStatus(HttpStatusCode.OK, "OK", LogReplyError);
	}

	private void OnResignNode(HttpEntityManager entity, UriTemplateMatch match) {
		if (IsUnauthorized(entity)) {
			ReplyUnauthorised(entity);
			return;
		}

		Log.Information("Request to resign node.");
		Publish(new ResignNode());
		entity.ReplyStatus(HttpStatusCode.OK, "OK", LogReplyError);
	}

	private static void OnGetLogin(HttpEntityManager entity, UriTemplateMatch match) {
		var message = new UserManagementMessage.UserDetailsResult(
			new UserManagementMessage.UserData(
				entity.User.Identity.Name,
				entity.User.Identity.Name,
				entity.User.Claims.Where(x => x.Type == ClaimTypes.Role).Select(x => x.Value).ToArray(),
				false,
				new DateTimeOffset(DateTime.UtcNow)));

		entity.ReplyTextContent(
			message.ToJson(),
			HttpStatusCode.OK,
			"",
			ContentType.Json,
			new List<KeyValuePair<string, string>>(),
			e => Log.Error(e, "Error while writing HTTP response"));
	}

	private IEnvelope CreateErrorEnvelope(HttpEntityManager http) {
		return new SendToHttpEnvelope<ScavengeDatabaseInProgressResponse>(
			networkSendQueue,
			http,
			ScavengeInProgressFormatter,
			ScavengeInProgressConfigurator,
			new SendToHttpEnvelope<ScavengeDatabaseNotFoundResponse>(
				networkSendQueue,
				http,
				ScavengeNotFoundFormatter,
				ScavengeNotFoundConfigurator,
				new SendToHttpEnvelope<ScavengeDatabaseUnauthorizedResponse>(
					networkSendQueue,
					http,
					ScavengeUnauthorizedFormatter,
					ScavengeUnauthorizedConfigurator,
					null)));
	}

	private static ResponseConfiguration ScavengeInProgressConfigurator(ICodec codec, ScavengeDatabaseInProgressResponse message) {
		return new(HttpStatusCode.BadRequest, "Bad Request", "text/plain", Helper.UTF8NoBom);
	}

	private static string ScavengeInProgressFormatter(ICodec codec, ScavengeDatabaseInProgressResponse message) {
		return message.Reason;
	}

	private static ResponseConfiguration ScavengeNotFoundConfigurator(ICodec codec, ScavengeDatabaseNotFoundResponse message) {
		return new(HttpStatusCode.NotFound, "Not Found", "text/plain", Helper.UTF8NoBom);
	}

	private static string ScavengeNotFoundFormatter(ICodec codec, ScavengeDatabaseNotFoundResponse message) {
		return message.Reason;
	}

	private static ResponseConfiguration ScavengeUnauthorizedConfigurator(ICodec codec, ScavengeDatabaseUnauthorizedResponse message) {
		return new(HttpStatusCode.Unauthorized, "Unauthorized", "text/plain", Helper.UTF8NoBom);
	}

	private static string ScavengeUnauthorizedFormatter(ICodec codec, ScavengeDatabaseUnauthorizedResponse message) {
		return message.Reason;
	}

	private static void LogReplyError(Exception exc) {
		Log.Debug("Error while closing HTTP connection (admin controller): {e}.", exc.Message);
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
			networkSendQueue, manager,
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

	private void GetStreamEventsBackwardScavenges(HttpEntityManager manager, UriTemplateMatch match) {
		if (GetDescriptionDocument(manager, match))
			return;
		var stream = "$scavenges";
		var evNum = match.BoundVariables["event"];
		var cnt = match.BoundVariables["count"];
		var scavengeId = match.BoundVariables["scavengeId"];

		long eventNumber = -1;
		int count = AtomSpecs.FeedPageSize;
		var embed = GetEmbedLevel(manager, match);

		if (scavengeId != null)
			stream = stream + "-" + scavengeId;

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

	private void GetStreamEventsBackward(HttpEntityManager manager, string stream, long eventNumber, int count,
		bool resolveLinkTos, bool requireLeader, bool headOfStream, EmbedLevel embed) {
		var envelope = new SendToHttpEnvelope(networkSendQueue,
			manager,
			(ent, msg) => Format.GetStreamEventsBackward(ent, msg, embed, headOfStream),
			(args, msg) => Configure.GetStreamEventsBackward(args, msg, headOfStream));
		var corrId = Guid.NewGuid();
		Publish(new ReadStreamEventsBackward(corrId, corrId, envelope, stream, eventNumber, count,
			resolveLinkTos, requireLeader, GetETagStreamVersion(manager), manager.User));
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
}
