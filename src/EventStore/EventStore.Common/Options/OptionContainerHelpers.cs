using System;
using System.ComponentModel;
using System.Linq;
using System.Net;
using EventStore.Common.Utils;
using Newtonsoft.Json.Linq;

namespace EventStore.Common.Options
{
    public static class OptionContainerHelpers
    {
        public static T ConvertFromJToken<T>(JToken token)
        {
            if (token.Type == JTokenType.String)
                return ConvertFromString<T>(token.Value<string>());

            return token.Value<T>();
        }

        public static T ConvertFromString<T>(string value)
        {
            Ensure.NotNull(value, "value");

            Type tt = typeof(T);
            bool nullable = tt.IsValueType 
                            && tt.IsGenericType 
                            && !tt.IsGenericTypeDefinition 
                            && tt.GetGenericTypeDefinition() == typeof (Nullable<>);
            Type targetType = nullable ? tt.GetGenericArguments()[0] : typeof(T);
            TypeConverter conv = TypeDescriptor.GetConverter(targetType);

            if (targetType == typeof(IPAddress))
                conv = new IPAddressTypeConverter();

			if (targetType == typeof(IPEndPoint))
				conv = new IPEndPointTypeConverter();

            return (T)conv.ConvertFromString(value);
        }

        public static JToken GetTokenByJsonPath(JObject json, string[] jsonPath)
        {
            Ensure.NotNull(jsonPath, "jsonPath");
            Ensure.Positive(jsonPath.Length, "jsonPath.Length");

            JToken obj = json;
            for (int i = 0; i < jsonPath.Length - 1; ++i)
            {
                if (obj.Type != JTokenType.Object)
                    return null;
                var p = ((JObject)obj).Property(jsonPath[i]);
                if (p == null || p.Value == null)
                    return null;
                obj = p.Value;
            }

            if (obj.Type != JTokenType.Object)
                return null;

            var prop = ((JObject)obj).Property(jsonPath.Last());
            if (prop == null)
                return null;

            var value = prop.Value;
            return value;
        }
    }
}