#define TESTING

using System;
using System.Net;
using System.Security;
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
            var baseuri = new Uri("http://127.0.0.1:2113/");
            
            if (args.Length == 1)
            {
                Console.WriteLine("Server set to: " + args[0]);
                baseuri = new Uri(args[0]);
                return new Args(false, baseuri, GetValidatedNetworkCredential(baseuri));
            }
            Console.WriteLine("No server set defaulting to http://127.0.0.1:2113/");
            return new Args(true, baseuri, GetValidatedNetworkCredential(baseuri));
        }

        private static NetworkCredential GetValidatedNetworkCredential(Uri baseuri)
        {
            for (int i = 0; i < 5; i++)
            {
                Console.Write("username:");
                var username = Console.ReadLine();
                Console.Write("password:");
                var password = ReadPassword();

                var cred = new NetworkCredential(username, password);

                if(TryValidatePassword(baseuri, cred))
                {
                    return cred;
                } else
                {
                    Console.WriteLine("Invalid username/password");
                }
            }
            Environment.Exit(1);
            return null;
        }

        private static bool TryValidatePassword(Uri baseuri, NetworkCredential cred)
        {
            
            var request = WebRequest.Create(baseuri.AbsoluteUri +"projections/transient?enabled=yes");
            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = 0;
            request.Credentials = cred;
            try
            {
                using (var response = (HttpWebResponse) request.GetResponse())
                {
                    return response.StatusCode != HttpStatusCode.Unauthorized;
                }
            }
            catch (WebException ex)
            {
                var response = (HttpWebResponse)ex.Response;
                return response.StatusCode != HttpStatusCode.Unauthorized;
            }
        }


#if __MonoCS__
        //mono apparently doesnt work with secure strings.
        //TODO delete me when its no longer broken
        private static string ReadPassword()
        {
            var ret = "";
            while (true)
            {
                var info = Console.ReadKey(true);
                if (info.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    Console.WriteLine();
                    return ret;
                }
                if (info.Key == ConsoleKey.Backspace)
                {
                    if (ret.Length > 0)
                    {
                        ret = ret.Substring(0, ret.Length - 1);
                    }
                }
                else
                {
                    ret += info.KeyChar;
                }
            }
        }
#else
        private static SecureString ReadPassword()
        {
            var ret = new SecureString();
            while (true)
            {
                var info = Console.ReadKey(true);
                if (info.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    Console.WriteLine();
                    return ret;
                }
                if (info.Key == ConsoleKey.Backspace)
                {
                    if (ret.Length > 0)
                    {
                        ret.RemoveAt(ret.Length - 1);
                    }
                }
                else
                {
                    ret.AppendChar(info.KeyChar);
                }
            }
        }
#endif

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
            catch (InvalidOperationException)
            {
                return true;
            }
        }
    }
}
