using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Syndication;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace esquery
{
    class CommandProcessor
    {
        static string GetFirst(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var next = s.IndexOf(char.IsWhiteSpace);
            if (next < 0) return null;
            return s.Substring(0, next);
        }

        static List<string> EatFirstN(int n, string s)
        {
            var ret = new List<string>();
            var current = 0;
            for (int i = 0; i < n;i++)
            {
                var next = s.IndexOfAfter(current, char.IsWhiteSpace);
                if(next < 0) { return null; }
                ret.Add(s.Substring(current, next - current));
                current = next + 1;
            }
            //get whatever else is on the line.
            ret.Add(s.Substring(current, s.Length - current));
            return ret;
        }

        public static object Process(string command, State state)
        {
            var c = GetFirst(command);
            if (c == null) return new HelpCommandResult();
            try
            {
                switch (c)
                {
                    case "a":
                    case "append":
                        var append = EatFirstN(3, command);
                        if (append.Count != 4) return new InvalidCommandResult(command);
                        return Append(append[1], append[2], append[3]);
                    case "h":
                    case "help":
                        return new HelpCommandResult();
                    case "q":
                    case "query":
                        var query = EatFirstN(1, command);
                        if (query.Count != 2) return new InvalidCommandResult(command);
                        return CreateAndRunQuery(new Uri("http://127.0.0.1:2113"), query[1], state.Args.Credentials);
                    default:
                        return new InvalidCommandResult(command);
                }
            }
            catch
            {
                return new InvalidCommandResult(command);
            }
        }

        private static Uri PostQuery(Uri baseUri, string query, NetworkCredential credential)
        {
            var request = WebRequest.Create("http://127.0.0.1:2113/projections/transient?enabled=yes");
            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = query.Length;
            //TODO take from args
            request.Credentials = credential;
            using (var sw = new StreamWriter(request.GetRequestStream()))
            {
                sw.Write(query);
            }
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                var s = new StreamReader(response.GetResponseStream()).ReadToEnd();
                
                if(response.StatusCode != HttpStatusCode.Created)
                {
                    throw new Exception("Query Failed with Status Code: " + response.StatusCode + "\n" + s);
                }
                return new Uri(response.Headers["Location"]);
            }
        }

        private static QueryInformation CheckQueryStatus(Uri toCheck, NetworkCredential credential)
        {
            var request = (HttpWebRequest) WebRequest.Create(toCheck);
            request.Method = "GET";
            request.Accept = "application/json";
            //TODO take from args
            request.Credentials = credential;
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                var s = new StreamReader(response.GetResponseStream()).ReadToEnd();
                JObject json = JObject.Parse(s);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception("Query Polling Failed with Status Code: " + response.StatusCode + "\n" + s);
                }
                var faulted = json["status"].Value<string>().StartsWith( "Faulted");
                var completed = json["status"].Value<string>().StartsWith("Completed");   
                var faultReason = json["stateReason"].Value<string>();
                var resulturi = new Uri(json["resultStreamUrl"].Value<string>());
                var progress = json["progress"].Value<decimal>();
               
                return new QueryInformation() {
                    Faulted = faulted, 
                    FaultReason = faultReason, 
                    ResultUri = resulturi, 
                    Progress=progress, 
                    Completed=completed
                };
            }
        }

        private static Uri GetNamedLink(JObject feed, string name)
        {
            JToken token;
            var links = feed["links"];
            if (links == null) return null;
            return (from item in links
                    where item["relation"].Value<string>() == name
                    select new Uri(item["uri"].Value<string>())).FirstOrDefault();
        }

        private static Uri GetLast(Uri head, NetworkCredential credential)
        {
            var request = (HttpWebRequest)WebRequest.Create(head);
            request.Credentials = credential;
            request.Accept = "application/vnd.eventstore.atom+json";

            using (var response = (HttpWebResponse)request.GetResponse())
            {

                var json = JObject.Parse(new StreamReader(response.GetResponseStream()).ReadToEnd());
                var last = GetNamedLink(json, "last");
                return last ?? GetNamedLink(json, "self");
            }
        }

        static Uri ReadResults(Uri uri, NetworkCredential credential)
        {
            var request = (HttpWebRequest) WebRequest.Create(uri);
            request.Credentials = credential;
            request.Accept = "application/vnd.eventstore.atom+json";
            using (var response = request.GetResponse())
            {
                var json = JObject.Parse(new StreamReader(response.GetResponseStream()).ReadToEnd());

                foreach (var item in json["entries"])
                {
                    Console.WriteLine(item["data"].ToString());
                }
                return GetNamedLink(json, "previous") ?? uri;
            }
        }

        private static void WriteItem(SyndicationItem item)
        {
            //TODO Print Result
            Console.WriteLine(item.Content);
        }

        private static object CreateAndRunQuery(Uri baseUri, string query, NetworkCredential credential)
        {
            try
            {
                var watch = new Stopwatch();
                watch.Start();
                var toCheck = PostQuery(baseUri, query, credential);
                var queryInformation = new QueryInformation();
                while (!queryInformation.Completed)
                {
                    queryInformation = CheckQueryStatus(toCheck, credential);
                    if (queryInformation.Faulted)
                    {
                        throw new Exception("Query Faulted.\n" + queryInformation.FaultReason);
                    }
                    Console.Write("\r{0}", queryInformation.Progress.ToString("f2") + "%");
                    Thread.Sleep(500);
                }
                Console.WriteLine("\rQuery Completed in: " + watch.Elapsed);
                var last = GetLast(queryInformation.ResultUri, credential);
                last = new Uri(last.OriginalString + "?embed=body");
                var next = ReadResults(last, credential);
                return new QueryResult() {Query = query};
            }
            catch(Exception ex)
            {
                return new ErrorResult(ex);
            }
        }

        private static AppendResult Append(string stream, string eventType, string data)
        {
            var message = "[{'eventType':'" + eventType + "', 'eventId' :'" + Guid.NewGuid() + "', 'data' : " + data +"}]";
            var request = WebRequest.Create("http://127.0.0.1:2113/streams/" + stream);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = message.Length;
            using (var sw = new StreamWriter(request.GetRequestStream()))
            {
                sw.Write(message);
            }
            using (var response = (HttpWebResponse) request.GetResponse())
            {
                return new AppendResult() {StatusCode = response.StatusCode};
            }
        }

        private class AppendResult
        {
            public HttpStatusCode StatusCode;

            public override string ToString()
            {
                if (StatusCode == HttpStatusCode.Created)
                {
                    return "Succeeded.";
                }
                return StatusCode.ToString();
            }
        }

        private class QueryResult
        {
            public string Query;

            public override string ToString()
            {
                return "Query Completed";
            }
        }
    }

    class QueryInformation
    {
        public bool Faulted;
        public string FaultReason;
        public decimal Progress;
        public Uri ResultUri;
        public bool Completed;
    }

    class ErrorResult 
    {
        private Exception _exception;

        public ErrorResult(Exception exception)
        {
            _exception = exception;
        }

        public override string ToString()
        {
            return "An error has occured\n" + _exception.Message;
        }
    }

    class HelpCommandResult
    {
        public override string ToString()
        {
            return "esquery help:\n" + 
                   "\th/help: prints help\n" + 
                   "\tq/query {js query} executes a query.\n" + 
                   "\ta/append {stream} {js object}: appends to a stream.\n";
        }
    }

    class InvalidCommandResult
    {
        private string _command;

        public InvalidCommandResult(string command)
        {
            _command = command;
        }

        public override string ToString()
        {
            return "Invalid command: '" + _command + "'";
        }
    }

    static class IEnumerableExtensions
    {
        public static int IndexOf<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            int i = 0;

            foreach (var element in source)
            {
                if (predicate(element))
                    return i;

                i++;
            }
            return i;
        }

        public static int IndexOfAfter<TSource>(this IEnumerable<TSource> source, int start, Func<TSource, bool> predicate)
        {
            int i = 0;

            foreach (var element in source)
            {
                if (predicate(element) && i >= start)
                    return i;

                i++;
            }
            return i;
        }
    }
}