using SimpleRegex;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace SimpleRegexTests
{
    public class SpecificRegexFixture
    {
        public Regex Semver = new Regex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-((0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(\.(0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(\+([0-9a-zA-Z-]+(\.[0-9a-zA-Z-]+)*))?$");
    }


    public class TestSpecificRegexes : IClassFixture<SpecificRegexFixture>
    {
        private readonly SpecificRegexFixture regexes;
        public TestSpecificRegexes(SpecificRegexFixture fixture) => regexes = fixture;

        [Theory]
        [InlineData("0.0.4", true, 0, 5)]
        [InlineData("1.0.0", true, 0, 5)]
        [InlineData("1.2.3", true, 0, 5)]
        [InlineData("1.1.2-prerelease+meta", true, 0, 21)]
        [InlineData("1.1.2+meta", true, 0, 10)]
        [InlineData("1.1.2+meta-valid", true, 0, 16)]
        [InlineData("1.0.0-alpha", true, 0, 11)]
        [InlineData("1.0.0-beta", true, 0, 10)]
        [InlineData("1.0.0-alpha.beta", true, 0, 16)]
        [InlineData("1.0.0-alpha.beta.1", true, 0, 18)]
        [InlineData("1.0.0-alpha.1", true, 0, 13)]
        // TODO: add more of the valid cases from https://regex101.com/r/vkijKf/1/
        [InlineData("1", false, 0, 0)]
        [InlineData("1.2", false, 0, 0)]
        [InlineData("1.2.3-0123", false, 0, 0)]
        [InlineData("1.2.3-0123.0123", false, 0, 0)]
        [InlineData("1.1.2+.123", false, 0, 0)]
        [InlineData("+invalid", false, 0, 0)]
        [InlineData("-invalid", false, 0, 0)]
        [InlineData("-invalid+invalid", false, 0, 0)]
        [InlineData("-invalid.01", false, 0, 0)]
        // TODO: add more of the invalid cases from https://regex101.com/r/vkijKf/1/
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
