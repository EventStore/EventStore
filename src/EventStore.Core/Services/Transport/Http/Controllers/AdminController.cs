using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Plugins.Authorization;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Atom;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using Microsoft.Extensions.Primitives;
using Serilog;

namespace EventStore.Core.Services.Transport.Http.Controllers {
	public class AdminController : CommunicationController {
		private readonly IPublisher _networkSendQueue;
		private static readonly ILogger Log = Serilog.Log.ForContext<AdminController>();

		private static readonly ICodec[] SupportedCodecs = new ICodec[]
			{Codec.Text, Codec.Json, Codec.Xml, Codec.ApplicationXml};
		
		public static readonly char[] ETagSeparatorArray = { ';' };
		
		private static readonly Func<UriTemplateMatch, Operation> ReadStreamOperationForScavengeStream =
			ForScavengeStream(Operations.Streams.Read);
		public AdminController(IPublisher publisher, IPublisher networkSendQueue) : base(publisher) {
			_networkSendQueue = networkSendQueue;
		}

		protected override void SubscribeCore(IHttpService service) {
			service.RegisterAction(
				new ControllerAction("/admin/shutdown", HttpMethod.Post, Codec.NoCodecs, SupportedCodecs, new Operation(Operations.Node.Shutdown)),
				OnPostShutdown);
			service.RegisterAction(
				new ControllerAction("/admin/reloadconfig", HttpMethod.Post, Codec.NoCodecs, SupportedCodecs, new Operation(Operations.Node.ReloadConfiguration)),
				OnPostReloadConfig);
			service.RegisterAction(
				new ControllerAction("/admin/scavenge?startFromChunk={startFromChunk}&threads={threads}",
					HttpMethod.Post, Codec.NoCodecs, SupportedCodecs, new Operation(Operations.Node.Scavenge.Start)), OnPostScavenge);
			service.RegisterAction(
				new ControllerAction("/admin/scavenge/{scavengeId}", HttpMethod.Delete, Codec.NoCodecs,
					SupportedCodecs, new Operation(Operations.Node.Scavenge.Stop)), OnStopScavenge);
			service.RegisterAction(
				new ControllerAction("/admin/mergeindexes", HttpMethod.Post, Codec.NoCodecs, SupportedCodecs, new Operation(Operations.Node.MergeIndexes)),
				OnPostMergeIndexes);
			service.RegisterAction(
				new ControllerAction("/admin/node/priority/{nodePriority}", HttpMethod.Post, Codec.NoCodecs, SupportedCodecs, new Operation(Operations.Node.SetPriority)),
				OnSetNodePriority);
			service.RegisterAction(
				new ControllerAction("/admin/node/resign", HttpMethod.Post, Codec.NoCodecs, SupportedCodecs, new Operation(Operations.Node.Resign)),
				OnResignNode);
			service.RegisterAction(
				new ControllerAction("/admin/login", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, new Operation(Operations.Node.Login)),
				OnGetLogin);
			Register(service, "/streams/$scavenges/{scavengeId}/{event}/{count}?embed={embed}", HttpMethod.Get, GetStreamEventsBackwardScavenges, Codec.NoCodecs,
				SupportedCodecs, ReadStreamOperationForScavengeStream);
			Register(service, "/streams/$scavenges?embed={embed}", HttpMethod.Get, GetStreamEventsBackwardScavenges, Codec.NoCodecs,
				SupportedCodecs, ReadStreamOperationForScavengeStream);
		}
	
		private static Func<UriTemplateMatch, Operation> ForScavengeStream(OperationDefinition definition) {
			return match => {
				var operation = new Operation(definition);
				var stream = "$scavenges";
				var scavengeId = match.BoundVariables["scavengeId"];
				if (scavengeId != null) 
					stream = stream + "-" + scavengeId;
				
				if (!string.IsNullOrEmpty(stream)) {
					return operation.WithParameter(Operations.Streams.Parameters.StreamId(stream));
				}

				return operation;
			};
		}
		
		private void OnPostShutdown(HttpEntityManager entity, UriTemplateMatch match) {
			if (entity.User != null &&
			    (entity.User.LegacyRoleCheck(SystemRoles.Admins) || entity.User.LegacyRoleCheck(SystemRoles.Operations))) {
				Log.Information("Request shut down of node because shutdown command has been received.");
				Publish(new ClientMessage.RequestShutdown(exitProcess: true, shutdownHttp: true, "Received shutdown command"));
				entity.ReplyStatus(HttpStatusCode.OK, "OK", LogReplyError);
			} else {
				entity.ReplyStatus(HttpStatusCode.Unauthorized, "Unauthorized", LogReplyError);
			}
		}

		private void OnPostReloadConfig(HttpEntityManager entity, UriTemplateMatch match) {
			if (entity.User != null &&
			    (entity.User.LegacyRoleCheck(SystemRoles.Admins) || entity.User.LegacyRoleCheck(SystemRoles.Operations))) {
				Log.Information("Reloading the node's configuration since a request has been received on /admin/reloadconfig.");
				Publish(new ClientMessage.ReloadConfig());
				entity.ReplyStatus(HttpStatusCode.OK, "OK", LogReplyError);
			} else {
				entity.ReplyStatus(HttpStatusCode.Unauthorized, "Unauthorized", LogReplyError);
			}
		}

		private void OnPostMergeIndexes(HttpEntityManager entity, UriTemplateMatch match) {
			Log.Information("Request merge indexes because /admin/mergeindexes request has been received.");

			var correlationId = Guid.NewGuid();
			var envelope = new SendToHttpEnvelope(_networkSendQueue, entity,
				(e, message) => { return e.ResponseCodec.To(new MergeIndexesResultDto(correlationId.ToString())); },
				(e, message) => {
					var completed = message as ClientMessage.MergeIndexesResponse;
					switch (completed?.Result) {
						case ClientMessage.MergeIndexesResponse.MergeIndexesResult.Started:
							return Configure.Ok(e.ResponseCodec.ContentType);
						default:
							return Configure.InternalServerError();
					}
				}
			);

			Publish(new ClientMessage.MergeIndexes(envelope, correlationId, entity.User));
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

			Log.Information(
				"Request scavenging because /admin/scavenge?startFromChunk={chunkStartNumber}&threads={numThreads} request has been received.",
				startFromChunk, threads);

			var envelope = new SendToHttpEnvelope(_networkSendQueue, entity, (e, message) => {
					var completed = message as ClientMessage.ScavengeDatabaseResponse;
					return e.ResponseCodec.To(new ScavengeResultDto(completed?.ScavengeId));
				},
				(e, message) => {
					var completed = message as ClientMessage.ScavengeDatabaseResponse;
					switch (completed?.Result) {
						case ClientMessage.ScavengeDatabaseResponse.ScavengeResult.Started:
							return Configure.Ok(e.ResponseCodec.ContentType);
						case ClientMessage.ScavengeDatabaseResponse.ScavengeResult.InProgress:
							return Configure.BadRequest();
						case ClientMessage.ScavengeDatabaseResponse.ScavengeResult.Unauthorized:
							return Configure.Unauthorized();
						default:
							return Configure.InternalServerError();
					}
				}
			);

			Publish(new ClientMessage.ScavengeDatabase(envelope, Guid.Empty, entity.User, startFromChunk, threads));
		}

		private void OnStopScavenge(HttpEntityManager entity, UriTemplateMatch match) {
			var scavengeId = match.BoundVariables["scavengeId"];

			Log.Information("Stopping scavenge because /admin/scavenge/{scavengeId} DELETE request has been received.",
				scavengeId);

			var envelope = new SendToHttpEnvelope(_networkSendQueue, entity, (e, message) => {
					var completed = message as ClientMessage.ScavengeDatabaseResponse;
					return e.ResponseCodec.To(completed?.ScavengeId);
				},
				(e, message) => {
					var completed = message as ClientMessage.ScavengeDatabaseResponse;
					switch (completed?.Result) {
						case ClientMessage.ScavengeDatabaseResponse.ScavengeResult.Stopped:
							return Configure.Ok(e.ResponseCodec.ContentType);
						case ClientMessage.ScavengeDatabaseResponse.ScavengeResult.Unauthorized:
							return Configure.Unauthorized();
						case ClientMessage.ScavengeDatabaseResponse.ScavengeResult.InvalidScavengeId:
							return Configure.NotFound();
						default:
							return Configure.InternalServerError();
					}
				}
			);

			Publish(new ClientMessage.StopDatabaseScavenge(envelope, Guid.Empty, entity.User, scavengeId));
		}

		private void OnSetNodePriority(HttpEntityManager entity, UriTemplateMatch match) {
			if (entity.User != null &&
			    (entity.User.LegacyRoleCheck(SystemRoles.Admins) || entity.User.LegacyRoleCheck(SystemRoles.Operations))) {
				Log.Information("Request to set node priority.");

				int nodePriority;
				var nodePriorityVariable = match.BoundVariables["nodePriority"];
				if (nodePriorityVariable == null) {
					SendBadRequest(entity, "Could not find expected `nodePriority` in the request body.");
					return;
				}

				if (!int.TryParse(nodePriorityVariable, out nodePriority)) {
					SendBadRequest(entity, "nodePriority must be an integer.");
					return;
				}

				Publish(new ClientMessage.SetNodePriority(nodePriority));
				entity.ReplyStatus(HttpStatusCode.OK, "OK", LogReplyError);
			} else {
				entity.ReplyStatus(HttpStatusCode.Unauthorized, "Unauthorized", LogReplyError);
			}
		}

		private void OnResignNode(HttpEntityManager entity, UriTemplateMatch match) {
			if (entity.User != null &&
			    (entity.User.LegacyRoleCheck(SystemRoles.Admins) || entity.User.LegacyRoleCheck(SystemRoles.Operations))) {
				Log.Information("Request to resign node.");
				Publish(new ClientMessage.ResignNode());
				entity.ReplyStatus(HttpStatusCode.OK, "OK", LogReplyError);
			} else {
				entity.ReplyStatus(HttpStatusCode.Unauthorized, "Unauthorized", LogReplyError);
			}
		}
		
		private void OnGetLogin(HttpEntityManager entity, UriTemplateMatch match) {
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
		
		private void LogReplyError(Exception exc) {
			Log.Debug("Error while closing HTTP connection (admin controller): {e}.", exc.Message);
		}
			private bool GetDescriptionDocument(HttpEntityManager manager, UriTemplateMatch match) {
			if (manager.ResponseCodec.ContentType == ContentType.DescriptionDocJson) {
				var stream = match.BoundVariables["stream"];
				var accepts = (manager.HttpEntity.Request.AcceptTypes?.Length ?? 0) == 0 ||
				              manager.HttpEntity.Request.AcceptTypes.Contains(ContentType.Any);
				var responseStatusCode = accepts ? HttpStatusCode.NotAcceptable : HttpStatusCode.OK;
				var responseMessage = manager.HttpEntity.Request.AcceptTypes == null
					? "We are unable to represent the stream in the format requested."
					: "Description Document";
				var envelope = new SendToHttpEnvelope(
					_networkSendQueue, manager,
					(args, message) => {
						var m = message as MonitoringMessage.GetPersistentSubscriptionStatsCompleted;
						if (m == null)
							throw new Exception("Could not get subscriptions for stream " + stream);

						string[] persistentSubscriptionGroups = null;
						if (m.Result == MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus
							    .Success) {
							persistentSubscriptionGroups = m.SubscriptionStats.Select(x => x.GroupName).ToArray();
						}

						manager.ReplyTextContent(
							Format.GetDescriptionDocument(manager, stream, persistentSubscriptionGroups),
							responseStatusCode, responseMessage,
							manager.ResponseCodec.ContentType,
							null,
							e => Log.Error(e, "Error while writing HTTP response"));
						return String.Empty;
					},
					(args, message) => new ResponseConfiguration(HttpStatusCode.OK, manager.ResponseCodec.ContentType,
						manager.ResponseCodec.Encoding));
				var cmd = new MonitoringMessage.GetStreamPersistentSubscriptionStats(envelope, stream);
				Publish(cmd);
				return true;
			}

			return false;
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
				SendBadRequest(manager, string.Format("Invalid stream name '{0}'", stream));
				return;
			}

			if (evNum != null && evNum != "head" && (!long.TryParse(evNum, out eventNumber) || eventNumber < 0)) {
				SendBadRequest(manager, string.Format("'{0}' is not valid event number", evNum));
				return;
			}

			if (cnt.IsNotEmptyString() && (!int.TryParse(cnt, out count) || count <= 0)) {
				SendBadRequest(manager, string.Format("'{0}' is not valid count. Should be positive integer", cnt));
				return;
			}

			bool resolveLinkTos;
			if (!GetResolveLinkTos(manager, out resolveLinkTos, true)) {
				SendBadRequest(manager, string.Format("{0} header in wrong format.", SystemHeaders.ResolveLinkTos));
				return;
			}

			if (!GetRequireLeader(manager, out var requireLeader)) {
				SendBadRequest(manager, string.Format("{0} header in wrong format.", SystemHeaders.RequireLeader));
				return;
			}

			bool headOfStream = eventNumber == -1;
			GetStreamEventsBackward(manager, stream, eventNumber, count, resolveLinkTos, requireLeader, headOfStream,
				embed);
		}
		private void GetStreamEventsBackward(HttpEntityManager manager, string stream, long eventNumber, int count,
			bool resolveLinkTos, bool requireLeader, bool headOfStream, EmbedLevel embed) {
			var envelope = new SendToHttpEnvelope(_networkSendQueue,
				manager,
				(ent, msg) =>
					Format.GetStreamEventsBackward(ent, msg, embed, headOfStream),
				(args, msg) => Configure.GetStreamEventsBackward(args, msg, headOfStream));
			var corrId = Guid.NewGuid();
			Publish(new ClientMessage.ReadStreamEventsBackward(corrId, corrId, envelope, stream, eventNumber, count,
				resolveLinkTos, requireLeader, GetETagStreamVersion(manager), manager.User));
		}
		private static EmbedLevel GetEmbedLevel(HttpEntityManager manager, UriTemplateMatch match,
			EmbedLevel htmlLevel = EmbedLevel.PrettyBody) {
			if (manager.ResponseCodec is IRichAtomCodec)
				return htmlLevel;
			var rawValue = match.BoundVariables["embed"] ?? string.Empty;
			switch (rawValue.ToLowerInvariant()) {
				case "content":
					return EmbedLevel.Content;
				case "rich":
					return EmbedLevel.Rich;
				case "body":
					return EmbedLevel.Body;
				case "pretty":
					return EmbedLevel.PrettyBody;
				case "tryharder":
					return EmbedLevel.TryHarder;
				default:
					return EmbedLevel.None;
			}
		}
		private bool GetResolveLinkTos(HttpEntityManager manager, out bool resolveLinkTos, bool defaultOption = false) {
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
		private bool GetRequireLeader(HttpEntityManager manager, out bool requireLeader) {
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
		private long? GetETagStreamVersion(HttpEntityManager manager) {
			var etag = manager.HttpEntity.Request.GetHeaderValues("If-None-Match");
			if (!StringValues.IsNullOrEmpty(etag)) {
				// etag format is version;contenttypehash
				var splitted = etag.ToString().Trim('\"').Split(ETagSeparatorArray);
				if (splitted.Length == 2) {
					var typeHash = manager.ResponseCodec.ContentType.GetHashCode()
						.ToString(CultureInfo.InvariantCulture);
					var res = splitted[1] == typeHash && long.TryParse(splitted[0], out var streamVersion)
						? (long?)streamVersion
						: null;
					return res;
				}
			}

			return null;
		}
	}
}
