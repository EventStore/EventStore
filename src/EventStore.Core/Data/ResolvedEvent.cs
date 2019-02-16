namespace EventStore.Core.Data {
	public struct ResolvedEvent {
		public static readonly ResolvedEvent[] EmptyArray = new ResolvedEvent[0];
		public static readonly ResolvedEvent EmptyEvent = new ResolvedEvent();

		public readonly EventRecord Event;
		public readonly EventRecord Link;

		public EventRecord OriginalEvent {
			get { return Link ?? Event; }
		}

		/// <summary>
		/// Position of the OriginalEvent (unresolved link or event) if available
		/// </summary>
		public readonly TFPos? OriginalPosition;

		public readonly ReadEventResult ResolveResult;

		public string OriginalStreamId {
			get { return OriginalEvent.EventStreamId; }
		}

		public long OriginalEventNumber {
			get { return OriginalEvent.EventNumber; }
		}


		private ResolvedEvent(EventRecord @event, EventRecord link, long? commitPosition,
			ReadEventResult resolveResult = default(ReadEventResult)) {
			Event = @event;
			Link = link;
			if (commitPosition.HasValue) {
				OriginalPosition = new TFPos(commitPosition.Value, (link ?? @event).LogPosition);
			} else {
				OriginalPosition = null;
			}

			ResolveResult = resolveResult;
		}

		public static ResolvedEvent ForUnresolvedEvent(EventRecord @event, long? commitPosition = null) {
			return new ResolvedEvent(@event, null, commitPosition);
		}

		public static ResolvedEvent ForResolvedLink(EventRecord @event, EventRecord link, long? commitPosition = null) {
			return new ResolvedEvent(@event, link, commitPosition);
		}

		public static ResolvedEvent ForFailedResolvedLink(EventRecord link, ReadEventResult resolveResult,
			long? commitPosition = null) {
			return new ResolvedEvent(null, link, commitPosition, resolveResult);
		}

		public ResolvedEvent WithoutPosition() {
			return new ResolvedEvent(Event, Link, null, ResolveResult);
		}
	}
}
