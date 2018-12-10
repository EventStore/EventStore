using System;
using EventStore.Core.Util;
using NUnit.Framework;

namespace EventStore.Core.Tests.Util
{
    public class StringFilterTest
    {
        [Test]
        public void all_strings_allowed_when_passing_null()
        {
            StringFilter sf = new StringFilter(null);
            Assert.True(sf.IsStringAllowed("lorem"));
            Assert.True(sf.IsStringAllowed("ipsum"));
            Assert.True(sf.IsStringAllowed("dolor"));
        }

        [Test]
        public void all_strings_allowed_when_passing_empty_array()
        {
            StringFilter sf = new StringFilter(new string[0]);
            Assert.True(sf.IsStringAllowed("lorem"));
            Assert.True(sf.IsStringAllowed("ipsum"));
            Assert.True(sf.IsStringAllowed("dolor"));
        }
        [Test]
        public void only_one_string_allowed_when_passing_single_string()
        {
            StringFilter sf = new StringFilter(new string[] { "lorem" });
            Assert.True(sf.IsStringAllowed("lorem"));
            Assert.False(sf.IsStringAllowed("ipsum"));
            Assert.False(sf.IsStringAllowed("dolor"));
        }

        [Test]
        public void only_selected_strings_allowed_when_passing_list_of_strings()
        {
            StringFilter sf = new StringFilter(new string[] { "lorem", "ipsum", "dolor" });
            Assert.True(sf.IsStringAllowed("lorem"));
            Assert.True(sf.IsStringAllowed("ipsum"));
            Assert.True(sf.IsStringAllowed("dolor"));
            Assert.False(sf.IsStringAllowed("sit"));
            Assert.False(sf.IsStringAllowed("amet"));
            Assert.False(sf.IsStringAllowed("gallicum"));
        }

        [Test]
        public void substrings_do_not_match_for_multiple_strings()
        {
            StringFilter sf = new StringFilter(new string[] { "The quick brown fox", "jumped over the hedge"});
            Assert.True(sf.IsStringAllowed("The quick brown fox"));
            Assert.True(sf.IsStringAllowed("jumped over the hedge"));
            Assert.False(sf.IsStringAllowed("The"));
            Assert.False(sf.IsStringAllowed("quick"));
            Assert.False(sf.IsStringAllowed("brown"));
            Assert.False(sf.IsStringAllowed("fox"));
            Assert.False(sf.IsStringAllowed("jumped"));
            Assert.False(sf.IsStringAllowed("over"));
            Assert.False(sf.IsStringAllowed("the"));
            Assert.False(sf.IsStringAllowed("hedge"));
        }

        [Test]
        public void substrings_do_not_match_for_single_string()
        {
            StringFilter sf = new StringFilter(new string[] { "The quick brown fox jumped over the hedge" });
            Assert.True(sf.IsStringAllowed("The quick brown fox jumped over the hedge"));
            Assert.False(sf.IsStringAllowed("The quick brown fox"));
            Assert.False(sf.IsStringAllowed("jumped over the hedge"));
            Assert.False(sf.IsStringAllowed("The"));
            Assert.False(sf.IsStringAllowed("quick"));
            Assert.False(sf.IsStringAllowed("brown"));
            Assert.False(sf.IsStringAllowed("fox"));
            Assert.False(sf.IsStringAllowed("jumped"));
            Assert.False(sf.IsStringAllowed("over"));
            Assert.False(sf.IsStringAllowed("the"));
            Assert.False(sf.IsStringAllowed("hedge"));
        }
    }
}
