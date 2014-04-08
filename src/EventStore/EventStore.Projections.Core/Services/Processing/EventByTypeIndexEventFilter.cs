using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Services;

namespace EventStore.Projections.Core.Services.Processing
{
    public class EventByTypeIndexEventFilter : EventFilter
    {
        //NOTE: this filter will pass both events and links to these events from index streams resulting
        //      in resolved events re-appearing in the event stream.  This must be filtered out by a 
        //      reader subscription
        private readonly HashSet<string> _events;
        private readonly HashSet<string> _streams;

        public EventByTypeIndexEventFilter(HashSet<string> events)
            : base(false, false, events)
        {
            _events = events;
            _streams = new HashSet<string>(from eventType in events select "$et-" + eventType);
        }

        protected override bool DeletedNotificationPasses(string positionStreamId)
        {
            return true;
        }

        public override bool PassesSource(bool resolvedFromLinkTo, string positionStreamId, string eventType)
        {
            //TODO: add tests to assure that resolved by link events are not passed twice into the subscription?!!
            return !(resolvedFromLinkTo && !SystemStreams.IsSystemStream(positionStreamId))
                   || _streams.Contains(positionStreamId);
        }

        public override string GetCategory(string positionStreamId)
        {
            return null;
        }
    }
}
