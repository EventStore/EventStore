using System.Text;
using EventStore.ClientAPI;

namespace EventStore.Projections.Core.Tests.ClientAPI {
	internal static class RecordedEventExtensions {
		public static string DebugDataView(this RecordedEvent source) => Encoding.UTF8.GetString(source.Data);
		public static string DebugMetadataView(this RecordedEvent source) => Encoding.UTF8.GetString(source.Metadata);
	}
}
