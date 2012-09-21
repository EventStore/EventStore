using System;

namespace EventStore.ClientAPI.TaskWrappers
{
    public enum ProcessResultStatus
    {
        Success,
        Retry,
        NotifyError
    }

    public class ProcessResult
    {
        public readonly ProcessResultStatus Status;
        public readonly Exception Exception;

        public ProcessResult(ProcessResultStatus status, Exception exception = null)
        {
            Status = status;
            Exception = exception;
        }
    }
}