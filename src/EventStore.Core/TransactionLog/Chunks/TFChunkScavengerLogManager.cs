using System;
using System.Collections.Generic;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services;
using EventStore.Core.Services.Storage;
using EventStore.Core.Services.UserManagement;

namespace EventStore.Core.TransactionLog.Chunks {
	public class TFChunkScavengerLogManager : ITFChunkScavengerLogManager {
		private readonly string _nodeEndpoint;
		private readonly TimeSpan _scavengeHistoryMaxAge;
		private readonly IODispatcher _ioDispatcher;
		private const int MaxRetryCount = 5;
		private static readonly ILogger Log = LogManager.GetLoggerFor<StorageScavenger>();
		private int _isInitialised;

		public TFChunkScavengerLogManager(string nodeEndpoint, TimeSpan scavengeHistoryMaxAge,
			IODispatcher ioDispatcher) {
			_nodeEndpoint = nodeEndpoint;
			_scavengeHistoryMaxAge = scavengeHistoryMaxAge;
			_ioDispatcher = ioDispatcher;
		}

		public void Initialise() {
			// We only initialise on first election so we don't incorrectly mark running scavenges as interrupted.
			if (Interlocked.Exchange(ref _isInitialised, 1) != 0)
				return;

			SetMaxAge();

			Log.Debug("Searching for incomplete scavenges on node {nodeEndPoint}.", _nodeEndpoint);
			GatherIncompleteScavenges(-1, new HashSet<string>(), new List<string>());
		}

		public ITFChunkScavengerLog CreateLog() {
			return CreateLogInternal(Guid.NewGuid().ToString());
		}

		private TFChunkScavengerLog CreateLogInternal(string scavengeId) {
			return new TFChunkScavengerLog(_ioDispatcher, scavengeId, _nodeEndpoint, MaxRetryCount,
				_scavengeHistoryMaxAge);
		}

		private void SetMaxAge() {
			var metaStreamId = SystemStreams.MetastreamOf(SystemStreams.ScavengesStream);

			_ioDispatcher.ReadBackward(metaStreamId, -1, 1, false, SystemAccount.Principal, readResult => {
				if (readResult.Result == ReadStreamResult.Success || readResult.Result == ReadStreamResult.NoStream) {
					if (readResult.Events.Length == 1) {
						var currentMetadata = StreamMetadata.FromJsonBytes(readResult.Events[0].Event.Data);

						if (currentMetadata.MaxAge == _scavengeHistoryMaxAge) {
							Log.Debug("Max age already set for the {stream} stream.", SystemStreams.ScavengesStream);
							return;
						}
					}

					Log.Debug("Setting max age for the {stream} stream to {maxAge}.", SystemStreams.ScavengesStream,
						_scavengeHistoryMaxAge);

					var metadata = new StreamMetadata(maxAge: _scavengeHistoryMaxAge);
					var metaStreamEvent = new Event(Guid.NewGuid(), SystemEventTypes.StreamMetadata, isJson: true,
						data: metadata.ToJsonBytes(), metadata: null);
					_ioDispatcher.WriteEvent(metaStreamId, ExpectedVersion.Any, metaStreamEvent,
						SystemAccount.Principal, m => {
							if (m.Result != OperationResult.Success) {
								Log.Error(
									"Failed to write the $maxAge of {days} days metadata for the {stream} stream. Reason: {reason}",
									_scavengeHistoryMaxAge.TotalDays, SystemStreams.ScavengesStream, m.Result);
							}
						});
				}
			});
		}

		private void GatherIncompleteScavenges(long from, ISet<string> completedScavenges,
			IList<string> incompleteScavenges) {
			_ioDispatcher.ReadBackward(SystemStreams.ScavengesStream, from, 20, true, SystemAccount.Principal,
				readResult => {
					if (readResult.Result != ReadStreamResult.Success &&
					    readResult.Result != ReadStreamResult.NoStream) {
						Log.Debug("Unable to read {stream} for scavenge log clean up. Result: {result}",
							SystemStreams.ScavengesStream, readResult.Result);
						return;
					}

					foreach (var ev in readResult.Events) {
						if (ev.ResolveResult == ReadEventResult.Success) {
							var dictionary = ev.Event.Data.ParseJson<Dictionary<string, object>>();

							object entryNode;
							if (!dictionary.TryGetValue("nodeEndpoint", out entryNode) ||
							    entryNode.ToString() != _nodeEndpoint) {
								continue;
							}

							object scavengeIdEntry;
							if (!dictionary.TryGetValue("scavengeId", out scavengeIdEntry)) {
								Log.Warn("An entry in the scavenge log has no scavengeId");
								continue;
							}

							var scavengeId = scavengeIdEntry.ToString();

							if (ev.Event.EventType == SystemEventTypes.ScavengeCompleted) {
								completedScavenges.Add(scavengeId);
							} else if (ev.Event.EventType == SystemEventTypes.ScavengeStarted) {
								if (!completedScavenges.Contains(scavengeId)) {
									incompleteScavenges.Add(scavengeId);
								}
							}
						}
					}

					if (readResult.IsEndOfStream || readResult.Events.Length == 0) {
						CompleteInterruptedScavenges(incompleteScavenges);
					} else {
						GatherIncompleteScavenges(readResult.NextEventNumber, completedScavenges, incompleteScavenges);
					}
				});
		}

		private void CompleteInterruptedScavenges(IList<string> incompletedScavenges) {
			if (incompletedScavenges.Count == 0) {
				Log.Debug("No incomplete scavenges found on node {nodeEndPoint}.", _nodeEndpoint);
			} else {
				Log.Info(
					"Found {incomplete} incomplete scavenge{s} on node {nodeEndPoint}. Marking as failed:{newLine}{incompleteScavenges}",
					incompletedScavenges.Count, incompletedScavenges.Count == 1 ? "" : "s", _nodeEndpoint,
					Environment.NewLine, string.Join(Environment.NewLine, incompletedScavenges));
			}

			foreach (var incompletedScavenge in incompletedScavenges) {
				var log = CreateLogInternal(incompletedScavenge);

				log.ScavengeCompleted(ScavengeResult.Failed, "The node was restarted.", TimeSpan.Zero);
			}
		}
	}
}
