namespace EventStore.Projections.Core.Services.Processing.Phases {
	public interface IProgressResultWriter {
		void WriteProgress(float progress);
	}
}
