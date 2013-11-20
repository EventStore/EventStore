using System;
using System.Net;
using System.Text;

namespace esquery
{
    class Program
    {
        static void Loop(Func<State,State> func, State initial)
        {
            var state = initial;
            while (true)
            {
                state = func(state);
                if (state.Exit) break;
            }
        }

        static State Eval(State state)
        {
            var str = state.Read;
            if(state.Read == null) { state.Exit = true;}
            var builder = state.Current;
            if (!string.IsNullOrEmpty(str))
            {
                builder.AppendLine(str);
                state.Evaled = null;
                return state;
            }
            
            var command = builder.ToString();
            builder.Clear();
            state.Evaled = CommandProcessor.Process(command, state);
            return state;
        }

        static State Read(State state)
        {
            if(state.Current.Length == 0)
                Console.Write("es:> ");
            string read = null;
            if (!state.Piped || Console.In.Peek() != -1)
            {
                read = Console.ReadLine();
            }
            if(state.Piped && read != null)
                Console.WriteLine(read);
            state.Read = read; 
            return state;
        }

        static State Print(State state)
        {
            if(state.Evaled != null)
                  Console.WriteLine("\n" + state.Evaled);
            return state;
        }
        
        private static Args ReadArgs(string[] args)
        {
            if (args.Length == 1)
            {
                Console.WriteLine("Server set to: " + args[0]);
                return new Args(false, new Uri(args[0]), new NetworkCredential("admin", "changeit"));
            }
            Console.WriteLine("No server set defaulting to http://127.0.0.1:2113/");
            return new Args(true, new Uri("http://127.0.0.1:2113/"), new NetworkCredential("admin", "changeit"));
        }

        static void Main(string[] args)
        {
            Loop(s =>Print(Eval(Read(s))), new State() { Args = ReadArgs(args), Piped = ConsoleHelper.IsPiped() });
        }
    }

    class State
    {
        public StringBuilder Current = new StringBuilder();
        public Args Args;
        public bool Exit;
        public string Read;
        public object Evaled;
        public bool Piped;
    }

    class Args
    {
        public readonly bool KeepRunning;
        public NetworkCredential Credentials;
        public Uri BaseUri;

        public Args(bool keepRunning, Uri baseUri, NetworkCredential credentials)
        {
            BaseUri = baseUri;
            KeepRunning = keepRunning;
            Credentials = credentials;
        }
    }
    class ConsoleHelper
    {

        public static bool IsPiped()
        {
            try
            {
                var nothing = Console.KeyAvailable;
                return false;
            }
            catch (InvalidOperationException expected)
            {
                return true;
            }
        }
    }
}
