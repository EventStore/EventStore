using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PowerArgs
{
    /// <summary>
    /// A simple helper that makes it possible to do Linq queries over a MatchCollection
    /// </summary>
    public static class MatchCollectionEx
    {
        /// <summary>
        /// Converts a MatchCollection to a List of Match objects so that you can perform linq queries over the matches.
        /// </summary>
        /// <param name="matches">The MatchCollection to convert</param>
        /// <returns>a list of Match objects</returns>
        public static List<Match> ToList(this MatchCollection matches)
        {
            List<Match> ret = new List<Match>();
            foreach (Match match in matches) ret.Add(match);
            return ret;
        }
    }
}
