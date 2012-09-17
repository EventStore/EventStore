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
using EventStore.Common.Log;
using EventStore.Common.Utils;

namespace EventStore.TestClient.Commands.DvuBasic
{
    public class Status
    {
        private readonly ILogger _log;

        public int ThreadId { get; private set; }
        public bool Success { get; private set; }

        public Status(ILogger log)
        {
            Ensure.NotNull(log, "log");
            _log = log;
        }

        public void ReportWritesProgress(int threadId,
                                         int sent,
                                         int prepareTimeouts,
                                         int commitTimeouts,
                                         int forwardTimeouts,
                                         int wrongExpctdVersions,
                                         int streamsDeleted,
                                         int fails,
                                         int requests)
        {
            var sentP = ToPercent(sent, requests);
            var failsP = ToPercent(fails, sent);

            var table = new ConsoleTable("WRITER ID", "Completed %", "Completed/Total",
                                         "Failed %", "Failed/Sent", "Prepare Timeouts",
                                         "Commit Timeouts", "Forward Timeouts",
                                         "Wrong Versions", "Stream Deleted");
            table.AppendRow(threadId.ToString(), string.Format("{0:0.0}%", sentP), string.Format("{0}/{1}", sent, requests),
                            string.Format("{0:0.0}%", failsP), string.Format("{0}/{1}", fails, sent), prepareTimeouts.ToString(),
                            commitTimeouts.ToString(), forwardTimeouts.ToString(),
                            wrongExpctdVersions.ToString(), streamsDeleted.ToString());

            if (failsP > 50d)
                _log.Fatal(table.CreateIndentedTable());
            else
                _log.Info(table.CreateIndentedTable());
        }

        public void ReportReadsProgress(int threadId, int successes, int fails)
        {
            var all = successes + fails;

            var table = new ConsoleTable("READER ID", "Fails", "Total Random Reads");
            table.AppendRow(threadId.ToString(), fails.ToString(), all.ToString());

            if (fails != 0)
                _log.Fatal(table.CreateIndentedTable());
            else
                _log.Info(table.CreateIndentedTable());
        }

        public void ReportReadError(int threadId, string stream, int indx)
        {
            _log.Fatal("FATAL : READER [{0}] encountered an error in {1} ({2})", threadId, stream, indx);
        }

        public void ReportNotFoundOnRead(int threadId, string stream, int indx)
        {
            _log.Fatal("FATAL : READER [{0}] asked for event {1} in '{2}' but server returned 'Not Found'", threadId,
                       indx, stream);
        }

        public void FinilizeStatus(int threadId, bool success)
        {
            ThreadId = threadId;
            Success = success;
        }

        private double ToPercent(int value, int max)
        {
            var approx = (value / (double)max) * 100d;
            return approx <= 100d ? approx : 100;
        }
    }
}