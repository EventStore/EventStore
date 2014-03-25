using Newtonsoft.Json.Linq;

namespace EventStore.Common.Options
{
    internal interface IOptionContainer
    {
        string Name { get; }
        object FinalValue { get; }
        bool IsSet { get; }
        bool HasDefault { get; }

        OptionOrigin Origin { get; set; }
        string OriginName { get; set; }
        string OriginOptionName { get; set; }
        bool DontParseFurther { get; }

        void ParseFromEnvironment();
        void ParseFromConfig(JObject json, string configName);
    }
}