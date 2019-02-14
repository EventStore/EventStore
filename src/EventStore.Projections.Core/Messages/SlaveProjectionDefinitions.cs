using System;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;

namespace EventStore.Projections.Core.Messages {
	public sealed class SlaveProjectionDefinitions {
		public enum SlaveProjectionRequestedNumber {
			One,
			OnePerNode,
			OnePerThread
		}

		public class Definition {
			public Definition(
				string name, string handlerType, string query, SlaveProjectionRequestedNumber requestedNumber,
				ProjectionMode mode, bool emitEnabled, bool checkpointsEnabled, bool enableRunAs,
				bool trackEmittedStreams,
				SerializedRunAs runAs1) {
				Name = name;
				HandlerType = handlerType;
				Query = query;
				RequestedNumber = requestedNumber;
				RunAs1 = runAs1;
				EnableRunAs = enableRunAs;
				CheckpointsEnabled = checkpointsEnabled;
				TrackEmittedStreams = trackEmittedStreams;
				EmitEnabled = emitEnabled;
				Mode = mode;
			}

			public string Name;

			public SlaveProjectionRequestedNumber RequestedNumber;

			public ProjectionMode Mode;

			public bool EmitEnabled;

			public bool CheckpointsEnabled;

			public bool TrackEmittedStreams;

			public bool EnableRunAs;

			public SerializedRunAs RunAs1;

			public string HandlerType;

			public string Query;
		}

		public SlaveProjectionDefinitions(params Definition[] definitions) {
			Definitions = definitions;
		}

		public Definition[] Definitions;
	}
}
