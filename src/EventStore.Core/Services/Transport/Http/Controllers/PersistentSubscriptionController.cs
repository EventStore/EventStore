using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using ClientMessages = EventStore.Core.Messages.ClientMessage.PersistentSubscriptionNackEvents;
using EventStore.Core.Services.PersistentSubscription;
using EventStore.Core.Data;
using EventStore.Common.Utils;
using EventStore.Transport.Http.Atom;

namespace EventStore.Core.Services.Transport.Http.Controllers {
	public class PersistentSubscriptionController : CommunicationController {
		private readonly IHttpForwarder _httpForwarder;
		private readonly IPublisher _networkSendQueue;
		private const int DefaultNumberOfMessagesToGet = 1;
		private static readonly ICodec[] DefaultCodecs = {Codec.Json, Codec.Xml};

		private static readonly ICodec[] AtomCodecs = {
			Codec.CompetingXml,
			Codec.CompetingJson,
		};

		private static readonly ILogger Log = LogManager.GetLoggerFor<PersistentSubscriptionController>();

		public PersistentSubscriptionController(IHttpForwarder httpForwarder, IPublisher publisher,
			IPublisher networkSendQueue)
			: base(publisher) {
			_httpForwarder = httpForwarder;
			_networkSendQueue = networkSendQueue;
		}

		protected override void SubscribeCore(IHttpService service) {
			Register(service, "/subscriptions", HttpMethod.Get, GetAllSubscriptionInfo, Codec.NoCodecs, DefaultCodecs);
			Register(service, "/subscriptions/{stream}", HttpMethod.Get, GetSubscriptionInfoForStream, Codec.NoCodecs,
				DefaultCodecs);
			Register(service, "/subscriptions/{stream}/{subscription}", HttpMethod.Put, PutSubscription, DefaultCodecs,
				DefaultCodecs);
			Register(service, "/subscriptions/{stream}/{subscription}", HttpMethod.Post, PostSubscription,
				DefaultCodecs, DefaultCodecs);
			RegisterUrlBased(service, "/subscriptions/{stream}/{subscription}", HttpMethod.Delete, DeleteSubscription);
			Register(service, "/subscriptions/{stream}/{subscription}", HttpMethod.Get, GetNextNMessages,
				Codec.NoCodecs, AtomCodecs);
			Register(service, "/subscriptions/{stream}/{subscription}?embed={embed}", HttpMethod.Get, GetNextNMessages,
				Codec.NoCodecs, AtomCodecs);
			Register(service, "/subscriptions/{stream}/{subscription}/{count}?embed={embed}", HttpMethod.Get,
				GetNextNMessages, Codec.NoCodecs, AtomCodecs);
			Register(service, "/subscriptions/{stream}/{subscription}/info", HttpMethod.Get, GetSubscriptionInfo,
				Codec.NoCodecs, DefaultCodecs);
			RegisterUrlBased(service, "/subscriptions/{stream}/{subscription}/ack/{messageid}", HttpMethod.Post,
				AckMessage);
			RegisterUrlBased(service, "/subscriptions/{stream}/{subscription}/nack/{messageid}?action={action}",
				HttpMethod.Post, NackMessage);
			RegisterUrlBased(service, "/subscriptions/{stream}/{subscription}/ack?ids={messageids}", HttpMethod.Post,
				AckMessages);
			RegisterUrlBased(service, "/subscriptions/{stream}/{subscription}/nack?ids={messageids}&action={action}",
				HttpMethod.Post, NackMessages);
			RegisterUrlBased(service, "/subscriptions/{stream}/{subscription}/replayParked", HttpMethod.Post,
				ReplayParkedMessages);
		}

		private static ClientMessages.NakAction GetNackAction(HttpEntityManager manager, UriTemplateMatch match,
			NakAction nakAction = NakAction.Unknown) {
			var rawValue = match.BoundVariables["action"] ?? string.Empty;
			switch (rawValue.ToLowerInvariant()) {
				case "park": return ClientMessages.NakAction.Park;
				case "retry": return ClientMessages.NakAction.Retry;
				case "skip": return ClientMessages.NakAction.Skip;
				case "stop": return ClientMessages.NakAction.Stop;
				default: return ClientMessages.NakAction.Unknown;
			}
		}

		private void AckMessages(HttpEntityManager http, UriTemplateMatch match) {
			if (_httpForwarder.ForwardRequest(http))
				return;
			var envelope = new NoopEnvelope();
			var groupname = match.BoundVariables["subscription"];
			var stream = match.BoundVariables["stream"];
			var messageIds = match.BoundVariables["messageIds"];
			var ids = new List<Guid>();
			foreach (var messageId in messageIds.Split(new[] {','})) {
				Guid id;
				if (!Guid.TryParse(messageId, out id)) {
					http.ReplyStatus(HttpStatusCode.BadRequest, "messageid should be a properly formed guid",
						exception => { });
					return;
				}

				ids.Add(id);
			}

			var cmd = new ClientMessage.PersistentSubscriptionAckEvents(
				Guid.NewGuid(),
				Guid.NewGuid(),
				envelope,
				BuildSubscriptionGroupKey(stream, groupname),
				ids.ToArray(),
				http.User);
			Publish(cmd);
			http.ReplyStatus(HttpStatusCode.Accepted, "", exception => { });
		}

		private void NackMessages(HttpEntityManager http, UriTemplateMatch match) {
			if (_httpForwarder.ForwardRequest(http))
				return;
			var envelope = new NoopEnvelope();
			var groupname = match.BoundVariables["subscription"];
			var stream = match.BoundVariables["stream"];
			var messageIds = match.BoundVariables["messageIds"];
			var nakAction = GetNackAction(http, match);
			var ids = new List<Guid>();
			foreach (var messageId in messageIds.Split(new[] {','})) {
				Guid id;
				if (!Guid.TryParse(messageId, out id)) {
					http.ReplyStatus(HttpStatusCode.BadRequest, "messageid should be a properly formed guid",
						exception => { });
					return;
				}

				ids.Add(id);
			}

			var cmd = new ClientMessage.PersistentSubscriptionNackEvents(
				Guid.NewGuid(),
				Guid.NewGuid(),
				envelope,
				BuildSubscriptionGroupKey(stream, groupname),
				"Nacked from HTTP",
				nakAction,
				ids.ToArray(),
				http.User);
			Publish(cmd);
			http.ReplyStatus(HttpStatusCode.Accepted, "", exception => { });
		}

		private static string BuildSubscriptionGroupKey(string stream, string groupName) {
			return stream + "::" + groupName;
		}

		private void AckMessage(HttpEntityManager http, UriTemplateMatch match) {
			if (_httpForwarder.ForwardRequest(http))
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

			var cmd = new ClientMessage.PersistentSubscriptionAckEvents(
				Guid.NewGuid(),
				Guid.NewGuid(),
				envelope,
				BuildSubscriptionGroupKey(stream, groupname),
				new[] {id},
				http.User);
			Publish(cmd);
			http.ReplyStatus(HttpStatusCode.Accepted, "", exception => { });
		}

		private void NackMessage(HttpEntityManager http, UriTemplateMatch match) {
			if (_httpForwarder.ForwardRequest(http))
				return;
			var envelope = new NoopEnvelope();
			var groupname = match.BoundVariables["subscription"];
			var stream = match.BoundVariables["stream"];
			var messageId = match.BoundVariables["messageId"];
			var nakAction = GetNackAction(http, match);
			var id = Guid.NewGuid();
			if (!Guid.TryParse(messageId, out id)) {
				http.ReplyStatus(HttpStatusCode.BadRequest, "messageid should be a properly formed guid",
					exception => { });
				return;
			}

			var cmd = new ClientMessage.PersistentSubscriptionNackEvents(
				Guid.NewGuid(),
				Guid.NewGuid(),
				envelope,
				BuildSubscriptionGroupKey(stream, groupname),
				"Nacked from HTTP",
				nakAction,
				new[] {id},
				http.User);
			Publish(cmd);
			http.ReplyStatus(HttpStatusCode.Accepted, "", exception => { });
		}

		private void ReplayParkedMessages(HttpEntityManager http, UriTemplateMatch match) {
			if (_httpForwarder.ForwardRequest(http))
				return;
			var envelope = new SendToHttpEnvelope(
				_networkSendQueue, http,
				(args, message) => http.ResponseCodec.To(message),
				(args, message) => {
					int code;
					var m = message as ClientMessage.ReplayMessagesReceived;
					if (m == null) throw new Exception("unexpected message " + message);
					switch (m.Result) {
						case ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.Success:
							code = HttpStatusCode.OK;
							break;
						case ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.DoesNotExist:
							code = HttpStatusCode.NotFound;
							break;
						case ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.AccessDenied:
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
			var cmd = new ClientMessage.ReplayAllParkedMessages(Guid.NewGuid(), Guid.NewGuid(), envelope, stream,
				groupname, http.User);
			Publish(cmd);
		}

		private void PutSubscription(HttpEntityManager http, UriTemplateMatch match) {
			if (_httpForwarder.ForwardRequest(http))
				return;
			var groupname = match.BoundVariables["subscription"];
			var stream = match.BoundVariables["stream"];
			var envelope = new SendToHttpEnvelope(
				_networkSendQueue, http,
				(args, message) => http.ResponseCodec.To(message),
				(args, message) => {
					int code;
					var m = message as ClientMessage.CreatePersistentSubscriptionCompleted;
					if (m == null) throw new Exception("unexpected message " + message);
					switch (m.Result) {
						case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult
							.Success:
							code = HttpStatusCode.Created;
							break;
						case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult
							.AlreadyExists:
							code = HttpStatusCode.Conflict;
							break;
						case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult
							.AccessDenied:
							code = HttpStatusCode.Unauthorized;
							break;
						default:
							code = HttpStatusCode.InternalServerError;
							break;
					}

					return new ResponseConfiguration(code, http.ResponseCodec.ContentType,
						http.ResponseCodec.Encoding,
						new KeyValuePair<string, string>("location",
							MakeUrl(http, "/subscriptions/" + stream + "/" + groupname)));
				});
			http.ReadTextRequestAsync(
				(o, s) => {
					var data = http.RequestCodec.From<SubscriptionConfigData>(s);
					var config = ParseConfig(data);
					if (!ValidateConfig(config, http)) return;
					var message = new ClientMessage.CreatePersistentSubscription(Guid.NewGuid(),
						Guid.NewGuid(),
						envelope,
						stream,
						groupname,
						config.ResolveLinktos,
						config.StartFrom,
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
						http.User,
						"",
						"");
					Publish(message);
				}, x => Log.DebugException(x, "Reply Text Content Failed."));
		}

		private void PostSubscription(HttpEntityManager http, UriTemplateMatch match) {
			if (_httpForwarder.ForwardRequest(http))
				return;
			var groupname = match.BoundVariables["subscription"];
			var stream = match.BoundVariables["stream"];
			var envelope = new SendToHttpEnvelope(
				_networkSendQueue, http,
				(args, message) => http.ResponseCodec.To(message),
				(args, message) => {
					int code;
					var m = message as ClientMessage.UpdatePersistentSubscriptionCompleted;
					if (m == null) throw new Exception("unexpected message " + message);
					switch (m.Result) {
						case ClientMessage.UpdatePersistentSubscriptionCompleted.UpdatePersistentSubscriptionResult
							.Success:
							code = HttpStatusCode.OK;
							//TODO competing return uri to subscription
							break;
						case ClientMessage.UpdatePersistentSubscriptionCompleted.UpdatePersistentSubscriptionResult
							.DoesNotExist:
							code = HttpStatusCode.NotFound;
							break;
						case ClientMessage.UpdatePersistentSubscriptionCompleted.UpdatePersistentSubscriptionResult
							.AccessDenied:
							code = HttpStatusCode.Unauthorized;
							break;
						default:
							code = HttpStatusCode.InternalServerError;
							break;
					}

					return new ResponseConfiguration(code, http.ResponseCodec.ContentType,
						http.ResponseCodec.Encoding,
						new KeyValuePair<string, string>("location",
							MakeUrl(http, "/subscriptions/" + stream + "/" + groupname)));
				});
			http.ReadTextRequestAsync(
				(o, s) => {
					var data = http.RequestCodec.From<SubscriptionConfigData>(s);
					var config = ParseConfig(data);
					if (!ValidateConfig(config, http)) return;
					var message = new ClientMessage.UpdatePersistentSubscription(Guid.NewGuid(),
						Guid.NewGuid(),
						envelope,
						stream,
						groupname,
						config.ResolveLinktos,
						config.StartFrom,
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
						http.User,
						"",
						"");
					Publish(message);
				}, x => Log.DebugException(x, "Reply Text Content Failed."));
		}

		private SubscriptionConfigData ParseConfig(SubscriptionConfigData config) {
			if (config == null) {
				return new SubscriptionConfigData();
			}

			return new SubscriptionConfigData {
				ResolveLinktos = config.ResolveLinktos,
				StartFrom = config.StartFrom,
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
				SendBadRequest(
					http,
					string.Format(
						"Buffer Size ({0}) must be positive",
						config.BufferSize));
				return false;
			}

			if (config.LiveBufferSize <= 0) {
				SendBadRequest(
					http,
					string.Format(
						"Live Buffer Size ({0}) must be positive",
						config.LiveBufferSize));
				return false;
			}

			if (config.ReadBatchSize <= 0) {
				SendBadRequest(
					http,
					string.Format(
						"Read Batch Size ({0}) must be positive",
						config.ReadBatchSize));
				return false;
			}

			if (!(config.BufferSize > config.ReadBatchSize)) {
				SendBadRequest(
					http,
					string.Format(
						"BufferSize ({0}) must be larger than ReadBatchSize ({1})",
						config.BufferSize, config.ReadBatchSize));
				return false;
			}

			return true;
		}

		private static string CalculateNamedConsumerStrategyForOldClients(SubscriptionConfigData data) {
			var namedConsumerStrategy = data == null ? null : data.NamedConsumerStrategy;
			if (string.IsNullOrEmpty(namedConsumerStrategy)) {
				var preferRoundRobin = data == null || data.PreferRoundRobin;
				namedConsumerStrategy = preferRoundRobin
					? SystemConsumerStrategies.RoundRobin
					: SystemConsumerStrategies.DispatchToSingle;
			}

			return namedConsumerStrategy;
		}

		private void DeleteSubscription(HttpEntityManager http, UriTemplateMatch match) {
			if (_httpForwarder.ForwardRequest(http))
				return;
			var envelope = new SendToHttpEnvelope(
				_networkSendQueue, http,
				(args, message) => http.ResponseCodec.To(message),
				(args, message) => {
					int code;
					var m = message as ClientMessage.DeletePersistentSubscriptionCompleted;
					if (m == null) throw new Exception("unexpected message " + message);
					switch (m.Result) {
						case ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult
							.Success:
							code = HttpStatusCode.OK;
							break;
						case ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult
							.DoesNotExist:
							code = HttpStatusCode.NotFound;
							break;
						case ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult
							.AccessDenied:
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
			var cmd = new ClientMessage.DeletePersistentSubscription(Guid.NewGuid(), Guid.NewGuid(), envelope, stream,
				groupname, http.User);
			Publish(cmd);
		}

		private void GetAllSubscriptionInfo(HttpEntityManager http, UriTemplateMatch match) {
			if (_httpForwarder.ForwardRequest(http))
				return;
			var envelope = new SendToHttpEnvelope(
				_networkSendQueue, http,
				(args, message) =>
					http.ResponseCodec.To(ToSummaryDto(http,
						message as MonitoringMessage.GetPersistentSubscriptionStatsCompleted).ToArray()),
				(args, message) => StatsConfiguration(http, message));
			var cmd = new MonitoringMessage.GetAllPersistentSubscriptionStats(envelope);
			Publish(cmd);
		}

		private void GetSubscriptionInfoForStream(HttpEntityManager http, UriTemplateMatch match) {
			if (_httpForwarder.ForwardRequest(http))
				return;
			var stream = match.BoundVariables["stream"];
			var envelope = new SendToHttpEnvelope(
				_networkSendQueue, http,
				(args, message) =>
					http.ResponseCodec.To(ToSummaryDto(http,
						message as MonitoringMessage.GetPersistentSubscriptionStatsCompleted).ToArray()),
				(args, message) => StatsConfiguration(http, message));
			var cmd = new MonitoringMessage.GetStreamPersistentSubscriptionStats(envelope, stream);
			Publish(cmd);
		}

		private void GetSubscriptionInfo(HttpEntityManager http, UriTemplateMatch match) {
			if (_httpForwarder.ForwardRequest(http))
				return;
			var stream = match.BoundVariables["stream"];
			var groupName = match.BoundVariables["subscription"];
			var envelope = new SendToHttpEnvelope(
				_networkSendQueue, http,
				(args, message) =>
					http.ResponseCodec.To(
						ToDto(http, message as MonitoringMessage.GetPersistentSubscriptionStatsCompleted)
							.FirstOrDefault()),
				(args, message) => StatsConfiguration(http, message));
			var cmd = new MonitoringMessage.GetPersistentSubscriptionStats(envelope, stream, groupName);
			Publish(cmd);
		}

		private static ResponseConfiguration StatsConfiguration(HttpEntityManager http, Message message) {
			int code;
			var m = message as MonitoringMessage.GetPersistentSubscriptionStatsCompleted;
			if (m == null) throw new Exception("unexpected message " + message);
			switch (m.Result) {
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

		private void GetNextNMessages(HttpEntityManager http, UriTemplateMatch match) {
			if (_httpForwarder.ForwardRequest(http))
				return;
			var groupname = match.BoundVariables["subscription"];
			var stream = match.BoundVariables["stream"];
			var cnt = match.BoundVariables["count"];
			var embed = GetEmbedLevel(http, match);
			int count = DefaultNumberOfMessagesToGet;
			if (!cnt.IsEmptyString() && (!int.TryParse(cnt, out count) || count > 100 || count < 1)) {
				SendBadRequest(http,
					string.Format("Message count must be an integer between 1 and 100 'count' ='{0}'", count));
				return;
			}

			var envelope = new SendToHttpEnvelope(
				_networkSendQueue, http,
				(args, message) => Format.ReadNextNPersistentMessagesCompleted(http,
					message as ClientMessage.ReadNextNPersistentMessagesCompleted, stream, groupname, count, embed),
				(args, message) => {
					int code;
					var m = message as ClientMessage.ReadNextNPersistentMessagesCompleted;
					if (m == null) throw new Exception("unexpected message " + message);
					switch (m.Result) {
						case
							ClientMessage.ReadNextNPersistentMessagesCompleted.ReadNextNPersistentMessagesResult
								.Success:
							code = HttpStatusCode.OK;
							break;
						case
							ClientMessage.ReadNextNPersistentMessagesCompleted.ReadNextNPersistentMessagesResult
								.DoesNotExist:
							code = HttpStatusCode.NotFound;
							break;
						case
							ClientMessage.ReadNextNPersistentMessagesCompleted.ReadNextNPersistentMessagesResult
								.AccessDenied:
							code = HttpStatusCode.Unauthorized;
							break;
						default:
							code = HttpStatusCode.InternalServerError;
							break;
					}

					return new ResponseConfiguration(code, http.ResponseCodec.ContentType,
						http.ResponseCodec.Encoding);
				});

			var cmd = new ClientMessage.ReadNextNPersistentMessages(
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
			switch (rawValue.ToLowerInvariant()) {
				case "content": return EmbedLevel.Content;
				case "rich": return EmbedLevel.Rich;
				case "body": return EmbedLevel.Body;
				case "pretty": return EmbedLevel.PrettyBody;
				case "tryharder": return EmbedLevel.TryHarder;
				default: return EmbedLevel.None;
			}
		}

		string parkedMessageUriTemplate =
			"/streams/" + Uri.EscapeDataString("$persistentsubscription") + "-{0}::{1}-parked";

		private IEnumerable<SubscriptionInfo> ToDto(HttpEntityManager manager,
			MonitoringMessage.GetPersistentSubscriptionStatsCompleted message) {
			if (message == null) yield break;
			if (message.SubscriptionStats == null) yield break;

			foreach (var stat in message.SubscriptionStats) {
				string escapedStreamId = Uri.EscapeDataString(stat.EventStreamId);
				string escapedGroupName = Uri.EscapeDataString(stat.GroupName);
				var info = new SubscriptionInfo {
					Links = new List<RelLink>() {
						new RelLink(
							MakeUrl(manager,
								string.Format("/subscriptions/{0}/{1}/info", escapedStreamId, escapedGroupName)),
							"detail"),
						new RelLink(
							MakeUrl(manager,
								string.Format("/subscriptions/{0}/{1}/replayParked", escapedStreamId,
									escapedGroupName)), "replayParked")
					},
					EventStreamId = stat.EventStreamId,
					GroupName = stat.GroupName,
					Status = stat.Status,
					AverageItemsPerSecond = stat.AveragePerSecond,
					TotalItemsProcessed = stat.TotalItems,
					CountSinceLastMeasurement = stat.CountSinceLastMeasurement,
					LastKnownEventNumber = stat.LastKnownMessage,
					LastProcessedEventNumber = stat.LastProcessedEventNumber,
					ReadBufferCount = stat.ReadBufferCount,
					LiveBufferCount = stat.LiveBufferCount,
					RetryBufferCount = stat.RetryBufferCount,
					TotalInFlightMessages = stat.TotalInFlightMessages,
					ParkedMessageUri = MakeUrl(manager,
						string.Format(parkedMessageUriTemplate, escapedStreamId, escapedGroupName)),
					GetMessagesUri = MakeUrl(manager,
						string.Format("/subscriptions/{0}/{1}/{2}", escapedStreamId, escapedGroupName,
							DefaultNumberOfMessagesToGet)),
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
						StartFrom = stat.StartFrom,
						ExtraStatistics = stat.ExtraStatistics,
						MaxSubscriberCount = stat.MaxSubscriberCount
					},
					Connections = new List<ConnectionInfo>()
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
							ExtraStatistics = connection.ObservedMeasurements ?? new List<Measurement>()
						});
					}
				}

				yield return info;
			}
		}

		private IEnumerable<SubscriptionSummary> ToSummaryDto(HttpEntityManager manager,
			MonitoringMessage.GetPersistentSubscriptionStatsCompleted message) {
			if (message == null) yield break;
			if (message.SubscriptionStats == null) yield break;

			foreach (var stat in message.SubscriptionStats) {
				string escapedStreamId = Uri.EscapeDataString(stat.EventStreamId);
				string escapedGroupName = Uri.EscapeDataString(stat.GroupName);
				var info = new SubscriptionSummary {
					Links = new List<RelLink>() {
						new RelLink(
							MakeUrl(manager,
								string.Format("/subscriptions/{0}/{1}/info", escapedStreamId, escapedGroupName)),
							"detail"),
					},
					EventStreamId = stat.EventStreamId,
					GroupName = stat.GroupName,
					Status = stat.Status,
					AverageItemsPerSecond = stat.AveragePerSecond,
					TotalItemsProcessed = stat.TotalItems,
					LastKnownEventNumber = stat.LastKnownMessage,
					LastProcessedEventNumber = stat.LastProcessedEventNumber,
					ParkedMessageUri = MakeUrl(manager,
						string.Format(parkedMessageUriTemplate, escapedStreamId, escapedGroupName)),
					GetMessagesUri = MakeUrl(manager,
						string.Format("/subscriptions/{0}/{1}/{2}", escapedStreamId, escapedGroupName,
							DefaultNumberOfMessagesToGet)),
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
			public long StartFrom { get; set; }
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
				StartFrom = 0;
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
			public long LastProcessedEventNumber { get; set; }
			public long LastKnownEventNumber { get; set; }
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
			public long LastProcessedEventNumber { get; set; }
			public long LastKnownEventNumber { get; set; }
			public int ReadBufferCount { get; set; }
			public long LiveBufferCount { get; set; }
			public int RetryBufferCount { get; set; }
			public int TotalInFlightMessages { get; set; }
			public List<ConnectionInfo> Connections { get; set; }
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
		}
	}
}
