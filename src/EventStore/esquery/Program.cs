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

        static bool IsPiped()
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
        static Tuple<object, State> Eval(Tuple<string, State> data)
        {
            var str = data.Item1;
            if(str == null) { data.Item2.Exit = true;}
            var builder = data.Item2.Current;
            if (!string.IsNullOrEmpty(str))
            {
                builder.AppendLine(str);
                return new Tuple<object, State>(null, data.Item2);
            }
            
            var command = builder.ToString();
            builder.Clear();
            return new Tuple<object, State>(CommandProcessor.Process(command, data.Item2), data.Item2);
        }

        static Tuple<string, State> Read(State state)
        {
            var piped = IsPiped();
            if(state.Current.Length == 0)
                Console.Write("es:> ");
            var read = Console.ReadLine();
            if(piped && read != null)
                Console.WriteLine(read);
            if (read == null && piped) return new Tuple<string, State>(null, state);
            return new Tuple<string, State>(read, state);
        }

        static Tuple<object, State> Print(Tuple<object,State> data)
        {
            if(data.Item1 != null)
                  Console.WriteLine("\n" + data.Item1);
            return data;
        }
        
        private static Args ReadArgs(string[] args)
        {
            if (args.Length == 1)
            {
                Console.WriteLine("Server set to: " + args[0]);
                return new Args(false, args[0], new NetworkCredential("admin", "changeit"));
            }
            Console.WriteLine("No server set defaulting to http://127.0.0.1:2113/");
            return new Args(true, "http://127.0.0.1:2113/", new NetworkCredential("admin", "changeit"));
        }

        static void Main(string[] args)
        {
            Loop(s =>Print(Eval(Read(s))).Item2, new State() { Args = ReadArgs(args) });
        }
    }

    class State
    {
        public StringBuilder Current = new StringBuilder();
        public Args Args;
        public bool Exit;
    }

    class Args
    {
        public readonly bool KeepRunning;
        public NetworkCredential Credentials;

        public Args(bool keepRunning, string http, NetworkCredential credentials)
        {
            KeepRunning = keepRunning;
            Credentials = credentials;
        }
    }

}
