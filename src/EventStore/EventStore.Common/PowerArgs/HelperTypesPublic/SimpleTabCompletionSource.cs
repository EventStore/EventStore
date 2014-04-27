using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PowerArgs
{
    /// <summary>
    /// A simple tab completion source implementation that looks for matches over a set of pre-determined strings.
    /// </summary>
    public class SimpleTabCompletionSource : ITabCompletionSourceWithContext
    {
        Func<IEnumerable<string>> CandidateFunction { get; set; }

        CycledTabCompletionManager manager;

        /// <summary>
        /// Require that the user type this number of characters before the source starts cycling through ambiguous matches.  The default is 3.
        /// </summary>
        public int MinCharsBeforeCyclingBegins { get; set; }

        private SimpleTabCompletionSource()
        {
            this.manager = new CycledTabCompletionManager();
            this.MinCharsBeforeCyclingBegins = 3;
        }

        /// <summary>
        /// Creates a new completion source given an enumeration of string candidates
        /// </summary>
        /// <param name="candidates"></param>
        public SimpleTabCompletionSource(IEnumerable<string> candidates)
            : this()
        {
            this.CandidateFunction = () => { return candidates.OrderBy(s => s); };
        }

        /// <summary>
        /// Creates a simple tab completion source given a function used to evaluate the candidates.
        /// </summary>
        /// <param name="candidateFunction">The function used to evaluate the completions where the input is a string parameter that represents the incomplete token</param>
        protected SimpleTabCompletionSource(Func<IEnumerable<string>> candidateFunction) : this()
        {
            // TODO P0 - This is exercised by a sample, but not by a test
            this.CandidateFunction = candidateFunction;
        }

        /// <summary>
        /// Not implemented since this type implements ITabCompletionSourceWithContext
        /// </summary>
        /// <param name="shift"></param>
        /// <param name="context"></param>
        /// <param name="completion"></param>
        /// <returns></returns>
        public bool TryComplete(bool shift, string context, out string completion)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Iterates through the candidates to try to find a match.  If there are multiple possible matches it 
        /// supports cycling through tem as the user continually presses tab.
        /// </summary>
        /// <param name="shift">Indicates if shift was being pressed</param>
        /// <param name="soFar">The text token that the user has typed before pressing tab.</param>
        /// <param name="context"></param>
        /// <param name="completion">The variable that you should assign the completed string to if you find a match.</param>
        /// <returns></returns>
        public bool TryComplete(bool shift, string context, string soFar, out string completion)
        {
            manager.MinCharsBeforeCyclingBegins = this.MinCharsBeforeCyclingBegins;

            bool ignoreCase = true;

            return manager.Cycle(shift, ref soFar, () =>
            {
                return (from c in CandidateFunction() where c.StartsWith(soFar, ignoreCase, CultureInfo.CurrentCulture) select c).ToList();
            }, out completion);
        }
    }
}
