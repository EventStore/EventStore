// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

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