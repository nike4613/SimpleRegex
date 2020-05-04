using SimpleRegex;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace SimpleRegexTests
{
    public class SpecificRegexFixture
    {
        public Regex Semver = new Regex("");
    }


    public class TestSpecificRegexes : IClassFixture<SpecificRegexFixture>
    {
        private readonly SpecificRegexFixture regexes;
        public TestSpecificRegexes(SpecificRegexFixture fixture) => regexes = fixture;

        [Theory]
        [InlineData("1.0.0", true, 0, 5)]
        public void SemverRegex(string text, bool matches, int start, int len)
        {
            var match = regexes.Semver.FindMatch(text);
            Assert.Equal(matches, match != null);
            if (match != null)
            {
                Assert.Equal(start, match.Start);
                Assert.Equal(len, match.Length);
            }
        }
    }
}
