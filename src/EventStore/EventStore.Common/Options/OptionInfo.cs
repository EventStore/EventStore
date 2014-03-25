namespace EventStore.Common.Options
{
    public class OptionInfo
    {
        public readonly bool Success;
        public readonly string Name;
        public readonly object Value;

        public readonly OptionOrigin Origin;
        public readonly string OriginName;
        public readonly string OriginOptionName;

        public readonly string Error;

        public OptionInfo(bool success, string name, object value, OptionOrigin origin, string originName, string originOptionName, string error)
        {
            Success = success;
            Name = name;
            Value = value;
            Origin = origin;
            OriginName = originName;
            OriginOptionName = originOptionName;
            Error = error;
        }
    }
}