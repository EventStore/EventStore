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
                                     "<threads, default = 50> " +
                                     "<tasks, default = 100000> " +
                                     "<producers, default = [test], available = [{1}]>",
                                     Keyword,
                                     String.Join(",", _availableProducers));
            }
        }

        private IEnumerable<string> _availableProducers
        {
            get
            {
                yield return "test";
                yield return "bank";
            }
        }
        private IProducer[] _producers;

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            var threads = 50;
            var tasks = 100000;
            var producers = new[] {"test"};

            if (args.Length != 0 && args.Length != 3)
            {
                context.Log.Error("Invalid number of arguments. Should be 0 or 3");
                return false;
            }

            if (args.Length > 0)
            {
                int threadsArg;
                if (int.TryParse(args[0], out threadsArg))
                {
                    threads = threadsArg;
                    int tasksArg;
                    if (int.TryParse(args[1], out tasksArg))
                    {
                        tasks = tasksArg;
                        string[] producersArg;
                        if ((producersArg = args[2].Split(new[] {","}, StringSplitOptions.RemoveEmptyEntries)).Length > 0)
                        {
                            producersArg = producersArg.Select(p => p.Trim().ToLower()).Distinct().ToArray();
                            if (producersArg.Any(p => !_availableProducers.Contains(p)))
                            {
                                context.Log.Error(
                                    "Invalid producers argument. Pass comma-separated subset of [{0}]",
                                    String.Join(",", _availableProducers));
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
                        context.Log.Error("Invalid argument value for <tasks>");
                        return false;
                    }
                }
                else
                {
                    context.Log.Error("Invalid argument value for <threads>");
                    return false;
                }
            }

            return InitProducers(producers) && Run(context, threads, tasks);
        }

        private bool InitProducers(IEnumerable<string> producers)
        {
            var instances = producers
                .Select(s =>
                            {
                                if (s == "test")
                                {
                                    return (IProducer) (new TestProducer());
                                }
                                if (s == "bank")
                                {
                                    return (IProducer) (new BankAccountProducer());
                                }

                                return null;
                            })
                .Where(x => x != null).ToArray();

            _producers = instances;
            return instances.Any();
        }

        private bool Run(CommandProcessorContext context, int threads, int tasks)
        {
            context.IsAsync();

            var done = new AutoResetEvent(false);

            var coordinator = new Coordinator(context, _producers, tasks, done);
            coordinator.Start();

            var workers = new Worker[threads];

            for (var i = 0; i < workers.Length; i++)
                workers[i] = new Worker(i.ToString(), coordinator, context.Client.Options.WriteWindow);
            foreach (var worker in workers)
                worker.Start();

            done.WaitOne();

            foreach (var worker in workers)
                worker.Stop();

            context.Success();
            return true;
        }
    }
}