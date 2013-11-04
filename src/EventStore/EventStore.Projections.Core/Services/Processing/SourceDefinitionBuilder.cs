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

namespace EventStore.Projections.Core.Services.Processing
{
    public sealed class SourceDefinitionBuilder: IQuerySources // name it!!
    {
        private readonly QuerySourceOptions _options = new QuerySourceOptions();
        private bool _allStreams;
        private List<string> _categories;
        private List<string> _streams;
        private string _catalogStream;
        private bool _allEvents;
        private List<string> _events;
        private bool _byStream;
        private bool _byCustomPartitions;
        private long? _limitingCommitPosotion;

        public SourceDefinitionBuilder()
        {
            _options.DefinesFold = true;
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

        public void FromCatalogStream(string catalogStream)
        {
            _catalogStream = catalogStream;
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
            _options.DefinesStateTransform = true;
            _options.ProducesResults = true;
        }

        public void SetOutputState()
        {
            _options.ProducesResults = true;
        }

        public void NoWhen()
        {
            _options.DefinesFold = false;
        }

        public void SetResultStreamNameOption(string resultStreamName)
        {
            _options.ResultStreamName = String.IsNullOrWhiteSpace(resultStreamName) ? null : resultStreamName;
        }

        public void SetPartitionResultStreamNamePatternOption(string partitionResultStreamNamePattern)
        {
            _options.PartitionResultStreamNamePattern = String.IsNullOrWhiteSpace(partitionResultStreamNamePattern) ? null : partitionResultStreamNamePattern;
        }

        public void SetForceProjectionName(string forceProjectionName)
        {
            _options.ForceProjectionName = String.IsNullOrWhiteSpace(forceProjectionName) ? null : forceProjectionName;
        }

        public void SetReorderEvents(bool reorderEvents)
        {
            _options.ReorderEvents = reorderEvents;
        }

        public void SetProcessingLag(int processingLag)
        {
            _options.ProcessingLag = processingLag;
        }

        public void SetIsBiState(bool isBiState)
        {
            _options.IsBiState = isBiState;
        }

        public bool AllStreams
        {
            get { return _allStreams; }
        }

        public string[] Categories
        {
            get { return _categories != null ? _categories.ToArray() : null; }
        }

        public string[] Streams
        {
            get { return _streams != null ? _streams.ToArray() : null; }
        }

        public string CatalogStream
        {
            get { return _catalogStream; }
        }

        bool IQuerySources.AllEvents
        {
            get
            {
                return _allEvents;
            }
        }

        public string[] Events
        {
            get
            {
                return _events != null ? _events.ToArray() : null; }
        }

        public bool ByStreams
        {
            get { return _byStream; }
        }

        public bool ByCustomPartitions
        {
            get { return _byCustomPartitions; }
        }

        public long? LimitingCommitPosition
        {
            get { return _limitingCommitPosotion; }
        }

        public bool DefinesStateTransform
        {
            get { return _options.DefinesStateTransform; }
        }

        public bool ProducesResults
        {
            get { return _options.ProducesResults; }
        }

        public bool DefinesFold
        {
            get { return _options.DefinesFold; }
        }

        public bool IncludeLinksOption
        {
            get { return _options.IncludeLinks; }
        }

        public string ResultStreamNameOption
        {
            get
            {
                return _options.ResultStreamName;
            }
        }

        public string PartitionResultStreamNamePatternOption
        {
            get { return _options.PartitionResultStreamNamePattern; }
        }

        public string ForceProjectionNameOption
        {
            get { return _options.ForceProjectionName; }
        }

        public bool ReorderEventsOption
        {
            get { return _options.ReorderEvents; }
        }

        public int? ProcessingLagOption
        {
            get { return _options.ProcessingLag; }
        }

        public bool IsBiState 
        {
            get { return _options.IsBiState; }
        }

        public static IQuerySources From(Action<SourceDefinitionBuilder> configure)
        {
            var b = new SourceDefinitionBuilder();
            configure(b);
            return b.Build();
        }

        public IQuerySources Build()
        {
            return QuerySourcesDefinition.From(this);
        }

        public void SetLimitingCommitPosition(long limitingCommitPosition)
        {
            _limitingCommitPosotion = limitingCommitPosition;
        }
    }

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
        public bool IsBiState { get; set; }

        [DataMember]
        public bool DefinesStateTransform { get; set; }

        [DataMember]
        public bool ProducesResults { get; set; }

        [DataMember]
        public bool DefinesFold { get; set; }

        [DataMember]
        public bool IncludeLinks { get; set; }

    }
}