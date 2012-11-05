using System;
using System.Collections.Generic;

namespace EventStore.Projections.Core.Services.Processing
{
    public class IndexedEventTypeEventFilter : EventFilter
    {
        private readonly string _stream;

        public IndexedEventTypeEventFilter(string eventType)
            : base(false, new HashSet<string> { eventType})
        {
            _stream = "$et-" + eventType;
        }

        public override bool PassesSource(bool resolvedFromLinkTo, string positionStreamId)
        {
            return resolvedFromLinkTo && _stream == positionStreamId;
        }

        public override string GetCategory(string positionStreamId)
        {
            return null;
        }

        public override string ToString()
        {
            return string.Format("Stream: {0}", _stream);
        }
    }
}