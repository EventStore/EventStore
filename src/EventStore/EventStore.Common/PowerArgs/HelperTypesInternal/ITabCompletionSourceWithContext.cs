
namespace PowerArgs
{
    internal interface ITabCompletionSourceWithContext : ITabCompletionSource
    {
        bool TryComplete(bool shift, string context, string soFar, out string completion);
    }
}
