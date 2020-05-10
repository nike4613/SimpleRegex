using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimpleRegex
{
    internal abstract class Expression
    {
        public override abstract string ToString();
    }

    internal abstract class CompositeExpression : Expression, IList<Expression>
    {
        private readonly List<Expression> subExpressions = new List<Expression>();

        public Expression this[int index] { get => ((IList<Expression>)subExpressions)[index]; set => ((IList<Expression>)subExpressions)[index] = value; }

        public int Count => ((IList<Expression>)subExpressions).Count;

        public bool IsReadOnly => ((IList<Expression>)subExpressions).IsReadOnly;

        public void Add(Expression item) => ((IList<Expression>)subExpressions).Add(item);

        public void Clear() => ((IList<Expression>)subExpressions).Clear();

        public bool Contains(Expression item) => ((IList<Expression>)subExpressions).Contains(item);

        public void CopyTo(Expression[] array, int arrayIndex) 
            => ((IList<Expression>)subExpressions).CopyTo(array, arrayIndex);

        public IEnumerator<Expression> GetEnumerator()
            => ((IList<Expression>)subExpressions).GetEnumerator();

        public int IndexOf(Expression item)
            => ((IList<Expression>)subExpressions).IndexOf(item);

        public void Insert(int index, Expression item)
            => ((IList<Expression>)subExpressions).Insert(index, item);

        public bool Remove(Expression item)
            => ((IList<Expression>)subExpressions).Remove(item);

        public void RemoveAt(int index)
        {
            ((IList<Expression>)subExpressions).RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
            => ((IList<Expression>)subExpressions).GetEnumerator();

        public override string ToString()
            => $"({string.Join("", this.Select(e => e.ToString()))})";
    }

    internal sealed class GroupExpression : CompositeExpression
    {
        public bool IsOpen { get; set; } = false;

        public bool IsCaptureGroup { get; set; } = false;

        public override string ToString()
            => $"({(IsCaptureGroup ? "" : "?:")}{string.Join("", this.Select(e => e.ToString()))})";
    }

    internal sealed class AlternationExpression : CompositeExpression 
    {
        public override string ToString()
            => $"({string.Join("|", this.Select(e => e.ToString()))})";
    }

    internal sealed class AnchorExpression : Expression
    {
        public bool IsStart { get; set; }
        public bool IsEnd
        {
            get => !IsStart;
            set => IsStart = !value;
        }

        public override string ToString()
            => IsStart ? "^" : "$";
    }

    internal abstract class CharacterGroupExpression : Expression
    {
        public abstract bool Matches(char c);
    }

    internal sealed class AnyCharacterGroup : CharacterGroupExpression
    {
        public override bool Matches(char c) => true;

        public override string ToString() => ".";
    }

    internal sealed class SingleCharacterGroup : CharacterGroupExpression
    { 
        public char Character { get; }
        public SingleCharacterGroup(char c) => Character = c;

        public override bool Matches(char c)
            => c == Character;

        public override string ToString() => Character.ToString();
    }

    internal sealed class WhitespaceCharacterGroup : CharacterGroupExpression
    {
        public bool Inverse { get; set; } = false;
        public override bool Matches(char c)
            => char.IsWhiteSpace(c) ^ Inverse;

        public override string ToString() => "\\" + (Inverse ? "S" : "s");
    }

    internal sealed class RangedCharacterGroup : CharacterGroupExpression
    {
        private string rangePairs = "";
        private readonly List<CharacterGroupExpression> moreGroups = new List<CharacterGroupExpression>();

        public RangedCharacterGroup() { }
        public RangedCharacterGroup(char min, char max) => AddRange(min, max);

        public bool Inverse { get; set; } = false;

        public void Add(char c) => AddRange(c, c);
        public void Add(CharacterGroupExpression group)
        {
            if (group is RangedCharacterGroup range)
            {
                for (int i = 0; i < range.rangePairs.Length; i += 2)
                {
                    AddRange(range.rangePairs[i], (char)(range.rangePairs[i + 1] - 1));
                }
            }
            else if (group is SingleCharacterGroup single)
            {
                Add(single.Character);
            }
            else
            {
                moreGroups.Add(group);
            }
        }
        public void AddRange(char min, char max)
            => rangePairs += new string(new[] { min, (char)(max+1) });

        public override bool Matches(char c)
            => Enumerable.Range(0, rangePairs.Length / 2).Select(i => i * 2)
                         .Any(i => rangePairs[i] <= c && rangePairs[i + 1] > c) ^ Inverse
            || moreGroups.Any(g => g.Matches(c));

        public override string ToString()
        {
            var sb = new StringBuilder(rangePairs.Length + rangePairs.Length / 2 + 2);
            sb.Append("[");
            for (int i = 0; i < rangePairs.Length; i += 2)
            {
                var min = rangePairs[i];
                var max = (char)(rangePairs[i + 1] - 1); 
                sb.Append(min);
                if (min != max) 
                    sb.Append("-")
                      .Append(max);
            }
            sb.Append("]");
            return sb.ToString();
        }
    }

    internal class QuantifierExpression : Expression
    {
        public Expression Target { get; }

        public enum QuantifierType
        {
            ZeroOrMore, OneOrMore, Optional
        }
        public QuantifierType Type { get; }
        public bool IsLazy { get; set; } = false;

        public QuantifierExpression(Expression target, QuantifierType type)
        {
            Target = target;
            Type = type;
        }

        public override string ToString()
            => Target.ToString() + Type switch
            {
                QuantifierType.ZeroOrMore => "*",
                QuantifierType.OneOrMore => "+",
                QuantifierType.Optional => "?",
                _ => "{unknown type}"
            };
    }
}
