using System;
using EventStore.Common.Log;
using EventStore.Transport.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace EventStore.Core.Services.Transport.Http.Codecs
{
    public class JsonCodec : ICodec
    {
        public static Formatting Formatting = Formatting.Indented;

        private static readonly ILogger Log = LogManager.GetLoggerFor<JsonCodec>();

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None,
            Converters = new JsonConverter[]
            {
                new StringEnumConverter()
            }
        };


        public string ContentType { get { return EventStore.Transport.Http.ContentType.Json; } }

        public bool CanParse(string format)
        {
            return string.Equals(ContentType, format, StringComparison.OrdinalIgnoreCase);
        }

        public bool SuitableForReponse(AcceptComponent component)
        {
            return component.MediaType == "*"
                   || (string.Equals(component.MediaType, "application", StringComparison.OrdinalIgnoreCase)
                       && (component.MediaSubtype == "*"
                           || string.Equals(component.MediaSubtype, "json", StringComparison.OrdinalIgnoreCase)));
        }

        public T From<T>(string text)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(text, JsonSettings);
            }
            catch (Exception e)
            {
                Log.ErrorException(e, "'{0}' is not a valid serialized {1}", text, typeof(T).FullName);
                return default(T);
            }
        }

        public string To<T>(T value)
        {
            try
            {
                return JsonConvert.SerializeObject(value, Formatting, JsonSettings);
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "Error serializing object {0}", value);
                return null;
            }
        }
    }
}