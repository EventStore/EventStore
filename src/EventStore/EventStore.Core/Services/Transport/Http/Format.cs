﻿// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
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
            if (msg == null || msg.Result != ReadEventResult.Success)
                return string.Empty;

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
    }
}
