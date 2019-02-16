#if NET452
using System.Threading.Tasks;
namespace EventStore.ClientAPI.Common.Utils.Threading
{
    internal static class TaskEx
    {
        public static readonly System.Threading.Tasks.Task CompletedTask = CreateCompletedTask();

        private static System.Threading.Tasks.Task CreateCompletedTask()
        {
            var tcs = new TaskCompletionSource<bool>();
            tcs.TrySetResult(true);
            return tcs.Task;
        }
    }
}
#endif
