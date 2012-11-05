using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EventStore.Projections.Core.Services.Processing
{
    public class IndexedEventTypesEventFilter : EventFilter
    {
        private readonly HashSet<string> _streams;

        public IndexedEventTypesEventFilter(string[] eventTypes)
            : base(false, new HashSet<string>(eventTypes))
        {
            _streams = new HashSet<string>(eventTypes.Select(v => "$et-" + v));
        }

        public override bool PassesSource(bool resolvedFromLinkTo, string positionStreamId)
        {
            return resolvedFromLinkTo && _streams.Contains(positionStreamId);
        }

        public override string GetCategory(string positionStreamId)
        {
            return null;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var stream in _streams)
            {
                sb.Append(stream);
                sb.Append("; ");
            }
            return string.Format("Streams: {0}", sb);
        }
    }
}