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
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.TestClient.Commands.DvuBasic;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    class ProjGenerateSampleData : ProjectionsScenarioBase
    {
        public ProjGenerateSampleData(
            Action<IPEndPoint, byte[]> directSendOverTcp, int maxConcurrentRequests, int connections, int streams,
            int eventsPerStream, int streamDeleteStep, string dbParentPath)
            : base(
                directSendOverTcp, maxConcurrentRequests, connections, streams, eventsPerStream, streamDeleteStep,
                dbParentPath)
        {
        }

        private EventData CreateBankEvent(int version)
        {
            var accountObject = BankAccountEventFactory.CreateAccountObject(version);
            var @event = BankAccountEvent.FromEvent(accountObject);
            return @event;
        }

        protected virtual int GetIterationCode()
        {
            return 1;
        }

        protected override void RunInternal()
        {
            StartNode();

            var writeTask = WriteData();

            writeTask.Wait();
            
            Console.ReadLine();
        }

        protected Task WriteData()
        {
            var streams =
                Enumerable.Range(0, Streams)
                          .Select(i => string.Format("bank_account_it{0}-{1}", GetIterationCode(), i))
                          .ToArray();
            var slices = Split(streams, 3);

            var w1 = Write(WriteMode.SingleEventAtTime, slices[0], EventsPerStream, CreateBankEvent);
            var w2 = Write(WriteMode.Bucket, slices[1], EventsPerStream, CreateBankEvent);
            var w3 = Write(WriteMode.Transactional, slices[2], EventsPerStream, CreateBankEvent);


//            var task = Task.Factory.StartNew(() =>
//            {
//                try
//                {
//                    w1.Wait();
//                }
//                catch
//                {
//                }
//                try
//                {
//                    w2.Wait();
//                }
//                catch
//                {
//                }
//                try
//                {
//                    w3.Wait();
//                }
//                catch
//                {
//                }
//            });
            var task = Task.Factory.ContinueWhenAll(new[] { w1, w2, w3 }, Task.WaitAll/*tasks => {
                                                                                       try
                                                                                       {
                                                                                           Task.WaitAll(tasks);
                                                                                       }
                                                                                       catch
                                                                                       {
                                                                                           Log.Info("EXCEPTION");
                                                                                       }
                                                                                       Log.Info("Data written for iteration {0}.", GetIterationCode();})*/);
            
//            return task;
            return task.ContinueWith(x => Log.Info("Data written for iteration {0}.", GetIterationCode()));
        }
    }
}
