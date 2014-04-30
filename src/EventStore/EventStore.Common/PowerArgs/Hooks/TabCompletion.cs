using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PowerArgs
{
    internal class REPLExitException : Exception {}
    internal class REPLContinueException : Exception { }

    /// <summary>
    /// A hook that takes over the command line and provides tab completion for known strings when the user presses
    /// the tab key.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class TabCompletion : ArgHook, ICommandLineArgumentsDefinitionMetadata
    {
        Type completionSource;

        /// <summary>
        /// When this indicator is the only argument the user specifies that triggers the hook to enhance the command prompt.  By default, the indicator is the empty string.
        /// </summary>
        public string Indicator { get; set; }

        /// <summary>
        /// If this is > 0 then PowerArgs will save this many previous executions of the command line to your application data folder.
        /// Users can then access the history by pressing arrow up or down from the enhanced command prompt.
        /// </summary>
        public int HistoryToSave { get; set; }

        /// <summary>
        /// The location of the history file name (AppData/PowerArgs/EXE_NAME.TabCompletionHistory.txt
        /// </summary>
        public string HistoryFileName { get; set; }

        /// <summary>
        /// The name of your program (leave null and PowerArgs will try to detect it automatically)
        /// </summary>
        public string ExeName { get; set; }

        /// <summary>
        /// If true, then you must use Args.InvokeAction or Args.InvokeMain instead of Args.Parse.  Your user
        /// will get an interactive prompt that loops until they specify the REPLExitIndicator.
        /// </summary>
        public bool REPL { get; set; }

        /// <summary>
        /// The string users can specify in order to exit the REPL (defaults to string.Empty)
        /// </summary>
        public string REPLExitIndicator { get; set; }

        /// <summary>
        /// The message to display to the user when the REPL starts.  The default is Type a command or '{{Indicator}}' to exit.
        /// You can customize this message and use {{Indicator}} for the placeholder for your exit indicator.
        /// </summary>
        public string REPLWelcomeMessage { get; set; }

        internal bool ShowREPLWelcome { get; set; }

        private string HistoryFileNameInternal
        {
            get
            {
                var exeName = ExeName ?? Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location);
                return HistoryFileName ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PowerArgs", exeName + ".TabCompletionHistory.txt");
            }
        }

        /// <summary>
        /// Creates a new tab completion hook.
        /// </summary>
        /// <param name="indicator">When this indicator is the only argument the user specifies that triggers the hook to enhance the command prompt.  By default, the indicator is the empty string.</param>
        public TabCompletion(string indicator = "")
        {
            this.Indicator = indicator;
            BeforeParsePriority = 100;
            HistoryToSave = 0;
            REPLExitIndicator = "quit";
            REPLWelcomeMessage = "Type a command or '{{Indicator}}' to exit.";
            ShowREPLWelcome = true;
        }

        /// <summary>
        /// Creates a new tab completion hook given a custom tab completion implementation.
        /// </summary>
        /// <param name="completionSource">A type that implements ITabCompletionSource such as SimpleTabCompletionSource</param>
        /// <param name="indicator">When this indicator is the only argument the user specifies that triggers the hook to enhance the command prompt.  By default, the indicator is the empty string.</param>
        public TabCompletion(Type completionSource, string indicator = "") : this(indicator)
        {
            if (completionSource.GetInterfaces().Contains(typeof(ITabCompletionSource)) == false)
            {
                throw new InvalidArgDefinitionException("Type " + completionSource + " does not implement " + typeof(ITabCompletionSource).Name);
            }

            this.completionSource = completionSource;
        }

        /// <summary>
        /// Before PowerArgs parses the args, this hook inspects the command line for the indicator and if found 
        /// takes over the command line and provides tab completion.
        /// </summary>
        /// <param name="context">The context used to inspect the command line arguments.</param>
        public override void BeforeParse(ArgHook.HookContext context)
        {
            if (Indicator == "" && context.CmdLineArgs.Length != 0) return;
            else if (Indicator != "" && (context.CmdLineArgs.Length != 1 || context.CmdLineArgs[0] != Indicator)) return;

            var existingColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            try
            {
                if (REPL && ShowREPLWelcome)
                {
                    Console.WriteLine();
                    var message = REPLWelcomeMessage.Replace("{{Indicator}}", REPLExitIndicator);
                    Console.WriteLine(message);
                    Console.WriteLine();
                    Console.Write(Indicator + "> ");
                    ShowREPLWelcome = false;
                }
                else if (REPL)
                {
                    Console.Write(Indicator + "> ");
                }
                else
                {

                    // This is a little hacky, but I could not find a better way to make the tab completion start on the same lime
                    // as the command line input
                    try
                    {
                        var lastLine = StdConsoleProvider.ReadALineOfConsoleOutput(Console.CursorTop - 1);
                        Console.CursorTop--;
                        Console.WriteLine(lastLine);
                        Console.CursorTop--;
                        Console.CursorLeft = lastLine.Length + 1;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine();
                        Console.Write(Indicator + "> ");
                    }
                }
            }
            finally
            {
                Console.ForegroundColor = existingColor;
            }

            List<string> completions = FindTabCompletions(context.Definition.Arguments, context.Definition.Actions);

            List<ITabCompletionSource> completionSources = new List<ITabCompletionSource>();

            if(this.completionSource != null) completionSources.Add((ITabCompletionSource)Activator.CreateInstance(this.completionSource));
            completionSources.Add(new EnumTabCompletionSource(context.Definition));
            completionSources.Add(new SimpleTabCompletionSource(completions) { MinCharsBeforeCyclingBegins = 0 });
            completionSources.Add(new FileSystemTabCompletionSource());
            
            string str = null;
            var newCommandLine = ConsoleHelper.ReadLine(ref str, LoadHistory(), new MultiTabCompletionSource(completionSources));

            if (REPL && newCommandLine.Length == 1 && string.Equals(newCommandLine[0], REPLExitIndicator, StringComparison.OrdinalIgnoreCase))
            {
                throw new REPLExitException();
            }

            if (REPL && newCommandLine.Length == 1 && newCommandLine[0] == "cls")
            {
                ConsoleHelper.ConsoleImpl.Clear();
                throw new REPLContinueException();
            }

            else if (REPL && newCommandLine.Length == 0 && string.IsNullOrWhiteSpace(REPLExitIndicator) == false)
            {
                throw new REPLContinueException();
            }

            context.CmdLineArgs = newCommandLine;
            AddToHistory(str);
        }

        /// <summary>
        /// Clears all history saved on disk
        /// </summary>
        public void ClearHistory()
        {
            if (File.Exists(HistoryFileNameInternal))
            {
                File.WriteAllText(HistoryFileNameInternal, "");
            }
        }

        private void AddToHistory(string item)
        {
            if (HistoryToSave == 0) return;
            List<string> history = File.ReadAllLines(HistoryFileNameInternal).ToList();
            history.Insert(0, item);
            history = history.Distinct().ToList();
            while (history.Count > HistoryToSave) history.RemoveAt(history.Count - 1);
            File.WriteAllLines(HistoryFileNameInternal, history.ToArray());
        }

        private List<string> LoadHistory()
        {
            if (HistoryToSave == 0) return new List<string>();

            if (Directory.Exists(Path.GetDirectoryName(HistoryFileNameInternal)) == false)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(HistoryFileNameInternal));
                return new List<string>();
            }
            else if (File.Exists(HistoryFileNameInternal) == false)
            {
                File.WriteAllLines(HistoryFileNameInternal, new string[0]);
                return new List<string>();
            }
            else
            {
                return File.ReadAllLines(HistoryFileNameInternal).ToList();
            }
        }

        private List<string> FindTabCompletions(List<CommandLineArgument> arguments, List<CommandLineAction> actions)
        {
            List<string> ret = new List<string>();

            var argIndicator = "-";

            foreach (var argument in arguments)
            {
                // TODO - It would be great to support all aliases in the completions
                ret.Add(argIndicator + argument.Aliases.First());
            }
        

            foreach (var action in actions)
            {
                var name = action.Aliases.First();

                if (name.EndsWith(Constants.ActionArgConventionSuffix))
                {
                    name = name.Substring(0, name.Length - Constants.ActionArgConventionSuffix.Length);
                }

                if (action.IgnoreCase)
                {
                    ret.Add(name.ToLower());
                }
                else
                {
                } ret.Add(name); 
                
                ret.AddRange(FindTabCompletions(action.Arguments, new List<CommandLineAction>()));
            }

            ret = ret.Distinct().ToList();
            return ret;
        }
    }
}
