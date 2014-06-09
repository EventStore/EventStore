using System;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStore.Common.Utils
{
    class IPEndpointConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var endpoint = (IPEndPoint) value;
            writer.WriteValue(string.Format("{0}:{1}", endpoint.Address, endpoint.Port));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var value = JToken.Load(reader).Value<string>();

            var address = value.Substring(0, value.LastIndexOf(':'));
            var port = value.Substring(value.LastIndexOf(':') + 1);

            return new IPEndPoint(IPAddress.Parse(address), Int32.Parse(port));
        }

        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof (IPEndPoint));
        }

    }
}