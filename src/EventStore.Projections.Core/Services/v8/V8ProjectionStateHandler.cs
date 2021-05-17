using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using EventStore.Common.Utils;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.v8;
using Newtonsoft.Json.Linq;
using Serilog;

namespace EventStore.Projections.Core.Services.v8 {
	using System.Threading;
	using Client.PersistentSubscriptions;

	public class V8ProjectionStateHandler : IProjectionStateHandler {
		private readonly PreludeScript _prelude;
		private readonly QueryScript _query;
		private List<EmittedEventEnvelope> _emittedEvents;
		private CheckpointTag _eventPosition;
		private bool _disposed;
		private static readonly char[] LinkToSeparator = { '@' };
		private static readonly string LinkType = "$>";
		private bool _enableContentTypeValidation;

		public V8ProjectionStateHandler(
			string preludeName, string querySource, Func<string, Tuple<string, string>> getModuleSource,
			Action<string, object[]> logger, Action<int, Action> cancelCallbackFactory, bool enableContentTypeValidation) {
			var preludeSource = getModuleSource(preludeName);
			var prelude = new PreludeScript(preludeSource.Item1, preludeSource.Item2, getModuleSource,
				cancelCallbackFactory, logger);
			QueryScript query;
			try {
				query = new QueryScript(prelude, querySource, "POST-BODY");
				query.Emit += QueryOnEmit;
			} catch {
				prelude.Dispose(); // clean up unmanaged resources if failed to create
				throw;
			}

			_prelude = prelude;
			_query = query;
			_enableContentTypeValidation = enableContentTypeValidation;
		}

		[DataContract]
		public class EmittedEventJsonContract {
			[DataMember] public string streamId;

			[DataMember] public string eventName;

			[DataMember] public bool isJson;

			[DataMember] public string body;

			[DataMember] public Dictionary<string, JRaw> metadata;

			public ExtraMetaData GetExtraMetadata() {
				if (metadata == null)
					return null;
				return new ExtraMetaData(metadata);
			}
		}

		private string GetEventData(ResolvedEvent evnt) {
			if (_enableContentTypeValidation) {
				return (evnt.IsJson ? evnt.Data : evnt.Data ?? string.Empty)?.Trim();
			}
			return evnt.Data?.Trim();
		}

		private void QueryOnEmit(string json) {
			EmittedEventJsonContract emittedEvent;
			try {
				emittedEvent = json.ParseJson<EmittedEventJsonContract>();
			} catch (Exception ex) {
				throw new ArgumentException("Failed to deserialize emitted event JSON", ex);
			}

			if (!IsValidEvent(emittedEvent)) {
				Log.Warning($"Invalid emitted event was ignored: streamId: [{emittedEvent.streamId}], eventType: [{emittedEvent.eventName}], payload: [{emittedEvent.body}]");
				return;
			}
			
			if (emittedEvent.eventName.Equals(LinkType) && !IsValidLinkEvent(emittedEvent)) {
				Log.Warning($"Invalid emitted link event was ignored: streamId: [{emittedEvent.streamId}], eventType: [{emittedEvent.eventName}], payload: [{emittedEvent.body}]");
				return;
			}
			
			if (_emittedEvents == null)
				_emittedEvents = new List<EmittedEventEnvelope>();
			_emittedEvents.Add(
				new EmittedEventEnvelope(
					new EmittedDataEvent(
						emittedEvent.streamId, Guid.NewGuid(), emittedEvent.eventName, emittedEvent.isJson,
						emittedEvent.body,
						emittedEvent.GetExtraMetadata(), _eventPosition, expectedTag: null)));
		}

		private QuerySourcesDefinition GetQuerySourcesDefinition() {
			CheckDisposed();
			var sourcesDefinition = _query.GetSourcesDefintion();
			if (sourcesDefinition == null)
				throw new InvalidOperationException("Invalid query.  No source definition.");
			return sourcesDefinition;
		}

		public void Load(string state) {
			CheckDisposed();
			_query.SetState(state);
		}

		public void LoadShared(string state) {
			CheckDisposed();
			_query.SetSharedState(state);
		}

		public void Initialize() {
			CheckDisposed();
			_query.Initialize();
		}

		public void InitializeShared() {
			CheckDisposed();
			_query.InitializeShared();
		}

		public string GetStatePartition(
			CheckpointTag eventPosition, string category, ResolvedEvent @event) {
			CheckDisposed();
			if (@event == null) throw new ArgumentNullException("event");

			if (string.IsNullOrEmpty(@event.EventType)) {
				//Nothing to actually process
				return null;
			}

			//Only get the event data if our previous checks passed.
			string eventData = GetEventData(@event);

			var partition = _query.GetPartition(eventData,
			                                    new string[] {
				                                                 @event.EventStreamId, @event.IsJson ? "1" : "", @event.EventType, category ?? "",
				                                                 @event.EventSequenceNumber.ToString(CultureInfo.InvariantCulture), @event.Metadata ?? "",
				                                                 @event.PositionMetadata ?? ""
			                                                 });
			if (partition == "")
				return null;
			
			return partition;
		}

		public bool ProcessEvent(string partition, CheckpointTag eventPosition, string category, ResolvedEvent @event, out string newState, out string newSharedState, out EmittedEventEnvelope[] emittedEvents) {
			CheckDisposed();
			_eventPosition = eventPosition;
			_emittedEvents = null;
			Tuple<string, string> newStates = null;

			var data = GetEventData(@event);

			if (@event == null || data == null || string.IsNullOrEmpty(data)) {
				newStates = _query.Push(
					"",
					new string[] { });
			} else {
				newStates = _query.Push(
					data,
					new[] {
						@event.IsJson ? "1" : "", 
						@event.EventStreamId, 
						@event.EventType, 
						category ?? "",
						@event.EventSequenceNumber.ToString(CultureInfo.InvariantCulture), 
						@event.Metadata ?? "",
						@event.PositionMetadata ?? "", 
						partition,
						@event.EventId.ToString()
					});
			}

			newState = newStates.Item1;
			newSharedState = newStates.Item2;
			/*            try
			            {
			                if (!string.IsNullOrEmpty(newState))
			                {
			                    var jo = newState.ParseJson<JObject>();
			                }
			
			            }
			            catch (InvalidCastException)
			            {
			                Console.Error.WriteLine(newState);
			            }
			            catch (JsonException)
			            {
			                Console.Error.WriteLine(newState);
			            }*/
			emittedEvents = _emittedEvents == null ? null : _emittedEvents.ToArray();
			return true;
		}

		public bool ProcessPartitionCreated(string partition, CheckpointTag createPosition, ResolvedEvent @event,
		                                    out EmittedEventEnvelope[] emittedEvents) {
			CheckDisposed();
			_eventPosition = createPosition;
			_emittedEvents = null;

			var data = GetEventData(@event);

			if (@event == null || data == null || string.IsNullOrEmpty(data)) { 
				emittedEvents = null;
				return true;
			}

			_query.NotifyCreated(
				data,
				new[] {
					@event.IsJson ? "1" : "", @event.EventStreamId, @event.EventType, "",
					@event.EventSequenceNumber.ToString(CultureInfo.InvariantCulture), @event.Metadata ?? "",
					@event.PositionMetadata ?? "", partition, ""
				});
			emittedEvents = _emittedEvents == null ? null : _emittedEvents.ToArray();
			return true;
		}

		public bool ProcessPartitionDeleted(string partition, CheckpointTag deletePosition, out string newState) {
			CheckDisposed();
			_eventPosition = deletePosition;
			_emittedEvents = null;
			var newStates = _query.NotifyDeleted(
				"", // trimming data passed to a JS 
				new[] {
					partition, "" /* isSoftDedleted */
				});
			newState = newStates;
			return true;
		}

		public string TransformStateToResult() {
			CheckDisposed();
			var result = _query.TransformStateToResult();
			return result;
		}

		private void CheckDisposed() {
			if (_disposed)
				throw new InvalidOperationException("Disposed");
		}

		public void Dispose() {
			_disposed = true;
			if (_query != null)
				_query.Dispose();
			if (_prelude != null)
				_prelude.Dispose();
		}

		public IQuerySources GetSourceDefinition() {
			return GetQuerySourcesDefinition();
		}
		
		private static bool IsValidEvent(EmittedEventJsonContract @event) {
			return !(@event.eventName.IsEmptyString() || @event.streamId.IsEmptyString() || @event.isJson && @event.body.IsEmptyString());
		}
		
		// This function assumes 'IsValidEvent' was called upfront.
		private static bool IsValidLinkEvent(EmittedEventJsonContract @event) {
			var parts = @event.body.Split(LinkToSeparator, 2);
			return long.TryParse(parts[0], out long _);
		}
	}
}
