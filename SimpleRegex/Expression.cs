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
    }

    internal sealed class AlternationExpression : CompositeExpression { }

    internal sealed class CharacterGroupExpression : Expression, ICollection<char>
    {
        private readonly HashSet<char> matchOptions = new HashSet<char>();

        public int Count => ((ICollection<char>)matchOptions).Count;

        public bool IsReadOnly => ((ICollection<char>)matchOptions).IsReadOnly;

        public void Add(char item) => ((ICollection<char>)matchOptions).Add(item);

        public void Clear() => ((ICollection<char>)matchOptions).Clear();

        public bool Contains(char item) => ((ICollection<char>)matchOptions).Contains(item);

        public void CopyTo(char[] array, int arrayIndex) 
            => ((ICollection<char>)matchOptions).CopyTo(array, arrayIndex);

        public IEnumerator<char> GetEnumerator() => ((ICollection<char>)matchOptions).GetEnumerator();

        public bool Remove(char item) => ((ICollection<char>)matchOptions).Remove(item);

        IEnumerator IEnumerable.GetEnumerator() => ((ICollection<char>)matchOptions).GetEnumerator();

        public override string ToString()
            => $"[{new string(this.ToArray())}]";
    }

    internal class QuantifierExpression : Expression
    {
        public Expression Target { get; }

        public enum QuantifierType
        {
            ZeroOrMore, OneOrMore, Optional
        }
        public QuantifierType Type { get; }

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
