using System;

namespace EventStore.Core.Messages {
	public class ScavengeGetLastResultDto {
		public string ScavengeId { get; set; } = Guid.Empty.ToString();
		public string ScavengeLink { get; set; } = "";
		public string ScavengeResult { get; set; } = "";

		public override string ToString() =>
			$"ScavengeId: {ScavengeId}, " +
			$"ScavengeLink: {ScavengeLink}, " +
			$"ScavengeResult: {ScavengeResult}";
	}
}
