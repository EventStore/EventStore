using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PowerArgs
{
    //public class ConsoleTable<T>
    //{
    //    public class ConsoleTableRow
    //    {
    //        public List<ConsoleTableCell> Cells { get; set; }
    //        public T Value { get; set; }

    //        public ConsoleTableRow()
    //        {
    //            Cells = new List<ConsoleTableCell>();
    //        }

    //        public override string ToString()
    //        {
    //            return string.Join("", Cells.Select(c => c.TextValue + Pad(c.Padding)));
    //        }

    //        private static string Pad(int amount)
    //        {
    //            string ret = "";
    //            for (int i = 0; i < amount; i++) ret += " ";
    //            return ret;
    //        }
    //    }

    //    public class ConsoleTableCell
    //    {
    //        public object OriginalValue { get; set; }
    //        public string TextValue { get; set; }
    //        public int Padding { get; set; }

    //        public override string ToString()
    //        {
    //            return TextValue + Pad(Padding);
    //        }

    //        private static string Pad(int amount)
    //        {
    //            string ret = "";
    //            for (int i = 0; i < amount; i++) ret += " ";
    //            return ret;
    //        }
    //    }

    //    public List<T> Items { get; private set; }

    //    public ConsoleTable()
    //    {
    //        Items = new List<T>();
    //    }

    //    public ConsoleTable(IEnumerable<T> items)
    //        : this()
    //    {
    //        Items.AddRange(items);
    //    }

    //    private List<ConsoleTableRow> FormatAsTable(params string[] columns)
    //    {
    //        List<string> headers = Items[0].GetType().GetProperties().Where(p => (columns.Length > 0 && columns.Contains(p.Name)) || columns.Length == 0).Select(p => p.Name).ToList();
    //        List<List<string>> values = Items.Select(o => o.GetType().GetProperties().Where(p => (columns.Length > 0 && columns.Contains(p.Name)) || columns.Length == 0).Select(p => p.GetValue(o, null) + "").ToList()).ToList();

    //        var rows = FormatAsTable(headers, values);
    //        for (int i = 0; i < Items.Count; i++)
    //        {
    //            rows[i + 1].Value = Items[i];
    //            for (int j = 0; j < headers.Count; j++)
    //            {
    //                rows[i + 1].Cells[j].OriginalValue = Items[i].GetType().GetProperty(headers[j]).GetValue(Items[i], null);
    //            }
    //        }

    //        return rows;

    //    }

    //    public void Iterate(Action<ConsoleTableRow, bool> action, params string[] columns)
    //    {
    //        bool first = true;
    //        foreach (ConsoleTableRow row in FormatAsTable(columns))
    //        {
    //            action(row, first);
    //            first = false;
    //        }
    //    }

    //    public override string ToString()
    //    {
    //        string ret = "";
    //        Iterate((row, isHeader) =>
    //        {
    //            ret += row.ToString();

    //        });

    //        return ret;
    //    }

    //    public static void Print<T>(IEnumerable<T> values, Func<T, ConsoleColor> colors = null, params string[] columns)
    //    {
    //        var temp = Console.ForegroundColor;
    //        try
    //        {
    //            new ConsoleTable<T>(values).Iterate((row, isHeader) =>
    //            {
    //                if (isHeader)
    //                {
    //                    Console.ForegroundColor = ConsoleColor.White;
    //                }
    //                else if (colors != null)
    //                {
    //                    Console.ForegroundColor = colors(row.Value);
    //                }
    //                else
    //                {
    //                    Console.ForegroundColor = temp;
    //                }

    //                Console.WriteLine(row);
    //            }, columns);
    //        }
    //        finally
    //        {
    //            Console.ForegroundColor = temp;
    //        }
    //    }

    //    public static List<ConsoleTableRow> FormatAsTable(List<string> headers, List<List<string>> rows)
    //    {
    //        List<ConsoleTableRow> ret = new List<ConsoleTableRow>();
    //        if (rows.Count == 0) return ret;

    //        Dictionary<int, int> maximums = new Dictionary<int, int>();

    //        for (int i = 0; i < headers.Count; i++) maximums.Add(i, headers[i].Length);
    //        for (int i = 0; i < headers.Count; i++)
    //        {
    //            foreach (var row in rows)
    //            {
    //                maximums[i] = Math.Max(maximums[i], row[i].Length);
    //            }
    //        }

    //        int buffer = 3;

    //        ConsoleTableRow currentRow = new ConsoleTableRow();

    //        for (int i = 0; i < headers.Count; i++)
    //        {
    //            ConsoleTableCell currentCell = new ConsoleTableCell();
    //            currentCell.Padding = maximums[i] + buffer - headers[i].Length;
    //            currentCell.TextValue = headers[i];
    //            currentRow.Cells.Add(currentCell);
    //        }

    //        ret.Add(currentRow);
    //        currentRow = new ConsoleTableRow();

    //        foreach (var row in rows)
    //        {
    //            for (int i = 0; i < headers.Count; i++)
    //            {
    //                ConsoleTableCell currentCell = new ConsoleTableCell();
    //                currentCell.Padding = maximums[i] + buffer - row[i].Length;
    //                currentCell.TextValue = row[i];
    //                currentRow.Cells.Add(currentCell);
    //            }
    //            ret.Add(currentRow);
    //            currentRow = new ConsoleTableRow();
    //        }

    //        return ret;
    //    }
    //}
}
