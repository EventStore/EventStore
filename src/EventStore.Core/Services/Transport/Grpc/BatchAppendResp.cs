// ReSharper disable once CheckNamespace

namespace EventStore.Client.Streams {
	partial class BatchAppendResp {
		partial class Types {
			partial class Success {
				public static Success Completed(long commitPosition, long preparePosition, long currentVersion) => new() {
					positionOptionCase_ = (commitPosition, preparePosition) switch {
						(>=0, >=0) => PositionOptionOneofCase.Position,
						_ => PositionOptionOneofCase.NoPosition
					},
					positionOption_ = (commitPosition, preparePosition) switch {
						(>=0, >=0) => new AllStreamPosition {
							CommitPosition = (ulong)commitPosition,
							PreparePosition = (ulong)preparePosition
						},
						_ => null,
					},
					currentRevisionOptionCase_ = currentVersion switch {
						>= 0 => CurrentRevisionOptionOneofCase.CurrentRevision,
						_ => CurrentRevisionOptionOneofCase.NoStream
					},
					currentRevisionOption_ = currentVersion switch {
						>= 0 => (ulong)currentVersion,
						_ => new Google.Protobuf.WellKnownTypes.Empty()
					}
				};
			}
		}
	}
}
