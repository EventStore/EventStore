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

namespace EventStore.Projections.Core.Services.Processing
{
    public class ProjectionNamesBuilder 
    {
        public static ProjectionNamesBuilder CreateForTest(string name)
        {
            return new ProjectionNamesBuilder(name);
        }

        public class Factory : QuerySourceProcessingStrategyBuilder
        {
            public ProjectionNamesBuilder Create(string name)
            {
                return new ProjectionNamesBuilder(name, _options);
            }
        }

        private readonly string _name;
        private readonly QuerySourceProcessingStrategyBuilder.QuerySourceOptions _options;
        private readonly string _partitionResultStreamNamePattern;
        private readonly string _resultStreamName;
        private readonly string _partitionCatalogStreamName;
        private readonly string _checkpointStreamName;
        private readonly string _orderStreamName;

        private ProjectionNamesBuilder(string name)
            : this(name, new QuerySourceProcessingStrategyBuilder.QuerySourceOptions())
        {
        }

        public ProjectionNamesBuilder(string name, QuerySourceProcessingStrategyBuilder.QuerySourceOptions options)
        {
            _name = name;
            _options = options;
            _partitionResultStreamNamePattern = _options.PartitionResultStreamNamePattern
                                                ?? ProjectionsStreamPrefix + EffectiveProjectionName + "-{0}" + ProjectionsStateStreamSuffix;
            _resultStreamName = _options.ResultStreamName ?? ProjectionsStreamPrefix + EffectiveProjectionName + ProjectionsStateStreamSuffix;
            _partitionCatalogStreamName = ProjectionsStreamPrefix + EffectiveProjectionName + ProjectionPartitionCatalogStreamSuffix;
            _checkpointStreamName = ProjectionsStreamPrefix + EffectiveProjectionName
                                    + ProjectionCheckpointStreamSuffix;
            _orderStreamName = ProjectionsStreamPrefix + EffectiveProjectionName
                               + ProjectionOrderStreamSuffix;
        }

        public string EffectiveProjectionName
        {
            get { return _options.ForceProjectionName ?? _name; }
        }

        private string GetPartitionResultStreamName(string partitionName)
        {
            return
                String.Format(GetPartitionResultStreamNamePattern(), partitionName);
        }

        public string GetResultStreamName()
        {
            return _resultStreamName;
        }

        public string GetPartitionResultStreamNamePattern()
        {
            return _options.PartitionResultStreamNamePattern
                    ?? ProjectionsStreamPrefix + EffectiveProjectionName + "-{0}" + ProjectionsStateStreamSuffix;
        }

        private const string ProjectionsStreamPrefix = "$projections-";
        private const string ProjectionsStateStreamSuffix = "-result";
        private const string ProjectionCheckpointStreamSuffix = "-checkpoint";
        private const string ProjectionOrderStreamSuffix = "-order";
        private const string ProjectionPartitionCatalogStreamSuffix = "-partitions";

        public string GetPartitionCatalogStreamName()
        {
            return _partitionCatalogStreamName;
        }

        public string GetPartitionResultCatalogStreamName()
        {
            return ProjectionsStreamPrefix + EffectiveProjectionName + ProjectionPartitionCatalogStreamSuffix;
        }

        public string MakePartitionResultStreamName(string statePartition)
        {
            return String.IsNullOrEmpty(statePartition)
                       ? GetResultStreamName()
                       : GetPartitionResultStreamName(statePartition);
        }

        public string MakePartitionCheckpointStreamName(string statePartition)
        {
            if (String.IsNullOrEmpty(statePartition))
                throw new InvalidOperationException("Root partition cannot have a partition checkpoint stream");

            return ProjectionsStreamPrefix + EffectiveProjectionName + "-" + statePartition + ProjectionCheckpointStreamSuffix;
        }


        public string MakeCheckpointStreamName()
        {
            return _checkpointStreamName;
        }

        public string GetOrderStreamName()
        {
            return _orderStreamName;
        }
    }
}
