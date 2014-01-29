using EventStore.Core.Data;
using EventStore.Core.Services;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Standard
{
    public class StreamDeletedHelper
    {
        public static bool IsStreamDeletedEvent(ResolvedEvent data, out string positionStreamId)
        {
            bool isMetaStream;
            if (SystemStreams.IsMetastream(data.PositionStreamId))
            {
                isMetaStream = true;
                positionStreamId = data.PositionStreamId.Substring("$$".Length);
            }
            else
            {
                isMetaStream = false;
                positionStreamId = data.PositionStreamId;
            }
            var isStreamDeletedEvent = false;
            if (isMetaStream)
            {
                if (data.EventType == SystemEventTypes.StreamMetadata)
                {
                    var metadata = StreamMetadata.FromJson(data.Data);
                    //NOTE: we do not ignore JSON deserialization exceptions here assuming that metadata stream events must be deserializable

                    if (metadata.TruncateBefore == EventNumber.DeletedStream)
                        isStreamDeletedEvent = true;
                }
            }
            else
            {
                if (data.EventType == SystemEventTypes.StreamDeleted)
                    isStreamDeletedEvent = true;
            }
            return isStreamDeletedEvent;
        }
    }
}