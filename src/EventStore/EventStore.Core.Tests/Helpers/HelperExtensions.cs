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

using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Utils;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace EventStore.Core.Tests.Helpers
{
    public static class HelperExtensions
    {
        public static bool IsBetween(this int n, int a, int b)
        {
            return n >= a && n <= b;
        }

        public static bool AreEqual<TKey, TValue>(this IDictionary<TKey, TValue> first, IDictionary<TKey, TValue> second)
        {
            if (first.Count != second.Count)
                return false;

            TValue value;
            return first.All(kvp => second.TryGetValue(kvp.Key, out value) && value.Equals(kvp.Value));
        }

        public static void AssertJObject(JObject expected, JObject response, string path)
        {
            foreach (KeyValuePair<string, JToken> v in expected)
            {
                JToken vv;
                if (v.Key.EndsWith("___"))
                {
                    if (response.TryGetValue(v.Key.Substring(0, v.Key.Length - "___".Length), out vv))
                    {
                        Assert.Fail("{0}/{1} found, but it is explicitly forbidden", path, v.Key);
                    }
                }
                else if (!response.TryGetValue(v.Key, out vv))
                {
                    Assert.Fail("{0}/{1} not found in '{2}'", path, v.Key, response.ToString());
                }
                else
                {
                    Assert.AreEqual(
                        v.Value.Type, vv.Type, "{0}/{1} type is {2}, but {3} is expected", path, v.Key, vv.Type,
                        v.Value.Type);
                    if (v.Value.Type == JTokenType.Object)
                    {
                        AssertJObject(v.Value as JObject, vv as JObject, path + "/" + v.Key);
                    }
                    else if (v.Value.Type == JTokenType.Array)
                    {
                        AssertJArray(v.Value as JArray, vv as JArray, path + "/" + v.Key);
                    }
                    else if (v.Value is JValue)
                    {
                        Assert.AreEqual(
                            ((JValue) (v.Value)).Value, ((JValue) vv).Value,
                            "{0}/{1} value is '{2}' but '{3}' is expected", path, v.Key, vv, v.Value);
                    }
                    else
                        Assert.Fail();
                }
            }
        }

        public static void AssertJArray(JArray expected, JArray response, string path)
        {
            for (int index = 0; index < expected.Count; index++)
            {
                JToken v = expected.Count > index ? expected[index] : new JValue((object)null);
                JToken vv = response.Count > index ? response[index] : new JValue((object) null);
                Assert.AreEqual(
                    v.Type, vv.Type, "{0}/{1} type is {2}, but {3} is expected", path, index, vv.Type,
                    v.Type);
                if (v.Type == JTokenType.Object)
                {
                    AssertJObject(v as JObject, vv as JObject, path + "/" + index);
                }
                else if (v.Type == JTokenType.Array)
                {
                    AssertJArray(v as JArray, vv as JArray, path + "/" + index);
                }
                else if (v is JValue)
                {
                    Assert.AreEqual(
                        ((JValue) v).Value, ((JValue) vv).Value, "{0}/{1} value is '{2}' but '{3}' is expected", path,
                        index, vv, v);
                }
                else
                    Assert.Fail();
            }
        }

        public static void AssertJson<T>(T expected, JObject response)
        {
            var serialized = expected.ToJson();
            var jobject = serialized.ParseJson<JObject>();

            var path = "/";

            AssertJObject(jobject, response, path);
        }
    }
}