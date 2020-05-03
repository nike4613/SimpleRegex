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

        private ushort LocalCount
        {
            get => (ushort)interpreter.localCount;
            set => interpreter.localCount = value;
        }
        private int Current => instructions.Count;
        private int lastGreedyQuantifierBacktraceLoc = -1;

        public RegexCompiler()
        {
            interpreter = new RegexInterpreter(instructions, charGroups);
        }

        public RegexCompiler Compile(Expression expr)
        {
            var startJump = EmitPartialJump(RegexInterpreter.Instruction.Jump);
            var rejectPos = Current;
            Emit(RegexInterpreter.Instruction.Reject);
            var advanceIterStart = Current;
            lastGreedyQuantifierBacktraceLoc = advanceIterStart;
            Emit(RegexInterpreter.Instruction.PopPos);
            Emit(RegexInterpreter.Instruction.Advance);
            EmitJump(RegexInterpreter.Instruction.JumpIfOutOfBounds, rejectPos);
            RepairPartialJump(startJump, Current);
            Emit(RegexInterpreter.Instruction.PushPos);
            Emit(RegexInterpreter.Instruction.Return);

            var failure = EmitTryMatchExpression(expr, out var continuePartial, out _);
            RepairPartialJump(failure, advanceIterStart);
            RepairPartialJump(continuePartial, Current);

            Emit(RegexInterpreter.Instruction.Match);

            return this;
        }

        // all EmitExpression variants return the partial jump on failure
        private IEnumerable<int> EmitTryMatchExpression(Expression expr, out IEnumerable<int> continuePartial, out int? backtrackFunc)
            => expr switch
            {
                GroupExpression group => EmitTryMatchGroupExpression(group, out continuePartial, out backtrackFunc),
                CharacterGroupExpression chr => EmitTryMatchCharacterGroup(chr, out continuePartial, out backtrackFunc),
                QuantifierExpression quant => EmitTryMatchQuantifier(quant, out continuePartial, out backtrackFunc),
                _ => throw new NotImplementedException(),
            };

        private IEnumerable<int> EmitTryMatchGroupExpression(GroupExpression group, out IEnumerable<int> continuePartial, out int? backtrackFunc)
        {
            //Emit(RegexInterpreter.Instruction.PushPos);
            var openJump = EmitPartialJump(RegexInterpreter.Instruction.Jump);
            var onFail = Current;
            //Emit(RegexInterpreter.Instruction.PopPos);
            var exitJump = EmitPartialJump(RegexInterpreter.Instruction.Jump);
            RepairPartialJump(openJump, Current);

            continuePartial = Enumerable.Empty<int>();

            var backtracks = new int?[group.Count];
            // TODO: figure out how to handle backtracking
            for (int i = 0; i < group.Count; i++)
            {
                var failure = EmitTryMatchExpression(group[i], out continuePartial, out backtracks[i]);
                RepairPartialJump(failure, /*onFail*/ lastGreedyQuantifierBacktraceLoc);
                RepairPartialJump(continuePartial, Current);
            }

            var finishedPartial = EmitPartialJump(RegexInterpreter.Instruction.Jump);

            backtrackFunc = Current;
            for (int i = group.Count - 1; i >= 0; i--)
            {
                var bt = backtracks[i];
                if (bt != null)
                    EmitJump(RegexInterpreter.Instruction.Call, bt.Value);
            }
            Emit(RegexInterpreter.Instruction.Return);

            continuePartial = continuePartial.Append(finishedPartial);
            return new[] { exitJump };
        }

        private IEnumerable<int> EmitTryMatchCharacterGroup(CharacterGroupExpression group, out IEnumerable<int> continuePartial, out int? backtrackFunc)
        {
            var boundsCheck = EmitPartialJump(RegexInterpreter.Instruction.JumpIfOutOfBounds);
            var exitJump = EmitPartialJumpIfNotMatch(group);
            Emit(RegexInterpreter.Instruction.Advance);
            continuePartial = Enumerable.Empty<int>();
            backtrackFunc = null;
            return new[] { exitJump, boundsCheck };
        }

        private IEnumerable<int> EmitTryMatchQuantifier(QuantifierExpression quant, out IEnumerable<int> continuePartial, out int? backtrackFunc)
        {
            return quant.Type switch
            {
                QuantifierExpression.QuantifierType.Optional => EmitTryMatchOptionalQuantifier(quant, out continuePartial, out backtrackFunc),
                QuantifierExpression.QuantifierType.ZeroOrMore => EmitTryMatchZeroOrMoreQuantifier(quant, out continuePartial, out backtrackFunc),
                QuantifierExpression.QuantifierType.OneOrMore => EmitTryMatchOneOrMoreQuantifier(quant, out continuePartial, out backtrackFunc),
                _ => throw new NotImplementedException(),
            };
        }

        private IEnumerable<int> EmitTryMatchOneOrMoreQuantifier(QuantifierExpression quant, out IEnumerable<int> continuePartial, out int? backtrackFunc)
        {
            throw new NotImplementedException();
            // TODO: implement this with backtracking

            /*var firstFail = EmitTryMatchExpression(quant.Target, out var cont);
            RepairPartialJump(cont, Current);

            var repeatFail = EmitTryMatchZeroOrMoreQuantifier(quant, out cont);
            RepairPartialJump(repeatFail, Current);
            RepairPartialJump(cont, Current);

            continuePartial = Enumerable.Empty<int>();
            return firstFail;*/
        }

        private IEnumerable<int> EmitTryMatchZeroOrMoreQuantifier(QuantifierExpression quant, out IEnumerable<int> continuePartial, out int? backtrackFunc)
        {
            throw new NotImplementedException();
            // TODO: implement this with backtracking

            /*
            var matchTarget = instructions.Count;
            var failed = EmitTryMatchExpression(quant.Target, out var cont);
            RepairPartialJump(cont, instructions.Count);

            EmitJump(RegexInterpreter.Instruction.Jump, matchTarget);
            RepairPartialJump(failed, instructions.Count);

            continuePartial = Enumerable.Empty<int>();
            return Enumerable.Empty<int>();
            */
        }

        private IEnumerable<int> EmitTryMatchOptionalQuantifier(QuantifierExpression quant, out IEnumerable<int> continuePartial, out int? backtrackFunc)
        {
            var matchCounterLocal = LocalCount++;
            Emit(RegexInterpreter.Instruction.PushLocal, matchCounterLocal);
            var jumpPartial = EmitPartialJump(RegexInterpreter.Instruction.Jump);

            var prevGreedyQuantifier = lastGreedyQuantifierBacktraceLoc;
            backtrackFunc = lastGreedyQuantifierBacktraceLoc = Current;
            Emit(RegexInterpreter.Instruction.PopPos);
            EmitJumpTriple(RegexInterpreter.Instruction.DecLocalOrPopJump, matchCounterLocal, prevGreedyQuantifier);
            var decToZeroPartial = EmitPartialJumpTriple(RegexInterpreter.Instruction.DecLocalOrPopJump, matchCounterLocal);
            //var decToZeroPartial = EmitPartialJumpTriple(RegexInterpreter.Instruction.DecLocalOrPopJump, matchCounterLocal);
            var callPartial = EmitPartialJump(RegexInterpreter.Instruction.Call);
            // = EmitPartialJumpTriple(RegexInterpreter.Instruction.JumpIfLocalZero, matchCounterLocal);
            Emit(RegexInterpreter.Instruction.IncLocal, matchCounterLocal);
            var ifNoBacktrack = Current;
            Emit(RegexInterpreter.Instruction.Return);
            RepairPartialJump(decToZeroPartial, Current);
            Emit(RegexInterpreter.Instruction.PushPos);
            var continueJump = EmitPartialJump(RegexInterpreter.Instruction.Jump);

            RepairPartialJump(jumpPartial, Current);
            Emit(RegexInterpreter.Instruction.PushPos);
            Emit(RegexInterpreter.Instruction.IncLocal, matchCounterLocal);
            var failed = EmitTryMatchExpression(quant.Target, out var cont, out var backtrack);
            RepairPartialJump(callPartial, backtrack ?? ifNoBacktrack);
            RepairPartialJump(cont, Current);
            Emit(RegexInterpreter.Instruction.IncLocal, matchCounterLocal);

            continuePartial = failed.Append(continueJump);

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
            Emit((ushort)GetJumpArg(Current, target));
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
            Emit((ushort)GetJumpArg(Current, target));
        }
        private int EmitPartialJumpIfMatch(CharacterGroupExpression group)
        {
            EmitJumpIfMatch(group, 0);
            return Current - 1;
        }
        private int EmitPartialJumpIfNotMatch(CharacterGroupExpression group)
        {
            EmitJumpIfNotMatch(group, 0);
            return Current - 1;
        }
        private void EmitJump(RegexInterpreter.Instruction insn, int target)
            => Emit(insn, (ushort)GetJumpArg(Current + 1, target));
        private void EmitJumpTriple(RegexInterpreter.Instruction insn, ushort part, int target)
        {
            Emit(insn, part);
            Emit((ushort)GetJumpArg(Current, target));
        }
        private int EmitPartialJumpTriple(RegexInterpreter.Instruction insn, ushort part)
        {
            EmitJumpTriple(insn, part, 0);
            return Current - 1;
        }
        private int EmitPartialJump(RegexInterpreter.Instruction insn)
        {
            EmitJump(insn, 0);
            return Current - 1;
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
