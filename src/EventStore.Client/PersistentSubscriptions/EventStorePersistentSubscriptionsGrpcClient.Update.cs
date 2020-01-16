using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Client.PersistentSubscriptions {
	public partial class EventStorePersistentSubscriptionsClient {
		private static readonly IDictionary<string, UpdateReq.Types.ConsumerStrategy> NamedConsumerStrategyToUpdateProto
			= new Dictionary<string, UpdateReq.Types.ConsumerStrategy> {
				[SystemConsumerStrategies.DispatchToSingle] = UpdateReq.Types.ConsumerStrategy.DispatchToSingle,
				[SystemConsumerStrategies.RoundRobin] = UpdateReq.Types.ConsumerStrategy.RoundRobin,
				[SystemConsumerStrategies.Pinned] = UpdateReq.Types.ConsumerStrategy.Pinned,
			};

		public async Task UpdateAsync(string streamName, string groupName, PersistentSubscriptionSettings settings,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			if (streamName == null) {
				throw new ArgumentNullException(nameof(streamName));
			}

			if (groupName == null) {
				throw new ArgumentNullException(nameof(groupName));
			}

			if (settings == null) {
				throw new ArgumentNullException(nameof(settings));
			}

			await _client.UpdateAsync(new UpdateReq {
				Options = new UpdateReq.Types.Options {
					StreamName = streamName,
					GroupName = groupName,
					Settings = new UpdateReq.Types.Settings {
						Revision = settings.StartFrom,
						CheckpointAfter = settings.CheckPointAfter.Ticks,
						ExtraStatistics = settings.ExtraStatistics,
						MessageTimeout = settings.MessageTimeout.Ticks,
						ResolveLinks = settings.ResolveLinkTos,
						HistoryBufferSize = settings.HistoryBufferSize,
						LiveBufferSize = settings.LiveBufferSize,
						MaxCheckpointCount = settings.MaxCheckPointCount,
						MaxRetryCount = settings.MaxRetryCount,
						MaxSubscriberCount = settings.MaxSubscriberCount,
						MinCheckpointCount = settings.MinCheckPointCount,
						NamedConsumerStrategy = NamedConsumerStrategyToUpdateProto[settings.NamedConsumerStrategy],
						ReadBatchSize = settings.ReadBatchSize
					}
				}
			}, RequestMetadata.Create(userCredentials), cancellationToken: cancellationToken);
		}
	}
}
