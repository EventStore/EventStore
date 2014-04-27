using System;
using System.Collections.Generic;
using System.Linq;

namespace PowerArgs
{
    /// <summary>
    /// Used for internal implementation, but marked public for testing, please do not use.
    /// </summary>
    public static class ConsoleHelper
    {


        /// <summary>
        /// Used for internal implementation, but marked public for testing, please do not use.
        /// </summary>
        public static IConsoleProvider ConsoleImpl = new StdConsoleProvider();



        private static string[] GetArgs(List<char> chars)
        {
            List<string> ret = new List<string>();

            bool inQuotes = false;
            string token = "";
            for (int i = 0; i < chars.Count; i++)
            {
                char c = chars[i];
                if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    ret.Add(token);
                    token = "";
                }
                else if (c == '\\' && i < chars.Count - 1 && chars[i + 1] == '"')
                {
                    token += '"';
                    i++;
                }
                else if (c == '"')
                {
                    if (!inQuotes)
                    {
                        inQuotes = true;
                    }
                    else
                    {
                        ret.Add(token);
                        token = "";
                        inQuotes = false;
                    }
                }
                else
                {
                    token += c;
                }
            }

            ret.Add(token);

            return (from t in ret where string.IsNullOrWhiteSpace(t) == false select t.Trim()).ToArray();
        }

        private static void RefreshConsole(int leftStart, int topStart, List<char> chars, int offset = 0, int lookAhead = 1)
        {
            int left = ConsoleImpl.CursorLeft;
            ConsoleImpl.CursorLeft = leftStart;
            ConsoleImpl.CursorTop = topStart;
            for (int i = 0; i < chars.Count; i++) ConsoleImpl.Write(chars[i]);
            for(int i = 0; i < lookAhead; i++) ConsoleImpl.Write(" ");
            ConsoleImpl.CursorTop = topStart + (int)Math.Floor((leftStart + chars.Count) / (double)ConsoleImpl.BufferWidth);
            ConsoleImpl.CursorLeft = (leftStart + chars.Count) % ConsoleImpl.BufferWidth;
        }

        private enum QuoteStatus
        {
            OpenedQuote,
            ClosedQuote,
            NoQuotes
        }

        private static QuoteStatus GetQuoteStatus(List<char> chars, int startPosition)
        {
            bool open = false;

            for (int i = 0; i <= startPosition; i++)
            {
                var c = chars[i];
                if (i > 0 && c == '"' && chars[i - 1] == '\\')
                {
                    // escaped
                }
                else if (c == '"')
                {
                    open = !open;
                }
            }

            if (open) return QuoteStatus.OpenedQuote;

            var charsAsString = new string(chars.ToArray());

            if (chars.LastIndexOf('"') > chars.LastIndexOf(' ')) return QuoteStatus.ClosedQuote;

            return QuoteStatus.NoQuotes;

        }

        private static List<string> Tokenize(string chars)
        {
            bool open = false;

            List<string> ret = new List<string>();
            string currentToken = "";

            for (int i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (i > 0 && c == '"' && chars[i - 1] == '\\')
                {

                }
                else if (c == '"')
                {
                    open = !open;
                    if (!open)
                    {
                        ret.Add(currentToken);
                        currentToken = "";
                        continue;
                    }
                }

                if (c != ' ' || open)
                {
                    currentToken += c;
                }
                else if (c == ' ')
                {
                    ret.Add(currentToken);
                    currentToken = "";
                }
            }

            if (open) return null;

            return ret.Where(token => string.IsNullOrWhiteSpace(token) == false).ToList();

        }

        internal static string[] ReadLine(ref string rawInput, List<string> history, params ITabCompletionSource[] tabCompletionHooks)
        {
            var leftStart = ConsoleImpl.CursorLeft;
            var topStart = ConsoleImpl.CursorTop;
            var chars = new List<char>();

            int historyIndex = -1;
            history = history ?? new List<string>();

            while (true)
            {
                var info = ConsoleImpl.ReadKey();
                int i = ConsoleImpl.CursorLeft - leftStart + (ConsoleImpl.CursorTop - topStart) * ConsoleImpl.BufferWidth;

                if (info.Key == ConsoleKey.Home)
                {
                    ConsoleImpl.CursorTop = topStart;
                    ConsoleImpl.CursorLeft = leftStart;
                    continue;
                }
                else if (info.Key == ConsoleKey.End)
                {
                    ConsoleImpl.CursorTop = topStart + (int)(Math.Floor((leftStart + chars.Count) / (double)ConsoleImpl.BufferWidth));
                    ConsoleImpl.CursorLeft = (leftStart + chars.Count) % ConsoleImpl.BufferWidth;
                    continue;
                }
                else if (info.Key == ConsoleKey.UpArrow)
                {
                    if (history.Count == 0) continue;
                    ConsoleImpl.CursorLeft = leftStart;
                    historyIndex++;
                    if (historyIndex >= history.Count) historyIndex = 0;
                    chars = history[historyIndex].ToList();
                    RefreshConsole(leftStart, topStart, chars, chars.Count, lookAhead: 80);
                    continue;
                }
                else if (info.Key == ConsoleKey.DownArrow)
                {
                    if (history.Count == 0) continue;
                    ConsoleImpl.CursorLeft = leftStart;
                    historyIndex--;
                    if (historyIndex < 0) historyIndex = history.Count - 1;
                    chars = history[historyIndex].ToList();
                    RefreshConsole(leftStart, topStart, chars, chars.Count, lookAhead: 80);
                    continue;
                }
                else if (info.Key == ConsoleKey.LeftArrow)
                {
                    ConsoleImpl.CursorLeft = Math.Max(leftStart, ConsoleImpl.CursorLeft - 1);
                    continue;
                }
                else if (info.Key == ConsoleKey.RightArrow)
                {
                    ConsoleImpl.CursorLeft = Math.Min(ConsoleImpl.CursorLeft + 1, leftStart + chars.Count);
                    continue;
                }
                else if (info.Key == ConsoleKey.Delete)
                {
                    if (i < chars.Count)
                    {
                        chars.RemoveAt(i);
                        RefreshConsole(leftStart, topStart, chars);
                    }
                    continue;
                }
                else if (info.Key == ConsoleKey.Backspace)
                {
                    if (i == 0) continue;
                    i--;
                    ConsoleImpl.CursorLeft = ConsoleImpl.CursorLeft - 1;
                    if (i < chars.Count)
                    {
                        chars.RemoveAt(i);
                        RefreshConsole(leftStart, topStart, chars);
                    }
                    continue;
                }
                else if (info.Key == ConsoleKey.Enter)
                {
                    ConsoleImpl.WriteLine();
                    break;
                }
                else if (info.Key == ConsoleKey.Tab)
                {
                    var token = "";
                    var quoteStatus = GetQuoteStatus(chars, i - 1);
                    bool readyToEnd = quoteStatus != QuoteStatus.ClosedQuote;
                    var endTarget = quoteStatus == QuoteStatus.NoQuotes ? ' ' : '"';

                    int j;
                    for (j = i - 1; j >= 0; j--)
                    {
                        if (chars[j] == endTarget && readyToEnd)
                        {
                            if (endTarget == ' ')
                            {
                                j++;
                            }
                            else
                            {
                                token += chars[j];
                            }

                            break;
                        }
                        else if (chars[j] == endTarget)
                        {
                            token += chars[j];
                            readyToEnd = true;
                        }
                        else
                        {
                            token += chars[j];
                        }
                    }

                    if (j == -1) j = 0;

                    var context = "";
                    for (int k = j - 1; k >= 0; k--)
                    {
                        context += chars[k];
                    }
                    context = new string(context.Reverse().ToArray());
                    context = ParseContext(context);

                    token = new string(token.Reverse().ToArray());

                    string completion = null;

                    foreach (var completionSource in tabCompletionHooks)
                    {
                        if (completionSource is ITabCompletionSourceWithContext)
                        {
                            if (((ITabCompletionSourceWithContext)completionSource).TryComplete(info.Modifiers.HasFlag(ConsoleModifiers.Shift), context, token, out completion)) break;
                        }
                        else
                        {
                            if (completionSource.TryComplete(info.Modifiers.HasFlag(ConsoleModifiers.Shift), token, out completion)) break;
                        }
                    }

                    if (completion == null) continue;

                    if (completion.Contains(" ") && completion.StartsWith("\"") == false && completion.EndsWith("\"") == false)
                    {
                        completion = '"' + completion + '"';
                    }

                    var insertThreshold = j + token.Length;

                    for (int k = 0; k < completion.Length; k++)
                    {
                        if (k + j == chars.Count)
                        {
                            chars.Add(completion[k]);
                        }
                        else if (k + j < insertThreshold)
                        {
                            chars[k + j] = completion[k];
                        }
                        else
                        {
                            chars.Insert(k + j, completion[k]);
                        }
                    }

                    // Handle the case where the completion is smaller than the token
                    int extraChars = token.Length - completion.Length;
                    for (int k = 0; k < extraChars; k++)
                    {
                        chars.RemoveAt(j + completion.Length);
                    }

                    RefreshConsole(leftStart, topStart, chars, completion.Length - token.Length, extraChars);
                }
                else
                {
                    if (i == chars.Count)
                    {
                        chars.Add(info.KeyChar);
                        ConsoleImpl.Write(info.KeyChar);
                    }
                    else
                    {
                        chars.Insert(i, info.KeyChar);
                        RefreshConsole(leftStart, topStart, chars, 1);
                    }
                    continue;
                }
            }

            rawInput = new string(chars.ToArray());

            return GetArgs(chars);
        }

        /// <summary>
        /// The input is the full command line previous to the token to be completed.  This function 
        /// pulls out the last token before the completion's 'so far' input.
        /// </summary>
        /// <param name="commandLine"></param>
        /// <returns></returns>
        private static string ParseContext(string commandLine)
        {
            var tokens = ConsoleHelper.Tokenize(commandLine);
            if (tokens == null) return "";
            else if (tokens.Count == 0) return "";
            else return tokens.Last();
        }
    }
}
