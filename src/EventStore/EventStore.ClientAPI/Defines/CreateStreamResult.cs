namespace EventStore.ClientAPI.Defines
{
    public class CreateStreamResult
    {
        public bool IsSuccessful { get; private set; }
        public OperationErrorCode LastErrorCode { get; private set; }

        public CreateStreamResult(OperationErrorCode operationErrorCode)
        {
            IsSuccessful = operationErrorCode == OperationErrorCode.Success;
            LastErrorCode = operationErrorCode;
        }
    }
}