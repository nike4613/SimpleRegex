using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimpleRegex
{
    internal class RegexCompiler
    {
        private readonly List<ushort> instructions = new List<ushort>();
        private readonly List<CharacterGroupExpression> charGroups = new List<CharacterGroupExpression>();
        private readonly RegexInterpreter interpreter;

        private int lastGreedyQuantifierBacktraceLoc = -1;

        public RegexCompiler()
        {
            interpreter = new RegexInterpreter(instructions, charGroups);
        }

        public RegexCompiler Compile(Expression expr)
        {
            var startJump = EmitPartialJump(RegexInterpreter.Instruction.Jump);
            var rejectPos = instructions.Count;
            Emit(RegexInterpreter.Instruction.Reject);
            var advanceIterStart = instructions.Count;
            Emit(RegexInterpreter.Instruction.Advance);
            EmitJump(RegexInterpreter.Instruction.JumpIfOutOfBounds, rejectPos);
            RepairPartialJump(startJump, instructions.Count);

            var emit = EmitTryMatchExpression(expr, out var continuePartial);
            RepairPartialJump(emit, advanceIterStart);
            RepairPartialJump(continuePartial, instructions.Count);

            Emit(RegexInterpreter.Instruction.Match);

            return this;
        }

        // all EmitExpression variants return the partial jump on failure
        private IEnumerable<int> EmitTryMatchExpression(Expression expr, out IEnumerable<int> continuePartial)
            => expr switch
            {
                GroupExpression group => EmitTryMatchGroupExpression(group, out continuePartial),
                CharacterGroupExpression chr => EmitTryMatchCharacterGroup(chr, out continuePartial),
                QuantifierExpression quant => EmitTryMatchQuantifier(quant, out continuePartial),
                _ => throw new NotImplementedException(),
            };

        private IEnumerable<int> EmitTryMatchGroupExpression(GroupExpression group, out IEnumerable<int> continuePartial)
        {
            Emit(RegexInterpreter.Instruction.PushPos);
            var openJump = EmitPartialJump(RegexInterpreter.Instruction.Jump);
            var onFail = instructions.Count;
            Emit(RegexInterpreter.Instruction.PopPos);
            var exitJump = EmitPartialJump(RegexInterpreter.Instruction.Jump);
            RepairPartialJump(openJump, instructions.Count);

            continuePartial = Enumerable.Empty<int>();
            // TODO: figure out how to handle backtracking
            for (int i = 0; i < group.Count; i++)
            {
                var partial = EmitTryMatchExpression(group[i], out continuePartial);
                RepairPartialJump(partial, onFail);
                RepairPartialJump(continuePartial, instructions.Count);
            }

            return new[] { exitJump };
        }

        private IEnumerable<int> EmitTryMatchCharacterGroup(CharacterGroupExpression group, out IEnumerable<int> continuePartial)
        {
            var boundsCheck = EmitPartialJump(RegexInterpreter.Instruction.JumpIfOutOfBounds);
            var exitJump = EmitPartialJumpIfNotMatch(group);
            Emit(RegexInterpreter.Instruction.Advance);
            continuePartial = Enumerable.Empty<int>();
            return new[] { exitJump, boundsCheck };
        }

        private IEnumerable<int> EmitTryMatchQuantifier(QuantifierExpression quant, out IEnumerable<int> continuePartial)
        {
            return quant.Type switch
            {
                QuantifierExpression.QuantifierType.Optional => EmitTryMatchOptionalQuantifier(quant, out continuePartial),
                QuantifierExpression.QuantifierType.ZeroOrMore => EmitTryMatchZeroOrMoreQuantifier(quant, out continuePartial),
                QuantifierExpression.QuantifierType.OneOrMore => EmitTryMatchOneOrMoreQuantifier(quant, out continuePartial),
                _ => throw new NotImplementedException(),
            };
        }

        private IEnumerable<int> EmitTryMatchOneOrMoreQuantifier(QuantifierExpression quant, out IEnumerable<int> continuePartial)
        {
            var firstFail = EmitTryMatchExpression(quant.Target, out var cont);
            RepairPartialJump(cont, instructions.Count);

            var repeatFail = EmitTryMatchZeroOrMoreQuantifier(quant, out cont);
            RepairPartialJump(repeatFail, instructions.Count);
            RepairPartialJump(cont, instructions.Count);
            continuePartial = Enumerable.Empty<int>();

            return firstFail;
        }

        private IEnumerable<int> EmitTryMatchZeroOrMoreQuantifier(QuantifierExpression quant, out IEnumerable<int> continuePartial)
        {
            var matchTarget = instructions.Count;
            var failed = EmitTryMatchExpression(quant.Target, out var cont);
            RepairPartialJump(cont, instructions.Count);

            EmitJump(RegexInterpreter.Instruction.Jump, matchTarget);
            RepairPartialJump(failed, instructions.Count);

            continuePartial = Enumerable.Empty<int>();
            return Enumerable.Empty<int>();
        }

        private IEnumerable<int> EmitTryMatchOptionalQuantifier(QuantifierExpression quant, out IEnumerable<int> continuePartial)
        {
            var failed = EmitTryMatchExpression(quant.Target, out var cont);
            RepairPartialJump(cont, instructions.Count);
            RepairPartialJump(failed, instructions.Count);
            continuePartial = Enumerable.Empty<int>();
            return Enumerable.Empty<int>();
        }

        #region Emit helpers
        private void EmitJumpIfMatch(CharacterGroupExpression group, int target)
        {
            if (group.Count == 1)
            {
                var chr = group.First();
                Emit(RegexInterpreter.Instruction.JumpIfCharIs, chr);
            }
            else
            {
                Emit(RegexInterpreter.Instruction.JumpIfCharMatches, (ushort)charGroups.Count);
                charGroups.Add(group);
            }
            Emit((ushort)GetJumpArg(instructions.Count, target));
        }
        private void EmitJumpIfNotMatch(CharacterGroupExpression group, int target)
        {
            if (group.Count == 1)
            {
                var chr = group.First();
                Emit(RegexInterpreter.Instruction.JumpIfCharIsNot, chr);
            }
            else
            {
                Emit(RegexInterpreter.Instruction.JumpIfCharNotMatches, (ushort)charGroups.Count);
                charGroups.Add(group);
            }
            Emit((ushort)GetJumpArg(instructions.Count, target));
        }
        private int EmitPartialJumpIfMatch(CharacterGroupExpression group)
        {
            EmitJumpIfMatch(group, 0);
            return instructions.Count - 1;
        }
        private int EmitPartialJumpIfNotMatch(CharacterGroupExpression group)
        {
            EmitJumpIfNotMatch(group, 0);
            return instructions.Count - 1;
        }
        private void EmitJump(RegexInterpreter.Instruction insn, int target)
            => Emit(insn, (ushort)GetJumpArg(instructions.Count + 1, target));

        private int EmitPartialJump(RegexInterpreter.Instruction insn)
        {
            EmitJump(insn, 0);
            return instructions.Count - 1;
        }
        private static short GetJumpArg(int pos, int target) => (short)(target - (pos + 1));
        private void RepairPartialJump(IEnumerable<int> poss, int target)
        {
            foreach (var pos in poss) RepairPartialJump(pos, target);
        }
        private void RepairPartialJump(int pos, int target)
        {
            if (pos < 0) return; // allow for nonexistent partials
            instructions[pos] = (ushort)GetJumpArg(pos, target);
        }

        private void Emit(RegexInterpreter.Instruction insn, ushort value)
        {
            Emit(insn);
            Emit(value);
        }
        private void Emit(RegexInterpreter.Instruction insn)
            => Emit((ushort)insn);
        private void Emit(ushort value)
            => instructions.Add(value);
        #endregion

        public RegexInterpreter AsInterpreter() => interpreter;
    }
}
