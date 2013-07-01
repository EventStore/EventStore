// Copyright (c) 2012, Event Store LLP
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
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Transport.Http.Atom;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStore.Core.Services.Transport.Http
{
    public static class Convert
    {
        private static readonly string AllEscaped = Uri.EscapeDataString("$all");
        public static FeedElement ToStreamEventForwardFeed(ClientMessage.ReadStreamEventsForwardCompleted msg, Uri requestedUrl, EmbedLevel embedContent)
        {
            Ensure.NotNull(msg, "msg");

            string escapedStreamId = Uri.EscapeDataString(msg.EventStreamId);
            var self = HostName.Combine(requestedUrl, "/streams/{0}", escapedStreamId);
            var feed = new FeedElement();
            feed.SetTitle(string.Format("Event stream '{0}'", msg.EventStreamId));
            feed.StreamId = msg.EventStreamId;
            feed.SetId(self);
            feed.SetUpdated(msg.Events.Length > 0 ? msg.Events[0].Event.TimeStamp : DateTime.MinValue.ToUniversalTime());
            feed.SetAuthor(AtomSpecs.Author);

            var prevEventNumber = Math.Min(msg.FromEventNumber + msg.MaxCount - 1, msg.LastEventNumber) + 1;
            var nextEventNumber = msg.FromEventNumber - 1;

            feed.AddLink("self", self);
            feed.AddLink("first", HostName.Combine(requestedUrl, "/streams/{0}/head/backward/{1}", escapedStreamId, msg.MaxCount));
            if (nextEventNumber >= 0)
            {
                feed.AddLink("last", HostName.Combine(requestedUrl, "/streams/{0}/{1}/forward/{2}", escapedStreamId, 0, msg.MaxCount));
                feed.AddLink("next", HostName.Combine(requestedUrl, "/streams/{0}/{1}/backward/{2}", escapedStreamId, nextEventNumber, msg.MaxCount));
            }
            if (!msg.IsEndOfStream || msg.Events.Length > 0)
                feed.AddLink("previous", HostName.Combine(requestedUrl, "/streams/{0}/{1}/forward/{2}", escapedStreamId, prevEventNumber, msg.MaxCount));
            feed.AddLink("metadata", HostName.Combine(requestedUrl, "/streams/{0}/metadata", escapedStreamId));
            for (int i = msg.Events.Length - 1; i >= 0; --i)
            {
                feed.AddEntry(ToEntry(msg.Events[i], requestedUrl, embedContent));
            }

            return feed;
        }

        public static FeedElement ToStreamEventBackwardFeed(ClientMessage.ReadStreamEventsBackwardCompleted msg, Uri requestedUrl, EmbedLevel embedContent, bool headOfStream)
        {
            Ensure.NotNull(msg, "msg");

            string escapedStreamId = Uri.EscapeDataString(msg.EventStreamId);
            var self = HostName.Combine(requestedUrl, "/streams/{0}", escapedStreamId);
            var feed = new FeedElement();
            feed.SetTitle(string.Format("Event stream '{0}'", msg.EventStreamId));
            feed.StreamId = msg.EventStreamId;
            feed.SetId(self);
            feed.SetUpdated(msg.Events.Length > 0 ? msg.Events[0].Event.TimeStamp : DateTime.MinValue.ToUniversalTime());
            feed.SetAuthor(AtomSpecs.Author);

            var prevEventNumber = Math.Min(msg.FromEventNumber, msg.LastEventNumber) + 1;
            var nextEventNumber = msg.FromEventNumber - msg.MaxCount;

            feed.AddLink("self", self);
            feed.AddLink("first", HostName.Combine(requestedUrl, "/streams/{0}/head/backward/{1}", escapedStreamId, msg.MaxCount));
            if (!msg.IsEndOfStream)
            {
                if (nextEventNumber < 0) throw new Exception(string.Format("nextEventNumber is negative: {0} while IsEndOfStream", nextEventNumber));
                feed.AddLink("last", HostName.Combine(requestedUrl, "/streams/{0}/{1}/forward/{2}", escapedStreamId, 0, msg.MaxCount));
                feed.AddLink("next", HostName.Combine(requestedUrl, "/streams/{0}/{1}/backward/{2}", escapedStreamId, nextEventNumber, msg.MaxCount));
            }
            feed.AddLink("previous", HostName.Combine(requestedUrl, "/streams/{0}/{1}/forward/{2}", escapedStreamId, prevEventNumber, msg.MaxCount));
            feed.AddLink("metadata", HostName.Combine(requestedUrl, "/streams/{0}/metadata", escapedStreamId));
            for (int i = 0; i < msg.Events.Length; ++i)
            {
                feed.AddEntry(ToEntry(msg.Events[i], requestedUrl, embedContent));
            }

            return feed;
        }

        public static FeedElement ToAllEventsForwardFeed(ClientMessage.ReadAllEventsForwardCompleted msg, Uri requestedUrl, EmbedLevel embedContent)
        {
            var self = HostName.Combine(requestedUrl, "/streams/{0}", AllEscaped);
            var feed = new FeedElement();
            feed.SetTitle("All events");
            feed.SetId(self);
            feed.SetUpdated(msg.Events.Length > 0 ? msg.Events[msg.Events.Length - 1].Event.TimeStamp : DateTime.MinValue.ToUniversalTime());
            feed.SetAuthor(AtomSpecs.Author);

            feed.AddLink("self", self);
            feed.AddLink("first", HostName.Combine(requestedUrl, "/streams/{0}/head/backward/{1}", AllEscaped, msg.MaxCount));
            if (msg.CurrentPos.CommitPosition != 0)
            {
                feed.AddLink("last", HostName.Combine(requestedUrl, "/streams/{0}/{1}/forward/{2}", AllEscaped, new TFPos(0, 0).AsString(), msg.MaxCount));
                feed.AddLink("next", HostName.Combine(requestedUrl, "/streams/{0}/{1}/backward/{2}", AllEscaped, msg.PrevPos.AsString(), msg.MaxCount));
            }
            if (!msg.IsEndOfStream || msg.Events.Length > 0)
                feed.AddLink("previous", HostName.Combine(requestedUrl, "/streams/{0}/{1}/forward/{2}", AllEscaped, msg.NextPos.AsString(), msg.MaxCount));
            feed.AddLink("metadata", HostName.Combine(requestedUrl, "/streams/{0}/metadata", AllEscaped));
            for (int i = msg.Events.Length - 1; i >= 0; --i)
            {
                feed.AddEntry(ToEntry(new ResolvedEvent(msg.Events[i].Event, msg.Events[i].Link), requestedUrl, embedContent));
            }
            return feed;
        }

        public static FeedElement ToAllEventsBackwardFeed(ClientMessage.ReadAllEventsBackwardCompleted msg, Uri requestedUrl, EmbedLevel embedContent)
        {
            var self = HostName.Combine(requestedUrl, "/streams/{0}", AllEscaped);
            var feed = new FeedElement();
            feed.SetTitle(string.Format("All events"));
            feed.SetId(self);
            feed.SetUpdated(msg.Events.Length > 0 ? msg.Events[0].Event.TimeStamp : DateTime.MinValue.ToUniversalTime());
            feed.SetAuthor(AtomSpecs.Author);

            feed.AddLink("self", self);
            feed.AddLink("first", HostName.Combine(requestedUrl, "/streams/{0}/head/backward/{1}", AllEscaped, msg.MaxCount));
            if (!msg.IsEndOfStream)
            {
                feed.AddLink("last", HostName.Combine(requestedUrl, "/streams/{0}/{1}/forward/{2}", AllEscaped, new TFPos(0, 0).AsString(), msg.MaxCount));
                feed.AddLink("next", HostName.Combine(requestedUrl, "/streams/{0}/{1}/backward/{2}", AllEscaped, msg.NextPos.AsString(), msg.MaxCount));
            }
            feed.AddLink("previous", HostName.Combine(requestedUrl, "/streams/{0}/{1}/forward/{2}", AllEscaped, msg.PrevPos.AsString(), msg.MaxCount));
            feed.AddLink("metadata", HostName.Combine(requestedUrl, "/streams/{0}/metadata", AllEscaped));
            for (int i = 0; i < msg.Events.Length; ++i)
            {
                feed.AddEntry(ToEntry(new ResolvedEvent(msg.Events[i].Event, msg.Events[i].Link), requestedUrl, embedContent));
            }
            return feed;
        }

        public static EntryElement ToEntry(ResolvedEvent eventLinkPair, Uri requestedUrl, EmbedLevel embedContent, bool singleEntry = false)
        {
            if (eventLinkPair.Event == null || requestedUrl == null)
                return null;

            var evnt = eventLinkPair.Event;

            EntryElement entry;
            if (embedContent > EmbedLevel.Content)
            {
                var richEntry = new RichEntryElement();
                entry = richEntry;

                richEntry.EventType = evnt.EventType;
                richEntry.EventNumber = evnt.EventNumber;
                richEntry.StreamId = evnt.EventStreamId;
                richEntry.PositionEventNumber = eventLinkPair.OriginalEvent.EventNumber;
                richEntry.PositionStreamId = eventLinkPair.OriginalEvent.EventStreamId;
                richEntry.IsJson = (evnt.Flags & PrepareFlags.IsJson) != 0;
                if (embedContent >= EmbedLevel.Body)
                {
                    if (richEntry.IsJson)
                    {
                        if (embedContent >= EmbedLevel.PrettyBody)
                        {
                            try
                            {
                                richEntry.Data = Helper.UTF8NoBom.GetString(evnt.Data);
                                // next step may fail, so we have already assigned body
                                richEntry.Data = FormatJson(Helper.UTF8NoBom.GetString(evnt.Data));
                            }
                            catch
                            {
                                // ignore - we tried
                            }
                        }
                        else
                            richEntry.Data = Helper.UTF8NoBom.GetString(evnt.Data);
                    }
                    else if (embedContent >= EmbedLevel.TryHarder)
                    {
                        try
                        {
                            richEntry.Data = Helper.UTF8NoBom.GetString(evnt.Data);
                            // next step may fail, so we have already assigned body
                            richEntry.Data = FormatJson(richEntry.Data);
                            // it is json if successed
                            richEntry.IsJson = true;
                        }
                        catch 
                        {
                            // ignore - we tried
                        }
                    }
                    // metadata
                    if (embedContent >= EmbedLevel.PrettyBody)
                    {
                        try
                        {
                            richEntry.MetaData = Helper.UTF8NoBom.GetString(evnt.Metadata);
                            richEntry.IsMetaData = richEntry.MetaData.IsNotEmptyString();
                            // next step may fail, so we have already assigned body
                            richEntry.MetaData = FormatJson(richEntry.MetaData);
                        }
                        catch
                        {
                            // ignore - we tried
                        }
                        var lnk = eventLinkPair.Link;
                        if (lnk != null)
                        {
                            try
                            {
                                richEntry.LinkMetaData = Helper.UTF8NoBom.GetString(lnk.Metadata);
                                richEntry.IsLinkMetaData = richEntry.LinkMetaData.IsNotEmptyString();
                                // next step may fail, so we have already assigned body
                                richEntry.LinkMetaData = FormatJson(richEntry.LinkMetaData);
                            }
                            catch
                            {
                                // ignore - we tried
                            }
                        }
                    }
                }
            }
            else
            {
                entry = new EntryElement();
            }

            var escapedStreamId = Uri.EscapeDataString(evnt.EventStreamId);
            entry.SetTitle(evnt.EventNumber + "@" + evnt.EventStreamId);
            entry.SetId(HostName.Combine(requestedUrl, "/streams/{0}/{1}", escapedStreamId, evnt.EventNumber));
            entry.SetUpdated(evnt.TimeStamp);
            entry.SetAuthor(AtomSpecs.Author);
            entry.SetSummary(evnt.EventType);
            if ((singleEntry || embedContent == EmbedLevel.Content) && ((evnt.Flags & PrepareFlags.IsJson) != 0))
                entry.SetContent(AutoEventConverter.CreateDataDto(eventLinkPair));
            entry.AddLink("edit", HostName.Combine(requestedUrl, "/streams/{0}/{1}", escapedStreamId, evnt.EventNumber));
            entry.AddLink("alternate", HostName.Combine(requestedUrl, "/streams/{0}/{1}", escapedStreamId, evnt.EventNumber));

            return entry;
        }

        private static string FormatJson(string unformattedjson)
        {
            if (string.IsNullOrEmpty(unformattedjson))
                return unformattedjson;
            var jo = JObject.Parse(unformattedjson);
            var json = JsonConvert.SerializeObject(jo, Formatting.Indented);
            return json;
        }
    }

}