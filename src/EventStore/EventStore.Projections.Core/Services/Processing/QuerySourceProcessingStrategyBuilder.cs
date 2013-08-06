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
using System.Runtime.Serialization;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Management;

namespace EventStore.Projections.Core.Services.Processing
{
    public abstract class QuerySourceProcessingStrategyBuilder // name it!!
    {
        [DataContract]
        public class QuerySourceOptions
        {
            [DataMember]
            public string ResultStreamName { get; set; }

            [DataMember]
            public string PartitionResultStreamNamePattern { get; set; }

            [DataMember]
            public string ForceProjectionName { get; set; }

            [DataMember]
            public bool ReorderEvents { get; set; }

            [DataMember]
            public int ProcessingLag { get; set; }

            [DataMember]
            public bool DefinesStateTransform { get; set; }

            [DataMember]
            public bool IncludeLinks { get; set; }

        }

        protected readonly QuerySourceOptions _options = new QuerySourceOptions();
        protected bool _allStreams;
        protected List<string> _categories;
        protected List<string> _streams;
        protected bool _allEvents;
        protected List<string> _events;
        protected bool _byStream;
        protected bool _byCustomPartitions;
        protected bool _definesStateTransform;

        public void Apply(IQuerySources definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");

            var s = new SourceDefinition(definition);
            s.ConfigureSourceProcessingStrategy(this);
        }

        public void FromAll()
        {
            _allStreams = true;
        }

        public void FromCategory(string categoryName)
        {
            if (_categories == null)
                _categories = new List<string>();
            _categories.Add(categoryName);
        }

        public void FromStream(string streamName)
        {
            if (_streams == null)
                _streams = new List<string>();
            _streams.Add(streamName);
        }

        public void AllEvents()
        {
            _allEvents = true;
        }

        public void SetIncludeLinks(bool includeLinks = true)
        {
            _options.IncludeLinks = true;
        }

        public void IncludeEvent(string eventName)
        {
            if (_events == null)
                _events = new List<string>();
            _events.Add(eventName);
        }

        public void SetByStream()
        {
            _byStream = true;
        }

        public void SetByCustomPartitions()
        {
            _byCustomPartitions = true;
        }

        public void SetDefinesStateTransform()
        {
            _definesStateTransform = true;
        }

        public void SetResultStreamNameOption(string resultStreamName)
        {
            _options.ResultStreamName = string.IsNullOrWhiteSpace(resultStreamName) ? null : resultStreamName;
        }

        public void SetPartitionResultStreamNamePatternOption(string partitionResultStreamNamePattern)
        {
            _options.PartitionResultStreamNamePattern = string.IsNullOrWhiteSpace(partitionResultStreamNamePattern) ? null : partitionResultStreamNamePattern;
        }

        public void SetForceProjectionName(string forceProjectionName)
        {
            _options.ForceProjectionName = string.IsNullOrWhiteSpace(forceProjectionName) ? null : forceProjectionName;
        }

        public void SetReorderEvents(bool reorderEvents)
        {
            _options.ReorderEvents = reorderEvents;
        }

        public void SetProcessingLag(int processingLag)
        {
            _options.ProcessingLag = processingLag;
        }

        protected HashSet<string> ToSet(IEnumerable<string> list)
        {
            if (list == null)
                return null;
            return new HashSet<string>(list);
        }

        protected void Validate()
        {
            if (!_allStreams && _categories == null && _streams == null)
                throw new InvalidOperationException("None of streams and categories are included");
            if (!_allEvents && _events == null)
                throw new InvalidOperationException("None of events are included");
            if (_streams != null && _categories != null)
                throw new InvalidOperationException(
                    "Streams and categories cannot be included in a filter at the same time");
            if (_allStreams && (_categories != null || _streams != null))
                throw new InvalidOperationException("Both FromAll and specific categories/streams cannot be set");
            if (_allEvents && _events != null)
                throw new InvalidOperationException("Both AllEvents and specific event filters cannot be set");

            if (_byStream && _streams != null)
                throw new InvalidOperationException("foreachStream projections are not supported on stream based sources");
            if (_options.ReorderEvents)
            {
                if (_allStreams)
                    throw new InvalidOperationException("Event reordering cannot be used with fromAll()");
                if (!(_streams != null && _streams.Count > 1))
                {
                    throw new InvalidOperationException(
                        "Event reordering is only available in fromStreams([]) projections");
                }
                if (_options.ProcessingLag < 50)
                    throw new InvalidOperationException("Event reordering requires processing lag at least of 50ms");
            }
        }

    }
}