// Copyright (c) 2012, Event Store Ltd
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
// Neither the name of the Event Store Ltd nor the names of its
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
using System.Linq;
using System.Threading;
using EventStore.TestClient.Commands.DvuAdvanced.Workers;

namespace EventStore.TestClient.Commands.DvuAdvanced
{
    public class DvuAdvancedProcessor : ICmdProcessor
    {
        public string Keyword
        {
            get
            {
                return "verifyfl";
            }
        }

        public string Usage
        {
            get
            {
                return string.Format("{0} " +
                                     "<writers, default = 20> " +
                                     "<readers, default = 30> " +
                                     "<events, default = 100000> " +
                                     "<producers, default = [test], available = [{1}]>",
                                     Keyword,
                                     String.Join(",", AvailableProducers));
            }
        }

        public IEnumerable<string> AvailableProducers
        {
            get
            {
                yield return "test";
                yield return "bank";
            }
        }

        public IProducer[] Producers { get; set; }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            var writers = 20;
            var readers = 30;
            var events = 100000;
            var producers = new[] { "test" };

            if (args.Length != 0 && args.Length != 4)
            {
                context.Log.Error("Invalid number of arguments. Should be 0 or 4");
                return false;
            }

            if (args.Length > 0)
            {
                int writersArg;
                if (int.TryParse(args[0], out writersArg))
                {
                    writers = writersArg;
                    int readersArg;
                    if (int.TryParse(args[1], out readersArg))
                    {
                        readers = readersArg;
                        int eventsArg;
                        if (int.TryParse(args[2], out eventsArg))
                        {
                            events = eventsArg;
                            string[] producersArg;
                            if ((producersArg = args[3].Split(new[] {","}, StringSplitOptions.RemoveEmptyEntries)).Length > 0)
                            {
                                producersArg = producersArg.Select(p => p.Trim().ToLower()).Distinct().ToArray();
                                if (producersArg.Any(p => !AvailableProducers.Contains(p)))
                                {
                                    context.Log.Error("Invalid producers argument. Pass comma-separated subset of [{0}]",
                                                      String.Join(",", AvailableProducers));
                                    return false;
                                }

                                producers = producersArg;
                            }
                            else
                            {
                                context.Log.Error("Invalid argument value for <producers>");
                                return false;
                            }
                        }
                        else
                        {
                            context.Log.Error("Invalid argument value for <events>");
                            return false;
                        }
                    }
                    else
                    {
                        context.Log.Error("Invalid argument value for <readers>");
                        return false;
                    }
                }
                else
                {
                    context.Log.Error("Invalid argument value for <writers>");
                    return false;
                }
            }

            return InitProducers(producers) && Run(context, writers, readers, events);
        }

        private bool InitProducers(string[] producers)
        {
            var instances = producers.Select(v =>
                {
                    if (v == "test")
                    {
                        return (IProducer)(new TestProducer());
                    }
                    if (v == "bank")
                    {
                        return (IProducer)(new BankAccountProducer());
                    }
                    return null;
                }).Where(x => x != null)
                  .ToArray();

            Producers = instances;
            return instances.Any();
        }

        private bool Run(CommandProcessorContext context, int writers, int readers, int events)
        {
            context.IsAsync();

            var created = new AutoResetEvent(false);
            var done = new AutoResetEvent(false);

            var coordinator = new Coordinator(context, Producers, events,created, done);
            coordinator.Start();

            var verificationWorkers = new VerificationWorker[readers];
            for (int i = 0; i < verificationWorkers.Length; i++)
                verificationWorkers[i] = new VerificationWorker(string.Format("verifier {0}", i),
                                                                coordinator,
                                                                context.Client.Options.ReadWindow);
            var writerWrokers = new WriteWorker[writers];
            for (int i = 0; i < writerWrokers.Length; i++)
                writerWrokers[i] = new WriteWorker(string.Format("writer {0}", i),
                                                   coordinator,
                                                   context.Client.Options.WriteWindow);

            foreach (var worker in verificationWorkers)
                worker.Start();
            foreach (var worker in writerWrokers)
                worker.Start();

            created.WaitOne();
            done.WaitOne();

            foreach (var worker in verificationWorkers)
                worker.Stop();
            foreach (var worker in writerWrokers)
                worker.Stop();

            context.Success();
            return true;
        }
    }
}