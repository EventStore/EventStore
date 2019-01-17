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
            var sf = new StringFilter(null);
            Assert.True(sf.IsStringAllowed("lorem"));
            Assert.True(sf.IsStringAllowed("ipsum"));
            Assert.True(sf.IsStringAllowed("dolor"));
        }

        [Test]
        public void all_strings_allowed_when_passing_empty_array()
        {
            var sf = new StringFilter(new string[0]);
            Assert.True(sf.IsStringAllowed("lorem"));
            Assert.True(sf.IsStringAllowed("ipsum"));
            Assert.True(sf.IsStringAllowed("dolor"));
        }
        [Test]
        public void only_one_string_allowed_when_passing_single_string()
        {
            var sf = new StringFilter(new string[] { "lorem" });
            Assert.True(sf.IsStringAllowed("lorem"));
            Assert.False(sf.IsStringAllowed("ipsum"));
            Assert.False(sf.IsStringAllowed("dolor"));
        }

        [Test]
        public void only_selected_strings_allowed_when_passing_list_of_strings()
        {
            var sf = new StringFilter(new string[] { "lorem", "ipsum", "dolor" });
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
            var sf = new StringFilter(new string[] { "The quick brown fox", "jumped over the hedge"});
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
            var sf = new StringFilter(new string[] { "The quick brown fox jumped over the hedge" });
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

        [Test]
        public void regex_supported_in_match()
        {
            var sf = new StringFilter(new string[] { "foo-.+" });
            Assert.True(sf.IsStringAllowed("foo-bar"));
            Assert.True(sf.IsStringAllowed("foo-foo"));
            Assert.False(sf.IsStringAllowed("foo-"));
        }

        [Test]
        public void regex_supported_in_match2()
        {
            var sf = new StringFilter(new string[] { "ab[c-e]fg" });
            Assert.True(sf.IsStringAllowed("abcfg"));
            Assert.True(sf.IsStringAllowed("abdfg"));
            Assert.True(sf.IsStringAllowed("abefg"));
            Assert.False(sf.IsStringAllowed("abbfg"));
        }

        [Test]
        public void dashes_in_plain_string_not_interpreted_as_regex()
        {
            var sf = new StringFilter(new string[] { "abc-def" });
            Assert.True(sf.IsStringAllowed("abc-def"));
            Assert.False(sf.IsStringAllowed("abcef"));
            Assert.False(sf.IsStringAllowed("abdef"));
        }

        [Test]
        public void system_events_not_regexed()
        {
            var sf = new StringFilter(new string[] { "$User" });
            Assert.True(sf.IsStringAllowed("$User"));
            Assert.False(sf.IsStringAllowed("User"));
            Assert.False(sf.IsStringAllowed("abdef"));
        }

        [Test]
        public void invalid_regexes_throws()
        {
            Assert.Throws<ArgumentException>(() => new StringFilter(new string[] { "ab[c-def" }));
        }
    }
}
