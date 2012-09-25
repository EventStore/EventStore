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
using System.Linq;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Atom;

namespace EventStore.Core.Services.Transport.Http
{
    public static class Convert
    {
        public static ServiceDocument ToServiceDocument(IEnumerable<string> userStreams, 
                                                        IEnumerable<string> systemStreams,
                                                        string userHostName)
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
                collection.SetUri(HostName.Combine(userHostName, "/streams/{0}", userStream));

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
                collection.SetUri(HostName.Combine(userHostName, "/streams/{0}", systemStream));

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

        public static FeedElement ToFeed(string eventStreamId, 
                                            int start, 
                                            int count, 
                                            DateTime updateTime,
                                            EventRecord[] items, 
                                            Func<EventRecord, string, EntryElement> itemToEntry,
                                            string userHostName)
        {
            if (string.IsNullOrEmpty(eventStreamId) || items == null || userHostName == null)
                return null;

            if (start == -1)
                start = GetActualStart(items);

            var self = HostName.Combine(userHostName, "/streams/{0}", eventStreamId);

            var feed = new FeedElement();

            feed.SetTitle(String.Format("Event stream '{0}'", eventStreamId));
            feed.SetId(self);

            feed.SetUpdated(updateTime);
            feed.SetAuthor(AtomSpecs.Author);

            feed.AddLink(self, "self", null);
            
            feed.AddLink(HostName.Combine(userHostName, 
                                     "/streams/{0}/range/{1}/{2}",
                                     eventStreamId,
                                     AtomSpecs.FeedPageSize - 1,
                                     AtomSpecs.FeedPageSize),
                         "first",
                         null);


            feed.AddLink(HostName.Combine(userHostName, 
                                     "/streams/{0}/range/{1}/{2}",
                                     eventStreamId,
                                     PrevStart(start),
                                     AtomSpecs.FeedPageSize),
                         "prev",
                         null);
            feed.AddLink(HostName.Combine(userHostName, 
                                     "/streams/{0}/range/{1}/{2}",
                                     eventStreamId,
                                     NextStart(start),
                                     AtomSpecs.FeedPageSize),
                         "next",
                         null);

            foreach (var item in items)
            {
                feed.AddEntry(itemToEntry(item, userHostName));
            }

            return feed;
        }

        private static int PrevStart(int currentStart)
        {
            return currentStart + AtomSpecs.FeedPageSize;
        }

        private static int NextStart(int currentStart)
        {
            var prevStart = currentStart - AtomSpecs.FeedPageSize;
            return prevStart >= 0 ? prevStart : 0;
        }

        private static int GetActualStart(EventRecord[] items)
        {
            return items.Max(e => e.EventNumber);
        }

        public static EntryElement ToEntry(EventRecord evnt, string userHostName)
        {
            if (evnt == null || userHostName == null)
                return null;

            var entry = new EntryElement();

            entry.SetTitle(String.Format("{0} #{1}", evnt.EventStreamId, evnt.EventNumber));

            entry.SetId(HostName.Combine(userHostName, "/streams/{0}/{1}", evnt.EventStreamId, evnt.EventNumber));
            entry.SetUpdated(evnt.TimeStamp);

            entry.SetAuthor(AtomSpecs.Author);
            entry.SetSummary(String.Format("Entry #{0}", evnt.EventNumber));

            entry.AddLink(HostName.Combine(userHostName, "/streams/{0}/{1}", evnt.EventStreamId, evnt.EventNumber), "edit", null);

            entry.AddLink(
                HostName.Combine(userHostName, "/streams/{0}/event/{1}?format=text", evnt.EventStreamId, evnt.EventNumber),
                null,
                ContentType.PlainText);
            entry.AddLink(
                HostName.Combine(userHostName, "/streams/{0}/event/{1}?format=json", evnt.EventStreamId, evnt.EventNumber),
                "alternate",
                ContentType.Json);
            entry.AddLink(
                HostName.Combine(userHostName, "/streams/{0}/event/{1}?format=xml", evnt.EventStreamId, evnt.EventNumber),
                "alternate",
                ContentType.Xml);

            return entry;
        }
    }
}