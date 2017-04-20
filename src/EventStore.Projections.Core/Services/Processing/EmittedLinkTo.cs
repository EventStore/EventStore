using System;
using System.Globalization;
using System.Text;

namespace EventStore.Projections.Core.Services.Processing
{
    public class EmittedLinkTo : EmittedEvent
    {
        private readonly string _targetStreamId;
        private long? _eventNumber;

        public EmittedLinkTo(
            string streamId, Guid eventId,
            string targetStreamId, CheckpointTag causedByTag, CheckpointTag expectedTag, Action<long> onCommitted = null)
            : base(streamId, eventId, "$>", causedByTag, expectedTag, onCommitted)
        {
            _targetStreamId = targetStreamId;
        }

        public EmittedLinkTo(
            string streamId, Guid eventId,
            string targetStreamId, int targetEventNumber, CheckpointTag causedByTag, CheckpointTag expectedTag,
            string originalStreamId = null)
            : base(streamId, eventId, "$>", causedByTag, expectedTag, null)
        {
            _eventNumber = targetEventNumber;
            _targetStreamId = targetStreamId;
        }

        public override string Data
        {
            get
            {
                if (!IsReady())
                    throw new InvalidOperationException("Link target has not been yet committed");
                return
                    _eventNumber.Value.ToString(CultureInfo.InvariantCulture) + "@" + _targetStreamId;
            }
        }

        public override bool IsJson
        {
            get { return false; }
        }

        public override bool IsReady()
        {
            return _eventNumber != null;
        }

        public void SetTargetEventNumber(long eventNumber)
        {
            if (_eventNumber != null)
                throw new InvalidOperationException("Target event number has been already set");
            _eventNumber = eventNumber;
        }

    }
}
