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
using System.Collections.Generic;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Atom;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStore.Core.Services.Transport.Http
{
    public static class Convert
    {
        private static readonly string AllEscaped = Uri.EscapeDataString("$all");

        public static ServiceDocument ToServiceDocument(IEnumerable<string> userStreams, IEnumerable<string> systemStreams, string userHostName)
        {
            if (userStreams == null || systemStreams == null || userHostName == null)
                return null;

            var document = new ServiceDocument();

            var userWorkspace = new WorkspaceElement();
            userWorkspace.SetTitle("User event streams");

            var systemWorkspace = new WorkspaceElement();
            systemWorkspace.SetTitle("System event streams");

            foreach (var userStream in userStreams)
            {
                var collection = new CollectionElement();

                collection.SetTitle(userStream);
                collection.SetUri(HostName.Combine(userHostName, "/streams/{0}", Uri.EscapeDataString(userStream)));

                collection.AddAcceptType(ContentType.Xml);
                collection.AddAcceptType(ContentType.Atom);
                collection.AddAcceptType(ContentType.Json);
                collection.AddAcceptType(ContentType.AtomJson);

                userWorkspace.AddCollection(collection);
            }

            foreach (var systemStream in systemStreams)
            {
                var collection = new CollectionElement();

                collection.SetTitle(systemStream);
                collection.SetUri(HostName.Combine(userHostName, "/streams/{0}", Uri.EscapeDataString(systemStream)));

                collection.AddAcceptType(ContentType.Xml);
                collection.AddAcceptType(ContentType.Atom);
                collection.AddAcceptType(ContentType.Json);
                collection.AddAcceptType(ContentType.AtomJson);

                systemWorkspace.AddCollection(collection);
            }

            document.AddWorkspace(userWorkspace);
            document.AddWorkspace(systemWorkspace);

            return document;
        }

        public static FeedElement ToReadStreamFeed(ClientMessage.ReadStreamEventsBackwardCompleted msg, string userHostName, EmbedLevel embedContent)
        {
            Ensure.NotNull(msg, "msg");

            string escapedStreamId = Uri.EscapeDataString(msg.EventStreamId);
            var self = HostName.Combine(userHostName, "/streams/{0}", escapedStreamId);
            var feed = new FeedElement();
            feed.SetTitle(string.Format("Event stream '{0}'", msg.EventStreamId));
            feed.SetId(self);
            feed.SetUpdated(msg.Events.Length > 0 ? msg.Events[0].Event.TimeStamp : DateTime.MinValue.ToUniversalTime());
            feed.SetAuthor(AtomSpecs.Author);

            feed.AddLink("self", self);
            feed.AddLink("first", HostName.Combine(userHostName, "/streams/{0}", escapedStreamId)); // TODO AN: should account for msg.MaxCount
            feed.AddLink("last", HostName.Combine(userHostName, "/streams/{0}/range/{1}/{2}", escapedStreamId, msg.MaxCount - 1, msg.MaxCount));
            feed.AddLink("previous", HostName.Combine(userHostName, 
                                                      "/streams/{0}/range/{1}/{2}", 
                                                      escapedStreamId, 
                                                      Math.Min(msg.FromEventNumber, msg.LastEventNumber) + msg.MaxCount, 
                                                      msg.MaxCount));

            if (msg.FromEventNumber - msg.MaxCount >= 0)
            {
                feed.AddLink("next", HostName.Combine(userHostName,
                                                      "/streams/{0}/range/{1}/{2}",
                                                      escapedStreamId,
                                                      msg.FromEventNumber - msg.MaxCount,
                                                      msg.MaxCount));
            }

            for (int i = 0; i < msg.Events.Length; ++i)
            {
                feed.AddEntry(ToEntry(msg.Events[i].Event, msg.Events[i].Link, userHostName, embedContent));
            }

            return feed;
        }

        public static FeedElement ToAllEventsForwardFeed(ReadAllResult result, string userHostName, EmbedLevel embedContent)
        {
            var self = HostName.Combine(userHostName, "/streams/{0}", AllEscaped);
            var feed = new FeedElement();
            feed.SetTitle("All events");
            feed.SetId(self);
            feed.SetUpdated(result.Records.Length > 0 ? result.Records[result.Records.Length - 1].Event.TimeStamp : DateTime.MinValue.ToUniversalTime());
            feed.SetAuthor(AtomSpecs.Author);

            feed.AddLink("self", self);
            feed.AddLink("first", HostName.Combine(userHostName, "/streams/{0}/{1}", AllEscaped, result.MaxCount));
            feed.AddLink("last", HostName.Combine(userHostName, "/streams/{0}/after/{1}/{2}", AllEscaped, new TFPos(0, 0).AsString(), result.MaxCount));
            feed.AddLink("previous", HostName.Combine(userHostName, "/streams/{0}/after/{1}/{2}", AllEscaped, result.NextPos.AsString(), result.MaxCount));
            feed.AddLink("next", HostName.Combine(userHostName, "/streams/{0}/before/{1}/{2}", AllEscaped, result.PrevPos.AsString(), result.MaxCount));

            for (int i = result.Records.Length - 1; i >= 0; --i)
            {
                feed.AddEntry(ToEntry(result.Records[i].Event, result.Records[i].Link, userHostName, embedContent));
            }
            return feed;
        }

        public static FeedElement ToAllEventsBackwardFeed(ReadAllResult result, string userHostName, EmbedLevel embedContent)
        {
            var self = HostName.Combine(userHostName, "/streams/{0}", AllEscaped);
            var feed = new FeedElement();
            feed.SetTitle(string.Format("All events"));
            feed.SetId(self);

            feed.SetUpdated(result.Records.Length > 0 ? result.Records[0].Event.TimeStamp : DateTime.MinValue.ToUniversalTime());
            feed.SetAuthor(AtomSpecs.Author);

            feed.AddLink("self", self);
            feed.AddLink("first", HostName.Combine(userHostName, "/streams/{0}/{1}", AllEscaped, result.MaxCount));
            feed.AddLink("last", HostName.Combine(userHostName, "/streams/{0}/after/{1}/{2}", AllEscaped, new TFPos(0, 0).AsString(), result.MaxCount));
            feed.AddLink("previous", HostName.Combine(userHostName, "/streams/{0}/after/{1}/{2}", AllEscaped, result.PrevPos.AsString(), result.MaxCount));
            feed.AddLink("next", HostName.Combine(userHostName, "/streams/{0}/before/{1}/{2}", AllEscaped, result.NextPos.AsString(), result.MaxCount));

            for (int i = 0; i < result.Records.Length; ++i)
            {
                feed.AddEntry(ToEntry(result.Records[i].Event, result.Records[i].Link, userHostName, embedContent));
            }
            return feed;
        }

        public static EntryElement ToEntry(EventRecord evnt, EventRecord link, string userHostName, EmbedLevel embedContent)
        {
            if (evnt == null || userHostName == null)
                return null;

            EntryElement entry;
            if (embedContent > EmbedLevel.None)
            {
                var richEntry = new RichEntryElement();
                entry = richEntry;

                richEntry.EventType = evnt.EventType;
                richEntry.EventNumber = evnt.EventNumber;
                richEntry.StreamId = evnt.EventStreamId;
                richEntry.PositionEventNumber = (link ?? evnt).EventNumber;
                richEntry.PositionStreamId = (link ?? evnt).EventStreamId;
                richEntry.IsJson = (evnt.Flags & PrepareFlags.IsJson) != 0;
                if (embedContent >= EmbedLevel.Body)
                {
                    if (richEntry.IsJson)
                    {
                        if (embedContent >= EmbedLevel.PrettyBody)
                            richEntry.Data = FormatJson(Encoding.UTF8.GetString(evnt.Data));
                        else 
                            richEntry.Data = Encoding.UTF8.GetString(evnt.Data);
                    }
                    else if (embedContent >= EmbedLevel.TryHarder)
                    {
                        try
                        {
                            richEntry.Data = Encoding.UTF8.GetString(evnt.Data);
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
                    if (embedContent >= EmbedLevel.PrettyBody)
                    {
                        try
                        {
                            richEntry.MetaData = Encoding.UTF8.GetString(evnt.Metadata);
                            richEntry.IsMetaData = richEntry.MetaData.IsNotEmptyString();
                            // next step may fail, so we have already assigned body
                            richEntry.MetaData = FormatJson(richEntry.MetaData);
                        }
                        catch
                        {
                            // ignore - we tried
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
            entry.SetId(HostName.Combine(userHostName, "/streams/{0}/{1}", escapedStreamId, evnt.EventNumber));
            entry.SetUpdated(evnt.TimeStamp);
            entry.SetAuthor(AtomSpecs.Author);
            entry.SetSummary(evnt.EventType);

            entry.AddLink("edit", HostName.Combine(userHostName, "/streams/{0}/{1}", escapedStreamId, evnt.EventNumber));
            entry.AddLink(null, HostName.Combine(userHostName, "/streams/{0}/event/{1}?format=text", escapedStreamId, evnt.EventNumber), ContentType.PlainText);
            entry.AddLink("alternate", HostName.Combine(userHostName, "/streams/{0}/event/{1}?format=json", escapedStreamId, evnt.EventNumber), ContentType.Json);
            entry.AddLink("alternate", HostName.Combine(userHostName, "/streams/{0}/event/{1}?format=xml", escapedStreamId, evnt.EventNumber), ContentType.Xml);

            return entry;
        }

        private static string FormatJson(string unformattedjson)
        {
            var jo = JObject.Parse(unformattedjson);
            var json = JsonConvert.SerializeObject(jo, Formatting.Indented);
            return json;
        }
    }

}