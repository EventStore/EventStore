using System;
using System.Text;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.Internal;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Represents a previously written event
    /// </summary>
    public class RecordedEvent
    {
        /// <summary>
        /// The Event Stream that this event belongs to
        /// </summary>
        public readonly string EventStreamId;

        /// <summary>
        /// The Unique Identifier representing this event
        /// </summary>
        public readonly Guid EventId;

        /// <summary>
        /// The number of this event in the stream
        /// </summary>
        public readonly long EventNumber;

        /// <summary>
        /// The type of event this is
        /// </summary>
        public readonly string EventType;

        /// <summary>
        /// A byte array representing the data of this event
        /// </summary>
        public readonly byte[] Data;

        /// <summary>
        /// A byte array representing the metadata associated with this event
        /// </summary>
        public readonly byte[] Metadata;

        /// <summary>
        /// Indicates whether the content is internally marked as json
        /// </summary>
        public readonly bool IsJson;

        /// <summary>
        /// A datetime representing when this event was created in the system
        /// </summary>
        public DateTime Created;

        /// <summary>
        /// A long representing the milliseconds since the epoch when the was created in the system
        /// </summary>
        public long CreatedEpoch;


#if DEBUG
        /// <summary>
        /// Shows the event data interpreted as a UTF8-encoded string.
        /// 
        /// NOTE: This is only available in DEBUG builds of the client API.
        /// </summary>
        public string DebugDataView
        {
            get { return Encoding.UTF8.GetString(Data); }
        }

        /// <summary>
        /// Shows the event metadata interpreted as a UTF8-encoded string.
        /// 
        /// NOTE: This is only available in DEBUG builds of the client API.
        /// </summary>
        public string DebugMetadataView
        {
            get { return Encoding.UTF8.GetString(Metadata); }
        }
#endif

        internal RecordedEvent(ClientMessage.EventRecord systemRecord)
        {
            EventStreamId = systemRecord.EventStreamId;

            EventId = new Guid(systemRecord.EventId);
            EventNumber = systemRecord.EventNumber;

            EventType = systemRecord.EventType;
            if (systemRecord.Created.HasValue)
            {
                Created = DateTime.FromBinary(systemRecord.Created.Value);
            }

            if (systemRecord.CreatedEpoch.HasValue)
            {
                CreatedEpoch = systemRecord.CreatedEpoch.Value;
            }

            Data = systemRecord.Data ?? Empty.ByteArray;
            Metadata = systemRecord.Metadata ?? Empty.ByteArray;
            IsJson = systemRecord.DataContentType == 1;
        }

        
        /// <summary>
        /// Public constructor for RecordedEvent (mainly to improve testability)
        /// </summary>
        /// <param name="eventId"></param>
        /// <param name="eventStreamId"></param>
        /// <param name="eventNumber"></param>
        /// <param name="eventType"></param>
        /// <param name="metadata"></param>
        /// <param name="data"></param>
        /// <param name="isJson"></param>
        /// <param name="created"></param>
        /// <param name="createdEpoch"></param>
        public RecordedEvent(Guid eventId, string eventStreamId, long eventNumber, string eventType, byte[] metadata,
            byte[] data, bool isJson, DateTime created, long createdEpoch)
        {
            EventId = eventId;
            EventStreamId = eventStreamId;
            EventNumber = eventNumber;
            EventType = eventType;
            Metadata = metadata;
            Data = data;
            IsJson = isJson;
            Created = created;
            CreatedEpoch = createdEpoch;
        }
    }
}