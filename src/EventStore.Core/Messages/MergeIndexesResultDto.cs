namespace EventStore.Core.Messages {
	public class MergeIndexesResultDto {
		public string MergeIndexesId { get; set; }

		public MergeIndexesResultDto() {
		}

		public MergeIndexesResultDto(string mergeIndexesId) {
			MergeIndexesId = mergeIndexesId;
		}
	}
}
