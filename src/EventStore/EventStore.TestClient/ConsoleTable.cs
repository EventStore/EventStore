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
using EventStore.Common.Utils;

namespace EventStore.TestClient
{
    public class ConsoleTable
    {
        private readonly string[] _header;
        private readonly int[] _columnWidths;
        private readonly List<string> _rows;

        public ConsoleTable(params string[] header)
        {
            Ensure.NotNull(header, "header");

            _header = header;
            _columnWidths = _header.Select(s => s.Length + 4).ToArray();
            _rows = new List<string>();

            AppendRow(_header);
        }

        public string CreateIndentedTable()
        {
            return string.Format("{0}{1}{0}", Environment.NewLine, CreateTable());
        }

        public string CreateTable()
        {
            var lineSeparator = RowSeparator(_columnWidths.Sum() + _columnWidths.Length + 1);

            var table = new StringBuilder();
            foreach (var row in _rows)
            {
                table.AppendLine(lineSeparator);
                table.AppendLine(row);
            }
            table.Append(lineSeparator);

            return table.ToString();
        }

        private string RowSeparator(int width)
        {
            return new string('-', width);
        }

        public void AppendRow(params string[] cells)
        {
            if (cells == null || cells.Length != _header.Length)
                return;

            var row = new StringBuilder();
            for (int i = 0; i < cells.Length; i++)
            {
                var format = "|{0," + _columnWidths[i] + "}";
                row.AppendFormat(format, cells[i]);
            }
            row.Append("|");

            _rows.Add(row.ToString());
        }
    }
}