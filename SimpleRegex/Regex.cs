using System;
using System.Collections.Generic;

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
            char Char() => text[i];
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

            Exception Invalid() => new ArgumentException("Invalid regex", nameof(text));

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
                            goto default;
                        case '\\':
                            escape = true;
                            continue;
                        case '(':
                            CollapseLast();
                            exprStack.Push(new GroupExpression { IsOpen = true });
                            continue;
                        case ')':
                            CollapseLast(); // one for the end of the group
                            if (!(exprStack.Peek() is GroupExpression group) || !group.IsOpen || exprStack.Count == 1)
                                throw Invalid();
                            group.IsOpen = false;
                            continue;
                        case '[':
                            CollapseLast();
                            charGroup = true;
                            exprStack.Push(new CharacterGroupExpression());
                            continue;
                        case ']': // this codepath is always invalid
                            throw Invalid();
                        case '*':
                        case '+':
                        case '?':
                            if (exprStack.Count == 1) throw Invalid();
                            var target = exprStack.Pop();
                            if (target is GroupExpression g && g.IsOpen) throw Invalid();
                            exprStack.Push(new QuantifierExpression(target, Char() switch {
                                '*' => QuantifierExpression.QuantifierType.ZeroOrMore,
                                '+' => QuantifierExpression.QuantifierType.OneOrMore,
                                '?' => QuantifierExpression.QuantifierType.Optional,
                                _ => throw new InvalidOperationException()
                            }));
                            continue;
                        // TODO: implement alternation
                        default:
                            CollapseLast();
                            exprStack.Push(new CharacterGroupExpression { Char() });
                            continue;
                    }
                }
                else
                {
                    if (!(exprStack.Peek() is CharacterGroupExpression group)) 
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

                    // TODO: support char ranges
                    group.Add(c);
                    escape = false;
                }
            }
            while (Advance());

            CollapseLast();

            if (exprStack.Count != 1 || charGroup) throw Invalid();

            var expr = exprStack.Pop();

            interpreter = new RegexCompiler().Compile(expr).AsInterpreter();
        }

        private readonly RegexInterpreter interpreter;

        public bool Matches(string text, int startAt = 0)
            => FindMatch(text, startAt) != null;

        public Match? FindMatch(string text, int startAt = 0)
            => interpreter.MatchOn(text, startAt);

        public IEnumerable<Match> FindMatches(string text, int startAt = 0)
        {
            var list = new List<Match>();
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
