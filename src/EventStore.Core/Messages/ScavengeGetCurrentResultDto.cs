namespace EventStore.Core.Messages {
	public class ScavengeGetCurrentResultDto {
		public string ScavengeId { get; set; } = "";
		public string ScavengeLink { get; set; } = "";

		public override string ToString() =>
			$"ScavengeId: {ScavengeId}, " +
			$"ScavengeLink: {ScavengeLink}";
	}
}
