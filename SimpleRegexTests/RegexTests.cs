using SimpleRegex;
using System;
using Xunit;

namespace SimpleRegexTests
{
    public class RegexTests
    {
        [Theory]
        [InlineData(@"ab")]
        [InlineData(@"a\e")]
        [InlineData(@"a\\e")]
        [InlineData(@"a(bcd)e")]
        [InlineData(@"a(b(c)d)e")]
        [InlineData(@"a(b\(d)e\)")]
        [InlineData(@"[ab]")]
        [InlineData(@"a[bc]d")]
        [InlineData(@"a[\[bc]d")]
        [InlineData(@"a[\]bc]d")]
        [InlineData(@"a\[bc\]d")]
        [InlineData(@"a+b")]
        [InlineData(@"a*b")]
        [InlineData(@"a?b")]
        [InlineData(@"(ab)+b")]
        [InlineData(@"(ab)*b")]
        [InlineData(@"(ab)?b")]
        public void RegexParse(string text)
        {
            _ = new Regex(text);
        }

        [Theory]
        [InlineData(@"ab", "ab", true, 0, 2)]
        [InlineData(@"ab", "aab", true, 1, 2)]
        [InlineData(@"ab", "aaab", true, 2, 2)]
        [InlineData(@"ab", "ccab", true, 2, 2)]
        [InlineData(@"ab", "a", false, 0, 0)]
        [InlineData(@"ab", "b", false, 0, 0)]
        [InlineData(@"[ab]", "a", true, 0, 1)]
        [InlineData(@"[ab]", "b", true, 0, 1)]
        [InlineData(@"[ab]", "c", false, 0, 0)]
        [InlineData(@"[ab][cd]", "ac", true, 0, 2)]
        [InlineData(@"[ab][cd]", "ad", true, 0, 2)]
        [InlineData(@"[ab][cd]", "bc", true, 0, 2)]
        [InlineData(@"[ab][cd]", "bd", true, 0, 2)]
        [InlineData(@"[ab][cd]", "ab", false, 0, 0)]
        [InlineData(@"[ab][cd]", "ba", false, 0, 0)]
        [InlineData(@"[ab][cd]", "cd", false, 0, 0)]
        [InlineData(@"[ab][cd]", "dc", false, 0, 0)]
        [InlineData(@"[ab][cd]", "abcd", true, 1, 2)]
        [InlineData(@"[ab][cd]", "badc", true, 1, 2)]
        [InlineData(@"a(bcd)e", "abcde", true, 0, 5)]
        [InlineData(@"a(bcd)e", "abe", false, 0, 0)]
        [InlineData(@"a(bcd)e", "ace", false, 0, 0)]
        [InlineData(@"a(bcd)e", "ade", false, 0, 0)]
        [InlineData(@"a?b", "ab", true, 0, 2)]
        [InlineData(@"a?b", "b", true, 0, 1)]
        [InlineData(@"a?b", "cb", true, 1, 1)]
        [InlineData(@"colou?r", "color", true, 0, 5)]
        [InlineData(@"colou?r", "colour", true, 0, 6)]
        [InlineData(@"colou?r", "colur", false, 0, 0)]
        [InlineData(@"a(bcd)?e", "abcde", true, 0, 5)]
        [InlineData(@"a(bcd)?e", "abe", false, 0, 0)]
        [InlineData(@"a(bcd)?e", "ace", false, 0, 0)]
        [InlineData(@"a(bcd)?e", "ade", false, 0, 0)]
        [InlineData(@"a(bcd)?e", "ae", true, 0, 2)]
        [InlineData(@"a(ebcd)?e", "ae", true, 0, 2)]
        [InlineData(@"a(ebcd)?e", "aebcde", true, 0, 6)]
        [InlineData(@"a(ebcd)?e", "abcde", false, 0, 0)]
        [InlineData(@"a(ebcd)?e", "aebde", true, 0, 2)]
        [InlineData(@"[abc]*@[def]*", "@", true, 0, 1)]
        [InlineData(@"[abc]*@[def]*", "aaabbac@fededf", true, 0, 14)]
        [InlineData(@"[abc]*@[def]*", "aabbacd@afededf", true, 7, 1)]
        [InlineData(@"[abc]+@[def]+", "@", false, 0, 0)]
        [InlineData(@"[abc]+@[def]+", "a@f", true, 0, 3)]
        [InlineData(@"[abc]+@[def]+", "aaabbac@fededf", true, 0, 14)]
        [InlineData(@"[abc]+@[def]+", "aabbacd@afededf", false, 7, 1)]
        [InlineData(@"a*", "", true, 0, 0)]
        [InlineData(@"a*", "a", true, 0, 1)]
        [InlineData(@"a*a", "", false, 0, 0)]
        [InlineData(@"a*a", "a", true, 0, 1)]
        [InlineData(@"a*a", "aa", true, 0, 2)]
        [InlineData(@"a*a", "aaa", true, 0, 3)]
        [InlineData(@"a*a", "aaaa", true, 0, 4)]
        [InlineData(@"a+", "", false, 0, 0)]
        [InlineData(@"a+", "a", true, 0, 1)]
        [InlineData(@"a+a", "", false, 0, 0)]
        [InlineData(@"a+a", "a", false, 0, 0)]
        [InlineData(@"a+a", "aa", true, 0, 2)]
        [InlineData(@"a+a", "aaa", true, 0, 3)]
        [InlineData(@"a+a", "aaaa", true, 0, 4)]
        [InlineData(@"a?", "", true, 0, 0)]
        [InlineData(@"a?", "a", true, 0, 1)]
        [InlineData(@"a?", "b", true, 0, 0)]
        [InlineData(@"a?a", "", false, 0, 0)]
        [InlineData(@"a?a", "a", true, 0, 1)]
        [InlineData(@"a?a", "aa", true, 0, 2)]
        public void TryMatch(string regex, string text, bool expect, int start, int len)
        {
            var obj = new Regex(regex);
            var match = obj.FindMatch(text);
            Assert.Equal(expect, match != null);
            if (match != null)
            {
                Assert.Equal(start, match.Start);
                Assert.Equal(len, match.Length);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData(@"")]
        [InlineData(@"*")]
        [InlineData(@"?")]
        [InlineData(@"+")]
        [InlineData(@"*+")]
        [InlineData(@"?*")]
        [InlineData(@"+?")]
        [InlineData(@"a(*)")]
        [InlineData(@"a(+)")]
        [InlineData(@"a(?)")]
        [InlineData(@"abcde)")]
        [InlineData(@"a(bcd\)e")]
        [InlineData(@"a\(bcd)e")]
        [InlineData(@"a(b(c\)d)e")]
        [InlineData(@"a(b\(c)d)e")]
        [InlineData(@"a[bc\]d")]
        [InlineData(@"a\[bc]d")]
        [InlineData(@"a[[bc]d")]
        [InlineData(@"a[[bc]]d")]
        [InlineData(@"a[bc]]d")]
        public void RegexThrow(string text)
        {
            Assert.Throws<ArgumentException>(() => new Regex(text));
        }
    }
}
