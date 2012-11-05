//using System;
//using ProtoBuf;

//namespace EventStore.ClientAPI.Messages
//{
//    [ProtoContract]
//    internal class ClientEvent
//    {
//        [ProtoMember(1)]
//        public byte[] EventId { get; set; }

//        [ProtoMember(2, IsRequired = false)]
//        public string EventType { get; set; }

//        [ProtoMember(3)]
//        public byte[] Data { get; set; }

//        [ProtoMember(4, IsRequired = false)]
//        public byte[] Metadata { get; set; }

//        public ClientEvent()
//        {
//        }

//        public ClientEvent(Guid eventId, string eventType, byte[] data, byte[] metadata)
//        {
//            EventId = eventId.ToByteArray();
//            EventType = eventType;
//            Data = data;
//            Metadata = metadata;
//        }
//    }
//}
