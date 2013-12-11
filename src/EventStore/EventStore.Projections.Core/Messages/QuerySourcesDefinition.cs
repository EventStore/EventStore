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

using System.Linq;
using System.Runtime.Serialization;

namespace EventStore.Projections.Core.Messages
{
    [DataContract]
    public class QuerySourcesDefinition : IQuerySources
    {
        [DataMember(Name = "allStreams")]
        public bool AllStreams { get; set; }

        [DataMember(Name = "categories")]
        public string[] Categories { get; set; }

        [DataMember(Name = "streams")]
        public string[] Streams { get; set; }

        [DataMember(Name = "catalogStream")]
        public string CatalogStream { get; set; }

        [DataMember(Name = "allEvents")]
        public bool AllEvents { get; set; }

        [DataMember(Name = "events")]
        public string[] Events { get; set; }

        [DataMember(Name = "byStreams")]
        public bool ByStreams { get; set; }

        [DataMember(Name = "byCustomPartitions")]
        public bool ByCustomPartitions { get; set; }

        [DataMember(Name = "limitingCommitPosition")]
        public long? LimitingCommitPosition { get; set; }

        bool IQuerySources.DefinesStateTransform
        {
            get { return Options != null && Options.DefinesStateTransform; }
        }

        bool IQuerySources.DefinesCatalogTransform
        {
            get { return Options != null && Options.DefinesCatalogTransform; }
        }

        bool IQuerySources.ProducesResults
        {
            get { return Options != null && Options.ProducesResults; }
        }

        bool IQuerySources.DefinesFold
        {
            get { return Options != null && Options.DefinesFold; }
        }

        bool IQuerySources.IncludeLinksOption
        {
            get { return Options != null && Options.IncludeLinks; }
        }

        string IQuerySources.ResultStreamNameOption
        {
            get { return Options != null ? Options.ResultStreamName : null; }
        }

        string IQuerySources.PartitionResultStreamNamePatternOption
        {
            get { return Options != null ? Options.PartitionResultStreamNamePattern : null; }
        }

        string IQuerySources.ForceProjectionNameOption
        {
            get { return Options != null ? Options.ForceProjectionName : null; }
        }

        bool IQuerySources.ReorderEventsOption
        {
            get
            {
                return Options != null && Options.ReorderEvents;
            }
        }

        int? IQuerySources.ProcessingLagOption
        {
            get { return Options != null ? Options.ProcessingLag : null; }
        }

        bool IQuerySources.IsBiState
        {
            get { return Options != null ? Options.IsBiState : false; }
        }

        [DataMember(Name = "options")]
        public QuerySourcesDefinitionOptions Options { get; set; }

        public static QuerySourcesDefinition From(IQuerySources sources)
        {
            return new QuerySourcesDefinition
            {
                AllEvents = sources.AllEvents,
                AllStreams = sources.AllStreams,
                ByStreams = sources.ByStreams,
                ByCustomPartitions = sources.ByCustomPartitions,
                Categories = (sources.Categories ?? new string[0]).ToArray(),
                Events = (sources.Events ?? new string[0]).ToArray(),
                Streams = (sources.Streams ?? new string[0]).ToArray(),
                CatalogStream = sources.CatalogStream,
                LimitingCommitPosition = sources.LimitingCommitPosition,
                Options =
                    new QuerySourcesDefinitionOptions
                    {
                        DefinesStateTransform = sources.DefinesStateTransform,
                        DefinesCatalogTransform = sources.DefinesCatalogTransform,
                        ProducesResults = sources.ProducesResults,
                        DefinesFold = sources.DefinesFold,
                        ForceProjectionName = sources.ForceProjectionNameOption,
                        IncludeLinks = sources.IncludeLinksOption,
                        PartitionResultStreamNamePattern = sources.PartitionResultStreamNamePatternOption,
                        ProcessingLag = sources.ProcessingLagOption.GetValueOrDefault(),
                        IsBiState = sources.IsBiState,
                        ReorderEvents = sources.ReorderEventsOption,
                        ResultStreamName = sources.ResultStreamNameOption,
                    }
            };
        }
    }
}
