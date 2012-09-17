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
using System.Text;
using EventStore.Common.Log;

namespace EventStore.TestClient
{
    public static class PerfUtils
    {
        public class NameValue
        {
            public string Name;
            public object Value;

            public NameValue(string name, object value)
            {
                Name = name;
                Value = value;
            }
        }

        public static NameValue Col(string name, object value)
        {
            return new NameValue(name, value);
        }

        public static NameValue[] Row(params NameValue[] nameValuesList)
        {
            return nameValuesList;
        }

        private static readonly Lazy<ILogger> Log = new Lazy<ILogger>(() => LogManager.GetLogger("integrationOutput"));

        private const string ColumnSplitter = ";";
        private const string PairSplitter = ":";

        private static string Format(string name, object value)
        {
            return string.Format("{0}{1}{2}{3}", name, PairSplitter, value, ColumnSplitter);
        }

        public static void LogData(string dataName, params NameValue[][] rows)
        {
            var sb = new StringBuilder();

            sb.AppendLine("[[begin");
            sb.AppendLine(Format("DataName", dataName));
            foreach (var cols in rows)
            {
                foreach (var nameValue in cols)
                {
                    sb.Append(Format(nameValue.Name, nameValue.Value));
                }
                sb.AppendLine();
            }
            sb.AppendLine("end]]");

            Log.Value.Debug(sb.ToString());
        }

        public static void LogTeamCityGraphData(string key, long value)
        {
            if (value < 0)
            {
                Log.Value.Error("value is {0}, however TeamCity requires Value as a positive (non negative) integer.", value);
                return;
            }

            // ##teamcity[buildStatisticValue key='<valueTypeKey>' value='<value>']
            const string teamCityFormat = "##teamcity[buildStatisticValue key='{0}' value='{1}']";
            Log.Value.Debug(teamCityFormat, key, value);
        }
    }
}