using System;
using System.Collections.Generic;

namespace PowerArgs
{
    /// <summary>
    /// This helper class can be leveraged when implementing custom tab completion logic.  It knows how to cycle through multple
    /// candidates and support tabbing forward and shift/tabbing backwards.  You just pass values from the tab completion methods
    /// and then provide an evaluation function that knows how to get the list of possible matches.
    /// </summary>
    public class CycledTabCompletionManager
    {
        /// <summary>
        /// If the value of soFar is a string that's less than this value then no completion will be returned.
        /// </summary>
        public int MinCharsBeforeCyclingBegins { get; set; }

        int lastIndex;
        string lastCompletion;
        string lastSoFar;

        /// <summary>
        /// Cycles through the candidates provided by the given evaluation function using the arguments passed through from
        /// the tab completion system.
        /// </summary>
        /// <param name="shift">You should pass true if the shift key was pressed during the tab</param>
        /// <param name="soFar">You should pass through a reference to the soFar value that was sent by the tab completion system</param>
        /// <param name="evaluation">A function that looks at 'soFar' and determines which values might be a match</param>
        /// <param name="completion">The completion to populate if the conditions all work out</param>
        /// <returns>True if completion was populated, false otherwise</returns>
        public bool Cycle(bool shift, ref string soFar, Func<List<string>> evaluation, out string completion)
        {
            if (soFar == lastCompletion && lastCompletion != null)
            {
                soFar = lastSoFar;
            }

            var candidates = evaluation();

            if (soFar == lastSoFar) lastIndex = shift ? lastIndex - 1 : lastIndex + 1;
            if (lastIndex >= candidates.Count) lastIndex = 0;
            if (lastIndex < 0) lastIndex = candidates.Count - 1;
            lastSoFar = soFar;

            if (candidates.Count == 0 || (candidates.Count > 1 && soFar.Length < MinCharsBeforeCyclingBegins))
            {
                completion = null;
                return false;
            }
            else
            {
                completion = candidates[lastIndex];
                lastCompletion = completion;
                return true;
            }
        }
    }
}
