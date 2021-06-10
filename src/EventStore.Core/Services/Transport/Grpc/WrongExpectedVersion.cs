using EventStore.Core.Services.Transport.Grpc;
using static EventStore.Core.Data.ExpectedVersion;

// ReSharper disable once CheckNamespace
namespace EventStore.Client {
	partial class WrongExpectedVersion {
		public static WrongExpectedVersion Create(StreamRevision currentStreamRevision,
			long expectedStreamPosition) => new() {
			currentStreamRevisionOption_ = currentStreamRevision == StreamRevision.End
				? new Google.Protobuf.WellKnownTypes.Empty()
				: currentStreamRevision.ToUInt64(),
			currentStreamRevisionOptionCase_ = currentStreamRevision == StreamRevision.End
				? CurrentStreamRevisionOptionOneofCase.CurrentNoStream
				: CurrentStreamRevisionOptionOneofCase.CurrentStreamRevision,
			expectedStreamPositionOption_ = expectedStreamPosition switch {
				Any or NoStream or StreamExists =>
					new Google.Protobuf.WellKnownTypes.Empty(),
				_ => StreamRevision.FromInt64(expectedStreamPosition).ToUInt64()
			},
			expectedStreamPositionOptionCase_ = expectedStreamPosition switch {
				Any => ExpectedStreamPositionOptionOneofCase.ExpectedAny,
				NoStream => ExpectedStreamPositionOptionOneofCase.ExpectedNoStream,
				StreamExists => ExpectedStreamPositionOptionOneofCase.ExpectedStreamExists,
				_ => ExpectedStreamPositionOptionOneofCase.ExpectedStreamPosition
			}
		};
	}
}
