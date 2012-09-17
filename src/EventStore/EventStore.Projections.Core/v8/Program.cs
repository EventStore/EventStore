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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using EventStore.Projections.Core.v8;

namespace js1test
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var preludeFileName = args[0];
            var preludeScript = File.ReadAllText(preludeFileName);
            var queryFileName = args[1];
            var queryScript = File.ReadAllText(queryFileName);

            Func<string, Tuple<string, string>> loadModule =
                moduleName =>
                    {
                        var moduleFilename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, moduleName + ".js");
                        return
                            Tuple.Create(
                                File.ReadAllText(moduleFilename),
                                moduleFilename);
                    };

            using (var prelude = new PreludeScript(preludeScript, preludeFileName, loadModule))
            using (var query = new QueryScript(prelude, queryScript, queryFileName))
            using (var events = File.OpenText(args[2]))
            using (var output = (args.Length >= 4) ? File.CreateText(args[3]) : Console.Out)
            {
                long totalMs = 0;
                int count = 0;
                query.Initialize();
                if (output != null)
                {
                    var capturedOutput = output;
                    query.Emit += s => capturedOutput.WriteLine(s.Trim());
                }
                var sw = new Stopwatch();
                while (!events.EndOfStream)
                {
                    var eventJson = events.ReadLine().Trim();
                    if (!string.IsNullOrWhiteSpace(eventJson))
                    {
                        sw.Start();
                        query.Push(eventJson, null);
                        count++;
                        sw.Stop();
                    }
                }
                totalMs = sw.ElapsedMilliseconds;
                Console.WriteLine(query.GetState());
                Console.WriteLine(query.GetStatistics());
                Console.WriteLine("Total JS push processing time: {0,5:f2} ms", totalMs);
                Console.WriteLine("Average time per 1000 pushes:  {0,5:f2} ms", 1000f*totalMs/(float) count);
                Console.WriteLine("Pure JS events per second:     {0,5:f2} events", count*1000f/totalMs);
                Console.WriteLine("Total events processed:        {0} events", count);
            }
        }
    }
}