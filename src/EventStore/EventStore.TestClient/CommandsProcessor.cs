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
using System.Text;
using System.Threading;
using EventStore.Common.Log;

namespace EventStore.TestClient
{
    public class CommandsProcessor
    {
        public IEnumerable<ICmdProcessor> RegisteredProcessors
        {
            get { return _processors.Values; }
        }

        private readonly ILogger _log;
        private readonly IDictionary<string, ICmdProcessor> _processors = new Dictionary<string, ICmdProcessor>();
        private ICmdProcessor _regCommandsProcessor;

        public CommandsProcessor(ILogger log)
        {
            _log = log;
        }

        public void Register(ICmdProcessor processor, bool usageProcessor = false)
        {
            var cmd = processor.Keyword.ToUpper();

            if (_processors.ContainsKey(cmd))
                throw new InvalidOperationException(
                    string.Format("The processor for command '{0}' is already registered.", cmd));

            _processors[cmd] = processor;

            if (usageProcessor)
                _regCommandsProcessor = processor;
        }

        public bool TryProcess(CommandProcessorContext context, string[] args, out int exitCode)
        {
            var commandName = args[0].ToUpper();
            var commandArgs = args.Skip(1).ToArray();

            ICmdProcessor commandProcessor;
            if (!_processors.TryGetValue(commandName, out commandProcessor))
            {
                _log.Info("Unknown command: {0}.", commandName);
                if (_regCommandsProcessor != null)
                    _regCommandsProcessor.Execute(context, new string[0]);
                exitCode = 1;
                return false;
            }

            int exitC = 0;
            var executedEvent = new AutoResetEvent(false);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var syntaxOk = commandProcessor.Execute(context, commandArgs);
                    if (syntaxOk)
                    {
                        exitC = context.ExitCode;
                    }
                    else
                    {
                        exitC = 1;
                        _log.Info("Usage of {0}:{1}{2}", commandName, Environment.NewLine, commandProcessor.Usage);
                    }
                    executedEvent.Set();
                }
                catch (Exception exc)
                {
                    _log.ErrorException(exc, "Error while executing command {0}.", commandName);
                    exitC = -1;
                    executedEvent.Set();
                }
            });

            executedEvent.WaitOne(1000);
            context.WaitForCompletion();

            if (!string.IsNullOrWhiteSpace(context.Reason))
                _log.Error("Error during execution of command: {0}.", context.Reason);
            if (context.Error != null)
            {
                _log.ErrorException(context.Error, "Error during execution of command");

                var details = new StringBuilder();
                BuildFullException(context.Error, details);
                _log.Error("Details: {0}", details.ToString());
            }
            
            exitCode = exitC == 0 ? context.ExitCode : exitC;
            return true;
        }

        private static void BuildFullException(Exception ex, StringBuilder details, int level = 0)
        {
            const int maxLevel = 3;

            if (details == null)
                throw new ArgumentNullException("details");

            if (level > maxLevel)
                return;

            while (ex != null)
            {
                details.AppendFormat("\n{0}-->{1}", new string(' ', level * 2), ex.Message);

                var aggregated = ex as AggregateException;
                if (aggregated != null && aggregated.InnerExceptions != null)
                {
                    if (level > maxLevel)
                        break;

                    foreach (var innerException in aggregated.InnerExceptions.Take(2))
                        BuildFullException(innerException, details, level + 1);
                }
                else
                    ex = ex.InnerException;

                level += 1;
            }
        }
    }
}