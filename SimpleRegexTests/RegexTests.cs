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
        [InlineData(@".*")]
        [InlineData(@".+")]
        [InlineData(@"(ab)+b")]
        [InlineData(@"(ab)*b")]
        [InlineData(@"(ab)?b")]
        [InlineData(@"a[b-d]d")]
        [InlineData(@"a[b-\-]d")]
        [InlineData(@"a[b-\s]d")]
        [InlineData(@"a[b-\\]d")]
        [InlineData(@"a[\b-\\]d")]
        [InlineData(@"^abc$")]
        [InlineData(@"a|b|c")]
        [InlineData(@"a(b|bc|cd)c")]
        [InlineData(@"a(?:bcd)e")]
        [InlineData(@"(?:ab)+b")]
        [InlineData(@"(?:ab)*b")]
        [InlineData(@"(?:ab)?b")]
        [InlineData(@"a(?:b|bc|cd)c")]
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
        [InlineData(@"[abc]?@[def]?", "@", true, 0, 1)]
        [InlineData(@"[abc]?@[def]?", "a@f", true, 0, 3)]
        [InlineData(@"[abc]?@[def]?", "aaabbac@fededf", true, 6, 3)]
        [InlineData(@"[abc]?@[def]?", "aabbacd@afededf", true, 7, 1)]
        [InlineData(@"[a-c]*@[d-f]*", "@", true, 0, 1)]
        [InlineData(@"[a-c]*@[d-f]*", "aaabbac@fededf", true, 0, 14)]
        [InlineData(@"[a-c]*@[d-f]*", "aabbacd@afededf", true, 7, 1)]
        [InlineData(@"[a-c]+@[d-f]+", "@", false, 0, 0)]
        [InlineData(@"[a-c]+@[d-f]+", "a@f", true, 0, 3)]
        [InlineData(@"[a-c]+@[d-f]+", "aaabbac@fededf", true, 0, 14)]
        [InlineData(@"[a-c]+@[d-f]+", "aabbacd@afededf", false, 7, 1)]
        [InlineData(@"[a-c]?@[d-f]?", "@", true, 0, 1)]
        [InlineData(@"[a-c]?@[d-f]?", "a@f", true, 0, 3)]
        [InlineData(@"[a-c]?@[d-f]?", "aaabbac@fededf", true, 6, 3)]
        [InlineData(@"[a-c]?@[d-f]?", "aabbacd@afededf", true, 7, 1)]
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
        [InlineData(@".*", "", true, 0, 0)]
        [InlineData(@".*", "a", true, 0, 1)]
        [InlineData(@".*", "b", true, 0, 1)]
        [InlineData(@".*", "aa", true, 0, 2)]
        [InlineData(@".*", "aba", true, 0, 3)]
        [InlineData(@".*", "abba", true, 0, 4)]
        [InlineData(@"^abc$", "abc", true, 0, 3)]
        [InlineData(@"^abc$", "aabc", false, 0, 0)]
        [InlineData(@"^abc$", "abca", false, 0, 0)]
        [InlineData(@"^abc$", "aabca", false, 0, 0)]
        [InlineData(@"^\s*$", "    \t", true, 0, 5)]
        [InlineData(@"^\s*$", "  a  \t", false, 0, 0)]
        [InlineData(@"^\S*$", "fivec", true, 0, 5)]
        [InlineData(@"^\S*$", "five c", false, 0, 0)]
        [InlineData(@"^\d*$", "1234567890", true, 0, 10)]
        [InlineData(@"^\d*$", "1 2 3 4 5 6 7 8 9 0", false, 0, 0)]
        [InlineData(@"^\D*$", "fivec", true, 0, 5)]
        [InlineData(@"^\D*$", "5c", false, 0, 0)]
        [InlineData(@"^\w*$", "hello_there_bby3", true, 0, 16)]
        [InlineData(@"^\w*$", "hello there bby3", false, 0, 0)]
        [InlineData(@"^\W*$", "-----", true, 0, 5)]
        [InlineData(@"^\W*$", "--a--", false, 0, 0)]
        [InlineData(@"^\n$", "\n", true, 0, 1)]
        [InlineData(@"^\n$", "\n ", false, 0, 0)]
        [InlineData(@"^\r$", "\r", true, 0, 1)]
        [InlineData(@"^\r$", "\r ", false, 0, 0)]
        [InlineData(@"^\t$", "\t", true, 0, 1)]
        [InlineData(@"^\t$", "\t ", false, 0, 0)]
        [InlineData(@"^\0$", "\0", true, 0, 1)]
        [InlineData(@"^\0$", "\0 ", false, 0, 0)]
        [InlineData(@"^\b$", "\b", true, 0, 1)]
        [InlineData(@"^\b$", "\b ", false, 0, 0)]
        [InlineData(@"^\f$", "\f", true, 0, 1)]
        [InlineData(@"^\f$", "\f ", false, 0, 0)]
        [InlineData(@"a|b|c", "a", true, 0, 1)]
        [InlineData(@"a|b|c", "b", true, 0, 1)]
        [InlineData(@"a|b|c", "c", true, 0, 1)]
        [InlineData(@"a|b|c", "d", false, 0, 0)]
        [InlineData(@"^((a|b)(c|d))?", "", true, 0, 0)]
        [InlineData(@"^((a|b)(c|d))?", "ac", true, 0, 2)]
        [InlineData(@"^((a|b)(c|d))?", "ad", true, 0, 2)]
        [InlineData(@"^((a|b)(c|d))?", "bc", true, 0, 2)]
        [InlineData(@"^((a|b)(c|d))?", "bd", true, 0, 2)]
        [InlineData(@"^((a|b)(c|d))?", "dc", true, 0, 0)]
        [InlineData(@"^((a|b)(c|d))?$", "", true, 0, 0)]
        [InlineData(@"^((a|b)(c|d))?$", "ac", true, 0, 2)]
        [InlineData(@"^((a|b)(c|d))?$", "ad", true, 0, 2)]
        [InlineData(@"^((a|b)(c|d))?$", "bc", true, 0, 2)]
        [InlineData(@"^((a|b)(c|d))?$", "bd", true, 0, 2)]
        [InlineData(@"^((a|b)(c|d))?$", "dc", false, 0, 0)]
        [InlineData(@"^((a|b)(c|d)*)?$", "", true, 0, 0)]
        [InlineData(@"^((a|b)(c|d)*)?$", "ac", true, 0, 2)]
        [InlineData(@"^((a|b)(c|d)*)?$", "acc", true, 0, 3)]
        [InlineData(@"^((a|b)(c|d)*)?$", "aac", false, 0, 0)]
        [InlineData(@"^((a|b)(c|d)*)?$", "ad", true, 0, 2)]
        [InlineData(@"^((a|b)(c|d)*)?$", "add", true, 0, 3)]
        [InlineData(@"^((a|b)(c|d)*)?$", "aad", false, 0, 0)]
        [InlineData(@"^((a|b)(c|d)*)?$", "bc", true, 0, 2)]
        [InlineData(@"^((a|b)(c|d)*)?$", "bcc", true, 0, 3)]
        [InlineData(@"^((a|b)(c|d)*)?$", "bbc", false, 0, 0)]
        [InlineData(@"^((a|b)(c|d)*)?$", "bd", true, 0, 2)]
        [InlineData(@"^((a|b)(c|d)*)?$", "bdd", true, 0, 3)]
        [InlineData(@"^((a|b)(c|d)*)?$", "bbd", false, 0, 0)]
        [InlineData(@"^((a|b)(c|d)*)?$", "dc", false, 0, 0)]
        [InlineData(@"^((a|b)c*)?$", "", true, 0, 0)]
        [InlineData(@"^((a|b)c*)?$", "ac", true, 0, 2)]
        [InlineData(@"^((a|b)c*)?$", "bc", true, 0, 2)]
        [InlineData(@"^((a|b)c*)?$", "acc", true, 0, 3)]
        [InlineData(@"^((a|b)c*)?$", "bcc", true, 0, 3)]
        [InlineData(@"^((a|b)c*)?$", "aac", false, 0, 0)]
        [InlineData(@"^((a|b)c*)?$", "bbc", false, 0, 0)]
        [InlineData(@"^((a|b)c*)$", "", false, 0, 0)]
        [InlineData(@"^((a|b)c*)$", "ac", true, 0, 2)]
        [InlineData(@"^((a|b)c*)$", "bc", true, 0, 2)]
        [InlineData(@"^((a|b)c*)$", "acc", true, 0, 3)]
        [InlineData(@"^((a|b)c*)$", "bcc", true, 0, 3)]
        [InlineData(@"^((a|b)c*)$", "aac", false, 0, 0)]
        [InlineData(@"^((a|b)c*)$", "bbc", false, 0, 0)]
        [InlineData(@"^(a|b)c*$", "", false, 0, 0)]
        [InlineData(@"^(a|b)c*$", "ac", true, 0, 2)]
        [InlineData(@"^(a|b)c*$", "bc", true, 0, 2)]
        [InlineData(@"^(a|b)c*$", "acc", true, 0, 3)]
        [InlineData(@"^(a|b)c*$", "bcc", true, 0, 3)]
        [InlineData(@"^(a|b)c*$", "aac", false, 0, 0)]
        [InlineData(@"^(a|b)c*$", "bbc", false, 0, 0)]
        [InlineData(@"^((a|b)c?)?$", "", true, 0, 0)]
        [InlineData(@"^((a|b)c?)?$", "ac", true, 0, 2)]
        [InlineData(@"^((a|b)c?)?$", "bc", true, 0, 2)]
        [InlineData(@"^((a|b)c?)?$", "acc", false, 0, 0)]
        [InlineData(@"^((a|b)c?)?$", "bcc", false, 0, 0)]
        [InlineData(@"^((a|b)c?)?$", "aac", false, 0, 0)]
        [InlineData(@"^((a|b)c?)?$", "bbc", false, 0, 0)]
        [InlineData(@"^(|a|b)bc$", "", false, 0, 0)]
        [InlineData(@"^(|a|b)bc$", "a", false, 0, 0)]
        [InlineData(@"^(|a|b)bc$", "b", false, 0, 0)]
        [InlineData(@"^(|a|b)bc$", "c", false, 0, 0)]
        [InlineData(@"^(|a|b)bc$", "bc", true, 0, 2)]
        [InlineData(@"^(|a|b)bc$", "abc", true, 0, 3)]
        [InlineData(@"^(|a|b)bc$", "bbc", true, 0, 3)]
        [InlineData(@"^[\s]*(.*?)[\s]*$", "abc def", true, 0, 7)]
        [InlineData(@"^[\s]*(.*?)[\s]*$", "   abc def", true, 0, 10)]
        [InlineData(@"^[\s]*(.*?)[\s]*$", "   abc def   ", true, 0, 13)]
        public void TryMatch(string regex, string text, bool expect, int start, int len)
        {
            var obj = new Regex(regex);
            var match = obj.FindMatch(text)?.FullMatch;
            Assert.Equal(expect, match != null);
            if (match != null)
            {
                Assert.Equal(start, match.Start);
                Assert.Equal(len, match.Length);
            }
        }

        [Theory]
        [InlineData(@"^[\s]*(.*?)[\s]*$", "abc def", 0, 0, "abc def")]
        [InlineData(@"^[\s]*(.*?)[\s]*$", "   abc def", 0, 3, "abc def")]
        [InlineData(@"^[\s]*(.*?)[\s]*$", "   abc def   ", 0, 3, "abc def")]
        public void TryMatchGroup(string regex, string text, int group, int start, string value)
        {
            var obj = new Regex(regex);
            var match = obj.FindMatch(text);
            Assert.NotNull(match);

            var range = match.Groups[group];
            Assert.Equal(start, range.Start);
            Assert.Equal(value, range.SubstringOf(text));
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
