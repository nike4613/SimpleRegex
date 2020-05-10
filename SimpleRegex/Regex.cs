using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleRegex
{
    public class Regex
    {

        public Regex(string text)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("Regex cannot be empty", nameof(text));

            var exprStack = new Stack<Expression>();
            exprStack.Push(new GroupExpression { IsOpen = true });

            int i = 0;
            char Char(int offset = 0) => text.Length > i + offset ? text[i + offset] : '\0';
            bool Advance() => ++i < text.Length;

            void CollapseLast()
            {
                if (exprStack.Count > 1)
                {
                    if (exprStack.Peek() is GroupExpression group && group.IsOpen)
                        return;

                    var expr = exprStack.Pop();
                    if (exprStack.Peek() is CompositeExpression composite)
                        composite.Add(expr);
                    else
                        throw new InvalidOperationException();
                }
            }

            Exception Invalid(int offs = 0) => new ArgumentException($"Invalid regex at {i + offs}", nameof(text));

            bool escape = false;
            bool charGroup = false;

            do
            {
                if (!charGroup)
                {
                    switch (Char())
                    {
                        case var _ when escape:
                            escape = false;
                            CollapseLast();
                            exprStack.Push(GetCharGroupForEscaped(Char()));
                            continue;

                        case '\\':
                            escape = true;
                            continue;
                        case '(':
                            CollapseLast();
                            var isCapture = true;
                            if (Char(1) == '?')
                            {
                                if (Char(2) != ':') // TODO: support other types of groups
                                    throw Invalid(2);
                                else
                                {
                                    isCapture = false;
                                    Advance();
                                }
                                Advance();
                            }
                            exprStack.Push(new GroupExpression { IsOpen = true, IsCaptureGroup = isCapture });
                            continue;
                        case ')':
                            {
                                CollapseLast(); // one for the end of the group
                                if (!(exprStack.Peek() is GroupExpression group) || !group.IsOpen || exprStack.Count == 1)
                                    throw Invalid();
                                group.IsOpen = false;
                                if (exprStack.Skip(1).FirstOrDefault() is AlternationExpression alt)
                                { // below the group is an alternation, so add the group
                                    alt.Add(exprStack.Pop());
                                    if (exprStack.Skip(1).FirstOrDefault() is GroupExpression grou && grou.IsCaptureGroup)
                                    { // below the alternation is a capture group, so add the alternation
                                        grou.Add(exprStack.Pop());
                                    }
                                }
                                continue;
                            }
                        case '|':
                            { // the alternation shit is whack
                                CollapseLast();
                                var top = exprStack.Pop();
                                if (!(top is CompositeExpression))
                                    throw new InvalidOperationException();
                                if (top is GroupExpression group)
                                { // close the group
                                    group.IsOpen = false;
                                    if (group.IsCaptureGroup)
                                    { // push a wrapping capture group so that code emission can be simple
                                        group.IsCaptureGroup = false;
                                        exprStack.Push(new GroupExpression { IsOpen = false, IsCaptureGroup = true });
                                    }
                                }
                                if (exprStack.Count > 0 && exprStack.Peek() is AlternationExpression alt)
                                { // we already have an alternation beneath
                                    alt.Add(top);
                                }
                                else
                                { // we need to create a new alternation
                                    exprStack.Push(new AlternationExpression { top });
                                }

                                // add another group on top for later alternations, etc
                                exprStack.Push(new GroupExpression { IsOpen = true });

                                continue;
                            }
                        case '[':
                            {
                                CollapseLast();
                                charGroup = true;
                                var group = new RangedCharacterGroup();
                                exprStack.Push(group);
                                if (Char(1) == '^')
                                {
                                    group.Inverse = true;
                                    Advance();
                                }
                                continue;
                            }
                        case ']': // this codepath is always invalid
                            throw Invalid();
                        case '*':
                        case '+':
                        case '?':
                            {
                                if (exprStack.Count == 1) throw Invalid();
                                var target = exprStack.Pop();
                                if (target is GroupExpression g && g.IsOpen) throw Invalid();
                                var lazy = Char(1) == '?';
                                exprStack.Push(new QuantifierExpression(target, Char() switch
                                {
                                    '*' => QuantifierExpression.QuantifierType.ZeroOrMore,
                                    '+' => QuantifierExpression.QuantifierType.OneOrMore,
                                    '?' => QuantifierExpression.QuantifierType.Optional,
                                    _ => throw new InvalidOperationException()
                                })
                                {
                                    IsLazy = lazy
                                });
                                if (lazy) Advance();
                                continue;
                            }
                        case '.':
                            CollapseLast();
                            exprStack.Push(new AnyCharacterGroup());
                            continue;
                        case '^':
                            CollapseLast();
                            exprStack.Push(new AnchorExpression { IsStart = true });
                            continue;
                        case '$':
                            CollapseLast();
                            exprStack.Push(new AnchorExpression { IsStart = false });
                            continue;

                        default:
                            CollapseLast();
                            exprStack.Push(new SingleCharacterGroup(Char()));
                            continue;
                    }
                }
                else
                {
                    if (!(exprStack.Peek() is RangedCharacterGroup group)) 
                        throw new InvalidOperationException();

                    var c = Char();
                    if (c == '\\' && !escape)
                    {
                        escape = true;
                        continue;
                    }
                    if (c == ']' && !escape)
                    {
                        charGroup = false;
                        continue;
                    }
                    if (c == '[' && !escape) // this codepath is always invalid
                        throw Invalid();

                    if (Char(1) == '-' && !escape)
                    {
                        Advance();
                        escape = false;
                        char max = '\0';
                        while (Advance())
                        {
                            var c2 = Char();
                            if (c2 == '\\' && !escape)
                            {
                                escape = true;
                                continue;
                            }
                            if (escape && c2 != '\\' && c2 != '-')
                            {
                                group.Add(c);
                                group.Add('-');
                                c = c2;
                                goto handleDefault;
                            }
                            max = c2;
                            escape = false;
                            break;
                        }

                        group.AddRange(c, max);
                        continue;
                    }

                handleDefault:
                    if (escape)
                        group.Add(GetCharGroupForEscaped(c));
                    else
                        group.Add(c);

                    escape = false;
                }
            }
            while (Advance());

            { // clean up the stack, which may have a group over an alternation
                CollapseLast();
                if (exprStack.Peek() is GroupExpression group)
                {
                    group.IsOpen = false;
                }
                if (exprStack.Skip(1).FirstOrDefault() is AlternationExpression alt)
                { // TODO: refactor this out somehow?
                    alt.Add(exprStack.Pop());
                }
            }

            if (exprStack.Count != 1 || charGroup) throw Invalid();

            var expr = exprStack.Pop();

            interpreter = new RegexCompiler().Compile(expr).AsInterpreter();
        }

        private static CharacterGroupExpression GetCharGroupForEscaped(char c)
            => c switch
            {
                's' => new WhitespaceCharacterGroup { Inverse = false },
                'S' => new WhitespaceCharacterGroup { Inverse = true },
                'd' => new RangedCharacterGroup('0', '9') { Inverse = false },
                'D' => new RangedCharacterGroup('0', '9') { Inverse = true },
                'w' => WordGroup(false),
                'W' => WordGroup(true),
                'n' => new SingleCharacterGroup('\n'),
                'r' => new SingleCharacterGroup('\r'),
                't' => new SingleCharacterGroup('\t'),
                '0' => new SingleCharacterGroup('\0'),
                'b' => new SingleCharacterGroup('\b'),
                'f' => new SingleCharacterGroup('\f'),
                _ => new SingleCharacterGroup(c)
            };

        private static RangedCharacterGroup WordGroup(bool inverse)
        {
            var group = new RangedCharacterGroup { Inverse = inverse };
            group.AddRange('a', 'z');
            group.AddRange('A', 'Z');
            group.AddRange('0', '9');
            group.Add('_');
            return group;
        }

        private readonly RegexInterpreter interpreter;

        public bool Matches(string text, int startAt = 0)
            => FindMatch(text, startAt) != null;

        public Match? FindMatch(string text, int startAt = 0)
            => interpreter.MatchOn(text, startAt);

        public IEnumerable<Match?> FindMatches(string text, int startAt = 0)
        {
            var list = new List<Match?>();
            Match? match;
            while ((match = FindMatch(text, startAt)) != null)
            {
                startAt = match.End;
                list.Add(match);
            }
            return list;
        }
    }

    public class Match
    {
        public int Start { get; }
        public int Length { get; }
        public int End => Start + Length;
        public Match(int start, int length)
        {
            Start = start;
            Length = length;
        }
        public static Match FromOffsets(int start, int end)
            => new Match(start, end - start);
    }
}
