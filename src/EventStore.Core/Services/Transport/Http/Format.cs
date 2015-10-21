using System;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Transport.Http;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;

namespace EventStore.Core.Services.Transport.Http
{
    public static class Format
    {
        public static string TextMessage(HttpResponseFormatterArgs entity, Message message)
        {
            var textMessage = message as HttpMessage.TextMessage;
            return textMessage != null ? entity.ResponseCodec.To(textMessage) : String.Empty;
        }

        public static string EventEntry(HttpResponseFormatterArgs entity, Message message, EmbedLevel embed)
        {
            var msg = message as ClientMessage.ReadEventCompleted;
            if (msg == null || msg.Result != ReadEventResult.Success || msg.Record.Event == null)
                return entity.ResponseCodec.To(new {});

            switch (entity.ResponseCodec.ContentType)
            {
                case ContentType.Atom:
                case ContentType.AtomJson:
                case ContentType.Html:
                    return entity.ResponseCodec.To(Convert.ToEntry(msg.Record, entity.RequestedUrl, embed, singleEntry: true));
                default:
                    return AutoEventConverter.SmartFormat(msg.Record, entity.ResponseCodec);
            }
        }

        public static string GetStreamEventsBackward(HttpResponseFormatterArgs entity, Message message, EmbedLevel embed, bool headOfStream)
        {
            var msg = message as ClientMessage.ReadStreamEventsBackwardCompleted;
            if (msg == null || msg.Result != ReadStreamResult.Success)
                return String.Empty;

            return entity.ResponseCodec.To(Convert.ToStreamEventBackwardFeed(msg, entity.RequestedUrl, embed, headOfStream));
        }

        public static string GetStreamEventsForward(HttpResponseFormatterArgs entity, Message message, EmbedLevel embed)
        {
            var msg = message as ClientMessage.ReadStreamEventsForwardCompleted;
            if (msg == null || msg.Result != ReadStreamResult.Success)
                return String.Empty;
                
            return entity.ResponseCodec.To(Convert.ToStreamEventForwardFeed(msg, entity.RequestedUrl, embed));
        }

        public static string ReadAllEventsBackwardCompleted(HttpResponseFormatterArgs entity, Message message, EmbedLevel embed)
        {
            var msg = message as ClientMessage.ReadAllEventsBackwardCompleted;
            if (msg == null || msg.Result != ReadAllResult.Success)
                return String.Empty;

            return entity.ResponseCodec.To(Convert.ToAllEventsBackwardFeed(msg, entity.RequestedUrl, embed));
        }

        public static string ReadAllEventsForwardCompleted(HttpResponseFormatterArgs entity, Message message, EmbedLevel embed)
        {
            var msg = message as ClientMessage.ReadAllEventsForwardCompleted;
            if (msg == null || msg.Result != ReadAllResult.Success)
                return String.Empty;

            return entity.ResponseCodec.To(Convert.ToAllEventsForwardFeed(msg, entity.RequestedUrl, embed)); 
        }

        public static string WriteEventsCompleted(HttpResponseFormatterArgs entity, Message message)
        {
            return String.Empty;
        }

        public static string DeleteStreamCompleted(HttpResponseFormatterArgs entity, Message message)
        {
            return String.Empty;
        }

        public static string GetFreshStatsCompleted(HttpResponseFormatterArgs entity, Message message)
        {
            var completed = message as MonitoringMessage.GetFreshStatsCompleted;
            if (completed == null || !completed.Success)
                return String.Empty;

            return entity.ResponseCodec.To(completed.Stats);
        }

		public static string SendGossip(HttpResponseFormatterArgs entity, Message message)
		{
			if (message.GetType() != typeof(GossipMessage.SendGossip))
				throw new Exception(string.Format("Unexpected type of response message: {0}, expected: {1}",
												  message.GetType().Name,
												  typeof(GossipMessage.SendGossip).Name));

			var sendGossip = message as GossipMessage.SendGossip;
			return sendGossip != null
					   ? entity.ResponseCodec.To(new ClusterInfoDto(sendGossip.ClusterInfo, sendGossip.ServerEndPoint))
					   : string.Empty;
		}

        public static string ReadNextNPersistentMessagesCompleted(HttpResponseFormatterArgs entity, Message message, string streamId, string groupName, int count, EmbedLevel embed)
        {
            var msg = message as ClientMessage.ReadNextNPersistentMessagesCompleted;
            if (msg == null || msg.Result != ClientMessage.ReadNextNPersistentMessagesCompleted.ReadNextNPersistentMessagesResult.Success)
                return String.Empty;

            return entity.ResponseCodec.To(Convert.ToNextNPersistentMessagesFeed(msg, entity.RequestedUrl, streamId, groupName, count, embed));
        }
    }
}
