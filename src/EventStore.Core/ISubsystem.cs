namespace EventStore.Core
{
    public interface ISubsystem
    {
        void Register(StandardComponents standardComponents);

        void Start();
        void Stop();
    }
}
