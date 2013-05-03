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
using System.Runtime.Serialization;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages
{
    [DataContract]
    public class QuerySourcesDefinition: ISourceDefinitionConfigurator
    {
        [DataMember(Name = "allStreams")]
        public bool AllStreams { get; set; }

        [DataMember(Name = "categories")]
        public string[] Categories { get; set; }

        [DataMember(Name = "streams")]
        public string[] Streams { get; set; }

        [DataMember(Name = "allEvents")]
        public bool AllEvents { get; set; }

        [DataMember(Name = "events")]
        public string[] Events { get; set; }

        [DataMember(Name = "byStreams")]
        public bool ByStreams { get; set; }

        [DataMember(Name = "byCustomPartitions")]
        public bool ByCustomPartitions { get; set; }

        [DataMember(Name = "definesStateTransform")]
        public bool DefinesStateTransform { get; set; }

        [DataMember(Name = "options")]
        public QuerySourcesDefinitionOptions Options { get; set; }

        public void ConfigureSourceProcessingStrategy(QuerySourceProcessingStrategyBuilder builder)
        {
            if (AllStreams)
                builder.FromAll();
            else
            {
                if (Streams != null)
                    foreach (var stream in Streams)
                        builder.FromStream(stream);
                if (Categories != null)
                    foreach (var category in Categories)
                        builder.FromCategory(category);
            }
            if (AllEvents)
                builder.AllEvents();
            else if (Events != null)
                foreach (var @event in Events)
                    builder.IncludeEvent(@event);
            if (ByStreams)
                builder.SetByStream();
            if (ByCustomPartitions)
                builder.SetByCustomPartitions();
            if (Options != null)
            {
                if (Options.IncludeLinks)
                    builder.SetIncludeLinks();
                if (!String.IsNullOrWhiteSpace(Options.ResultStreamName))
                    builder.SetResultStreamNameOption(Options.ResultStreamName);
                if (!String.IsNullOrWhiteSpace(Options.PartitionResultStreamNamePattern))
                    builder.SetPartitionResultStreamNamePatternOption(Options.PartitionResultStreamNamePattern);
                if (!String.IsNullOrWhiteSpace(Options.ForceProjectionName))
                    builder.SetForceProjectionName(Options.ForceProjectionName);
                if (Options.UseEventIndexes)
                    builder.SetUseEventIndexes(true);
                if (Options.ReorderEvents)
                    builder.SetReorderEvents(true);
                if (Options.ProcessingLag != null)
                    builder.SetProcessingLag(Options.ProcessingLag.GetValueOrDefault());
            }
            if (DefinesStateTransform)
                builder.SetDefinesStateTransform();
        }
    }
}
