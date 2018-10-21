using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EventStore.Core.Util
{
    /// <summary>
    /// This class can answer the question, if a given string is a member of a list of strings. 
    /// 
    /// Internally it uses different strategies based on the number of strings it has to compare. 
    /// </summary>
    public class StringFilter
    {
        private interface IFilterStrategy
        {
            bool IsStringAllowed(string s);
        }

        private readonly IFilterStrategy _strategy;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:EventStore.Core.Util.StringFilter"/> class.
        /// </summary>
        /// <param name="allowedStrings">Allowed strings. If null or empty, all strings are considered valid by <see cref="IsStringAllowed(string)"/></param>
        public StringFilter(IReadOnlyList<string> allowedStrings)
        {
            if (allowedStrings == null || allowedStrings.Count == 0)
            {
                _strategy = new AlwaysAllowStrategy();
            }
            else
            {
                var plainStrings = new List<string>();
                var regexes = new List<Regex>();

                foreach (var str in allowedStrings)
                {
                    if (RegexCollectionStrategy.IsRegexString(str))
                    {
                        regexes.Add(new Regex(str, RegexOptions.Compiled));
                    }
                    else
                    {
                        plainStrings.Add(str);
                    }
                }
                var strategies = new List<IFilterStrategy>();

                if (plainStrings.Count == 1)
                {
                    strategies.Add(new SingleStringStrategy(plainStrings[0]));
                }
                else if (plainStrings.Count > 1)
                {
                    strategies.Add(new PlainStringCollectionStrategy(plainStrings));
                }

                if (regexes.Count != 0)
                {
                    strategies.Add(new RegexCollectionStrategy(regexes));
                }

                _strategy = new MultiStrategyStrategy(strategies);
            }
        }

        /// <summary>
        /// Returns true, if the given string is part of the list of strings passed to the constructor. 
        /// 
        /// If the constructor was given an empty list, all strings are considered to be allowed. 
        /// </summary>
        /// <returns><c>true</c>, if string is allowed, <c>false</c> otherwise.</returns>
        /// <param name="s">String to check</param>
        public bool IsStringAllowed(string s)
        {
            return _strategy.IsStringAllowed(s);
        }

        private class AlwaysAllowStrategy : IFilterStrategy
        {
            public bool IsStringAllowed(string s)
            {
                return true;
            }
        }

        private class SingleStringStrategy : IFilterStrategy
        {
            private readonly string _expectedString;

            public SingleStringStrategy(string expectedString)
            {
                _expectedString = expectedString;
            }

            public bool IsStringAllowed(string s)
            {
                return _expectedString.Equals(s);
            }
        }

        private class PlainStringCollectionStrategy : IFilterStrategy
        {
            private readonly Regex _allowedStringsRegex;

            public PlainStringCollectionStrategy(IEnumerable<string> allowedStrings)
            {
                var filters = allowedStrings.Select(Regex.Escape);
                _allowedStringsRegex = new Regex("^" + string.Join("|", filters) + "$", RegexOptions.Compiled);
            }

            public bool IsStringAllowed(string s)
            {
                return _allowedStringsRegex.IsMatch(s);
            }
        }

        private class RegexCollectionStrategy : IFilterStrategy
        {
            private readonly List<Regex> regexes;

            public RegexCollectionStrategy(List<Regex> regexes)
            {
                this.regexes = regexes;
            }

            public static bool IsRegexString(string s)
            {
                return !s.StartsWith("$", System.StringComparison.Ordinal) && !s.Equals(Regex.Escape(s));
            }

            public bool IsStringAllowed(string s)
            {
                foreach(var regex in regexes)
                {
                    if(regex.IsMatch(s))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private class MultiStrategyStrategy : IFilterStrategy
        {
            private readonly List<IFilterStrategy> strategies;

            public MultiStrategyStrategy(List<IFilterStrategy> strategies)
            {
                this.strategies = strategies;
            }

            public bool IsStringAllowed(string s)
            {
                foreach(var strat in strategies)
                {
                    if (strat.IsStringAllowed(s))
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
