namespace EventStore.Projections.Core.Services.Processing.Checkpointing;

public interface ICoreProjectionCheckpointReader {
	void BeginLoadState();
	void Initialize();
}
