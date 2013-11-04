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

using System.Runtime.Serialization;

namespace EventStore.Projections.Core.Messages
{
    [DataContract]
    public class QuerySourcesDefinitionOptions
    {
        [DataMember(Name = "producesResults")]
        public bool ProducesResults { get; set; }

        [DataMember(Name = "definesFold")]
        public bool DefinesFold { get; set; }

        [DataMember(Name = "definesStateTransform")]
        public bool DefinesStateTransform { get; set;}

        [DataMember(Name = "resultStreamName")]
        public string ResultStreamName { get; set; }

        [DataMember(Name = "partitionResultStreamNamePattern")]
        public string PartitionResultStreamNamePattern { get; set; }

        [DataMember(Name = "$forceProjectionName")]
        public string ForceProjectionName { get; set; }

        [DataMember(Name = "$includeLinks")]
        public bool IncludeLinks { get; set; }

        [DataMember(Name = "reorderEvents")]
        public bool ReorderEvents { get; set; }

        [DataMember(Name = "processingLag")]
        public int? ProcessingLag { get; set; }

        [DataMember(Name = "biState")]
        public bool IsBiState { get; set; }
    }
}
