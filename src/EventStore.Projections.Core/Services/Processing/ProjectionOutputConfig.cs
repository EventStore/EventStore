using System.Runtime.Serialization;

namespace EventStore.Projections.Core.Services.Processing {
	[DataContract]
	public class ProjectionOutputConfig {
		[DataMember] public string ResultStreamName { get; set; }
	}
}
