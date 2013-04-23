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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text;
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Http.Streams;
using EventStore.Core.Tests.Http.Users;
using NUnit.Framework;
using EventStore.Core.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStore.Core.Tests.Http
{
    public abstract class HttpBehaviorSpecification : SpecificationWithDirectoryPerTestFixture
    {
        protected MiniNode _node;
        protected EventStoreConnection _connection;
        protected HttpWebResponse _lastResponse;
        protected string _lastResponseBody;
        protected JsonException _lastJsonException;
        private Func<HttpWebResponse, byte[]> _dumpResponse;
        private Func<HttpWebResponse, int> _dumpResponse2;
        private Func<HttpWebRequest, byte[]> _dumpRequest;
        private Func<HttpWebRequest, byte[]> _dumpRequest2;
        private string _tag;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
#if !__MonoCS__
            EventStore.Common.Utils.Helper.EatException(() => _dumpResponse = CreateDumpResponse());
            EventStore.Common.Utils.Helper.EatException(() => _dumpResponse2 = CreateDumpResponse2());
            EventStore.Common.Utils.Helper.EatException(() => _dumpRequest = CreateDumpRequest());
            EventStore.Common.Utils.Helper.EatException(() => _dumpRequest2 = CreateDumpRequest2());
#endif

            base.TestFixtureSetUp();

            if (SetUpFixture._connection != null && SetUpFixture._node != null)
            {
                _tag = "_" + (++SetUpFixture._counter).ToString();
                _node = SetUpFixture._node;
                _connection = SetUpFixture._connection;
            }
            else
            {
                _tag = "_1";
                _node = CreateMiniNode();
                _node.Start();

                _connection = TestConnection.Create();
                _connection.Connect(_node.TcpEndPoint);
            }
            _lastResponse = null;
            _lastResponseBody = null;
            _lastJsonException = null;

            Given();
            When();

        }

        public string TestStream {
            get { return "/streams/test" + Tag; }
        }

        public string Tag
        {
            get { return _tag; }
        }

        protected virtual MiniNode CreateMiniNode()
        {
            return new MiniNode(PathName);
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            if (SetUpFixture._connection == null || SetUpFixture._node == null)
            {
                _connection.Close();
                _node.Shutdown();
            }
            base.TestFixtureTearDown();
        }

        protected HttpWebRequest CreateRequest(
            string path, string method, string contentType, ICredentials credentials = null)
        {
            var httpWebRequest = (HttpWebRequest) WebRequest.Create(MakeUrl(path));
            httpWebRequest.Method = method;
            httpWebRequest.ContentType = contentType;
            if (credentials != null)
            {
                httpWebRequest.Credentials = credentials;
            }
            return httpWebRequest;
        }

        protected HttpWebRequest CreateRequest(string path, string method)
        {
            var httpWebRequest = (HttpWebRequest) WebRequest.Create(MakeUrl(path));
            httpWebRequest.Method = method;
            return httpWebRequest;
        }

        protected Uri MakeUrl(string path)
        {
            var supplied = new Uri(path, UriKind.RelativeOrAbsolute);
            if (supplied.IsAbsoluteUri)
                return supplied;

            var httpEndPoint = _node.HttpEndPoint;
            return new UriBuilder("http", httpEndPoint.Address.ToString(), httpEndPoint.Port, path).Uri;
        }

        protected HttpWebResponse MakeJsonPost<T>(string path, T body)
        {
            var request = CreateJsonPostRequest(path, "POST", body);
            var httpWebResponse = GetRequestResponse(request);
            return httpWebResponse;
        }

        protected HttpWebResponse MakeJsonPut<T>(string path, T body)
        {
            var request = CreateJsonPostRequest(path, "PUT", body);
            var httpWebResponse = GetRequestResponse(request);
            return httpWebResponse;
        }

        protected HttpWebResponse MakeDelete(string path)
        {
            var request = CreateRequest(path, "DELETE");
            var httpWebResponse = GetRequestResponse(request);
            return httpWebResponse;
        }

        protected HttpWebResponse MakePost(string path)
        {
            var request = CreateJsonPostRequest(path);
            var httpWebResponse = GetRequestResponse(request);
            return httpWebResponse;
        }

        protected T GetJson<T>(string path, string accept = null, ICredentials credentials = null)
        {
            var request = CreateRequest(path, "GET", null, credentials);
            request.Accept = accept ?? "application/json";
            _lastResponse = GetRequestResponse(request);
            var memoryStream = new MemoryStream();
            _lastResponse.GetResponseStream().CopyTo(memoryStream);
            var bytes = memoryStream.ToArray();
            _lastResponseBody = Encoding.UTF8.GetString(bytes);
            try
            {
                return _lastResponseBody.ParseJson<T>();
            }
            catch (JsonException ex)
            {
                _lastJsonException = ex;
                return default(T);
            }
        }

        private HttpWebResponse GetRequestResponse(HttpWebRequest request)
        {
            HttpWebResponse response;
            try
            {
                response = (HttpWebResponse) request.GetResponse();
            }
            catch (WebException ex)
            {
                response = (HttpWebResponse) ex.Response;
            }
            if (_dumpRequest != null)
            {
                var bytes = _dumpRequest(request);
                if (bytes != null)
                    Console.WriteLine(Encoding.ASCII.GetString(bytes, 0, GetBytesLength(bytes)).TrimEnd('\0'));
            }
            if (_dumpRequest2 != null)
            {
                var bytes = _dumpRequest2(request);
                if (bytes != null)
                    Console.WriteLine(Encoding.ASCII.GetString(bytes, 0, GetBytesLength(bytes)).TrimEnd('\0'));
            }
            Console.WriteLine();
            if (_dumpResponse != null)
            {
                var bytes = _dumpResponse(response);
                var len = _dumpResponse2(response);
                if (bytes != null)
                    Console.WriteLine(Encoding.ASCII.GetString(bytes, 0, len).TrimEnd('\0'));
            }
            return response;
        }

        private int GetBytesLength(byte[] bytes)
        {
            var index = Array.IndexOf(bytes, 0);
            return index < 0 ? bytes.Length : index;
        }

        private HttpWebRequest CreateJsonPostRequest<T>(string path, string method, T body)
        {
            var request = CreateRequest(path, method, "application/json");
            request.GetRequestStream().WriteJson(body);
            return request;
        }

        private HttpWebRequest CreateJsonPostRequest(string path)
        {
            var request = CreateRequest(path, "POST");
            request.ContentLength = 0;
            return request;
        }

        protected abstract void Given();
        protected abstract void When();

        protected void AssertJObject(JObject expected, JObject response, string path)
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

        private void AssertJArray(JArray expected, JArray response, string path)
        {
            for (int index = 0; index < expected.Count; index++)
            {
                JToken v = expected[index];
                JToken vv = response[index];
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

        protected void AssertJson<T>(T expected, JObject response)
        {
            var serialized = expected.ToJson();
            var jobject = serialized.ParseJson<JObject>();

            var path = "/";

            AssertJObject(jobject, response, path);
        }

        private static Func<HttpWebResponse, byte[]> CreateDumpResponse()
        {
            var r = Expression.Parameter(typeof (HttpWebResponse), "r");
            var piCoreResponseData = typeof (HttpWebResponse).GetProperty(
                "CoreResponseData",
                BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy| BindingFlags.Instance);
            var fim_ConnectStream = piCoreResponseData.PropertyType.GetField("m_ConnectStream",
                                                                             BindingFlags.GetField| BindingFlags.Public | BindingFlags.FlattenHierarchy| BindingFlags.Instance);
            var connectStreamType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("System.Net.ConnectStream")).Where(t => t != null).FirstOrDefault();
            var fim_ReadBuffer = connectStreamType.GetField("m_ReadBuffer",
                                                            BindingFlags.GetField| BindingFlags.NonPublic | BindingFlags.FlattenHierarchy| BindingFlags.Instance);
            var body = Expression.Field(Expression.Convert(Expression.Field(Expression.Property(r, piCoreResponseData), fim_ConnectStream), connectStreamType), fim_ReadBuffer);
            var debugExpression = Expression.Lambda<Func<HttpWebResponse, byte[]>>(body, r);
            return debugExpression.Compile();
        }

        private static Func<HttpWebResponse, int> CreateDumpResponse2()
        {
            var r = Expression.Parameter(typeof (HttpWebResponse), "r");
            var piCoreResponseData = typeof (HttpWebResponse).GetProperty(
                "CoreResponseData",
                BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy| BindingFlags.Instance);
            var fim_ConnectStream = piCoreResponseData.PropertyType.GetField("m_ConnectStream",
                                                                             BindingFlags.GetField| BindingFlags.Public | BindingFlags.FlattenHierarchy| BindingFlags.Instance);
            var connectStreamType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("System.Net.ConnectStream")).Where(t => t != null).FirstOrDefault();
            var fim_ReadOffset = connectStreamType.GetField("m_ReadOffset",
                                                            BindingFlags.GetField| BindingFlags.NonPublic | BindingFlags.FlattenHierarchy| BindingFlags.Instance);
            var fim_ReadBufferSize = connectStreamType.GetField("m_ReadBufferSize",
                                                            BindingFlags.GetField| BindingFlags.NonPublic | BindingFlags.FlattenHierarchy| BindingFlags.Instance);
            var stream = Expression.Convert(Expression.Field(Expression.Property(r, piCoreResponseData), fim_ConnectStream), connectStreamType);
            var body = Expression.Add(Expression.Field(stream, fim_ReadOffset), Expression.Field(stream, fim_ReadBufferSize));
            var debugExpression = Expression.Lambda<Func<HttpWebResponse, int>>(body, r);
            return debugExpression.Compile();
        }

        private static Func<HttpWebRequest, byte[]> CreateDumpRequest()
        {
            var r = Expression.Parameter(typeof (HttpWebRequest), "r");
            var fi_WriteBuffer = typeof (HttpWebRequest).GetField("_WriteBuffer",
                                                                  BindingFlags.GetField| BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy| BindingFlags.Instance);
            var body = Expression.Field(r, fi_WriteBuffer);
            var debugExpression = Expression.Lambda<Func<HttpWebRequest, byte[]>>(body, r);
            return debugExpression.Compile();
        }

        private static Func<HttpWebRequest, byte[]> CreateDumpRequest2()
        {
            var r = Expression.Parameter(typeof (HttpWebRequest), "r");
            var fi_SubmitWriteStream = typeof (HttpWebRequest).GetField(
                "_SubmitWriteStream",
                BindingFlags.GetField | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy
                | BindingFlags.Instance);
            var connectStreamType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("System.Net.ConnectStream")).Where(t => t != null).FirstOrDefault();
            var piBufferedData = connectStreamType.GetProperty(
                "BufferedData",
                BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy
                | BindingFlags.Instance);
            var fiheadChunk = piBufferedData.PropertyType.GetField(
                "headChunk",
                BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy
                | BindingFlags.Instance);
            var piBuffer = fiheadChunk.FieldType.GetField(
                "Buffer",
                BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy
                | BindingFlags.Instance);
            var headChunk =
                Expression.Field(
                    Expression.Property(
                        Expression.Convert(Expression.Field(r, fi_SubmitWriteStream), connectStreamType), piBufferedData),
                    fiheadChunk);
            var body =
                Expression.Condition(
                    Expression.ReferenceNotEqual(headChunk, Expression.Constant(null, headChunk.Type)),
                    Expression.Field(headChunk, piBuffer), Expression.Constant(null, piBuffer.FieldType));
            var debugExpression = Expression.Lambda<Func<HttpWebRequest, byte[]>>(body, r);
            return debugExpression.Compile();
        }
    // System.Text.Encoding.UTF8.GetString(((System.Net.ConnectStream)(_response.CoreResponseData.m_ConnectStream)).m_ReadBuffer) // m_ReadOffset
    // System.Text.Encoding.UTF8.GetString(((System.Net.HttpWebRequest)(request))._WriteBuffer)
    // ((System.Net.ConnectStream)(request._SubmitWriteStream)).BufferedData.headChunk.Buffer
    }
}
