using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EventStore.Client.Monitoring;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using Google.Protobuf.WellKnownTypes;
using EventStore.Common.Utils;
using Google.Protobuf.Collections;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	internal partial class Monitoring : EventStore.Client.Monitoring.Monitoring.MonitoringBase {
		private readonly IPublisher _publisher;
		
		public override async Task Stats(StatsReq request, IServerStreamWriter<StatsResp> responseStream, ServerCallContext context) {
			await using var enumerator = CollectStats(request);
			await using (context.CancellationToken.Register(() => enumerator.DisposeAsync())) {
				while (await enumerator.MoveNextAsync().ConfigureAwait(false)) {
					await responseStream.WriteAsync(enumerator.Current).ConfigureAwait(false);
				}
			}
		}

		public Monitoring(IPublisher publisher) {
			_publisher = publisher;
		}

		private async IAsyncEnumerator<StatsResp> CollectStats(StatsReq request) {
			for (;;) {
				var source = new TaskCompletionSource<StatsResp>();
				var envelope = new CallbackEnvelope(message => {
					if (message is not MonitoringMessage.GetFreshStatsCompleted completed) {
						source.TrySetException(UnknownMessage<MonitoringMessage.GetFreshStatsCompleted>(message));
					} else {
						var resp = new StatsResp();

						foreach (var tuple in completed.Stats) {
							if (tuple.Value == null)
								continue;
							
							resp.Stats.Add(tuple.Key, tuple.Value.ToString());
						}

						source.TrySetResult(resp);
					}
				});
				_publisher.Publish(new MonitoringMessage.GetFreshStats(envelope, x => x, request.UseMetadata, false));
				var resp = await source.Task.ConfigureAwait(false);
				
				yield return resp;

				await Task.Delay(TimeSpan.FromMilliseconds(request.RefreshTimePeriodInMs)).ConfigureAwait(false);
			}
		}
		
		private static Exception UnknownMessage<T>(Message message) where T : Message =>
			new RpcException(
				new Status(StatusCode.Unknown,
					$"Envelope callback expected {typeof(T).Name}, received {message.GetType().Name} instead"));
	}
}
