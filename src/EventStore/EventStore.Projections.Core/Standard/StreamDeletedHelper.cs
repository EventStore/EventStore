using EventStore.Core.Data;
using EventStore.Core.Services;
using EventStore.Projections.Core.Utils;

namespace EventStore.Projections.Core.Standard
{
    public static class StreamDeletedHelper
    {
        public static bool IsStreamDeletedEvent(
            string streamOrMetaStreamId, string eventType, string eventData, out string streamId)
        {
            bool isMetaStream;
            if (SystemStreams.IsMetastream(streamOrMetaStreamId))
            {
                isMetaStream = true;
                streamId = streamOrMetaStreamId.Substring("$$".Length);
            }
            else
            {
                isMetaStream = false;
                streamId = streamOrMetaStreamId;
            }
            var isStreamDeletedEvent = false;
            if (isMetaStream)
            {
                if (eventType == SystemEventTypes.StreamMetadata)
                {
                    var metadata = StreamMetadata.FromJson(eventData);
                    //NOTE: we do not ignore JSON deserialization exceptions here assuming that metadata stream events must be deserializable

                    if (metadata.TruncateBefore == EventNumber.DeletedStream)
                        isStreamDeletedEvent = true;
                }
            }
            else
            {
                if (eventType == SystemEventTypes.StreamDeleted)
                    isStreamDeletedEvent = true;
            }
            return isStreamDeletedEvent;
        }

        public static bool IsStreamDeletedEvent(
            string streamOrMetaStreamId, string eventType, byte[] eventData, out string streamId)
        {
            bool isMetaStream;
            if (SystemStreams.IsMetastream(streamOrMetaStreamId))
            {
                isMetaStream = true;
                streamId = streamOrMetaStreamId.Substring("$$".Length);
            }
            else
            {
                isMetaStream = false;
                streamId = streamOrMetaStreamId;
            }
            var isStreamDeletedEvent = false;
            if (isMetaStream)
            {
                if (eventType == SystemEventTypes.StreamMetadata)
                {
                    var metadata = StreamMetadata.FromJson(eventData.FromUtf8());
                    //NOTE: we do not ignore JSON deserialization exceptions here assuming that metadata stream events must be deserializable

                    if (metadata.TruncateBefore == EventNumber.DeletedStream)
                        isStreamDeletedEvent = true;
                }
            }
            else
            {
                if (eventType == SystemEventTypes.StreamDeleted)
                    isStreamDeletedEvent = true;
            }
            return isStreamDeletedEvent;
        }
    }
}