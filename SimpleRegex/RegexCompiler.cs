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
        private readonly List<string> commentStrings = new List<string>();
        private readonly RegexInterpreter interpreter;

        private ushort LocalCount
        {
            get => (ushort)interpreter.localCount;
            set => interpreter.localCount = value;
        }
        private ushort GroupCount
        {
            get => (ushort)interpreter.groupCount;
            set => interpreter.groupCount = value;
        }
        private int Current => instructions.Count;
        private int lastBacktraceLoc = -1;

        public RegexCompiler()
        {
            interpreter = new RegexInterpreter(instructions, charGroups, commentStrings);
        }

        public RegexCompiler Compile(Expression expr)
        {
#if DEBUG
            commentStrings.Add("}");
#endif

            var startJump = EmitPartialJump(RegexInterpreter.Instruction.Jump);
            var rejectPos = Current;
            Emit(RegexInterpreter.Instruction.Reject);
            var advanceIterStart = Current;
            lastBacktraceLoc = advanceIterStart;
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
        {
#if DEBUG
            Emit(RegexInterpreter.Instruction.Comment, (ushort)commentStrings.Count);
            commentStrings.Add($"{{ // {expr}");
#endif
            var returnValue = expr switch
            {
                GroupExpression group => EmitTryMatchGroupExpression(group, out continuePartial, out backtrackFunc),
                CharacterGroupExpression chr => EmitTryMatchCharacterGroup(chr, out continuePartial, out backtrackFunc),
                QuantifierExpression quant => EmitTryMatchQuantifier(quant, out continuePartial, out backtrackFunc),
                AnchorExpression anchor => EmitTryMatchAnchor(anchor, out continuePartial, out backtrackFunc),
                AlternationExpression alt => EmitTryMatchAlternation(alt, out continuePartial, out backtrackFunc),
                _ => throw new NotImplementedException(),
            };
#if DEBUG
            Emit(RegexInterpreter.Instruction.Comment, 0);
#endif
            return returnValue;
        }

        private IEnumerable<int> EmitTryMatchGroupExpression(GroupExpression group, out IEnumerable<int> continuePartial, out int? backtrackFunc)
        {
            var failureJumps = Enumerable.Empty<int>();
            continuePartial = Enumerable.Empty<int>();
            ushort groupIndex = ushort.MaxValue;

            if (group.IsCaptureGroup)
            {
                groupIndex = GroupCount++;
                Emit(RegexInterpreter.Instruction.StartGroup, groupIndex);
            }

            if (group.Count == 1)
            {
                failureJumps = EmitTryMatchExpression(group.First(), out continuePartial, out backtrackFunc);
            }
            else if (group.Count == 0)
            {
                backtrackFunc = null;
            }
            else
            {
                var backtracks = new int?[group.Count];
                for (int i = 0; i < group.Count; i++)
                {
                    var lastBt = lastBacktraceLoc;
                    var failure = EmitTryMatchExpression(group[i], out continuePartial, out backtracks[i]);
                    RepairPartialJump(failure, /*onFail*/ lastBt);
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
            }

            if (group.IsCaptureGroup)
            {
                RepairPartialJump(continuePartial, Current);
                continuePartial = Enumerable.Empty<int>();
                Emit(RegexInterpreter.Instruction.EndGroup, groupIndex);
            }

            return failureJumps;
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

        private IEnumerable<int> EmitTryMatchAnchor(AnchorExpression anchor, out IEnumerable<int> continuePartial, out int? backtrackFunc)
        {
            var check = EmitPartialJump(anchor.IsStart ? RegexInterpreter.Instruction.JumpIfNotAtStart
                                                       : RegexInterpreter.Instruction.JumpIfNotAtEnd);
            continuePartial = Enumerable.Empty<int>();
            backtrackFunc = null;
            return new[] { check };
        }

        private IEnumerable<int> EmitTryMatchAlternation(AlternationExpression alt, out IEnumerable<int> continuePartial, out int? backtrackFunc)
        {
            var indexLocal = LocalCount++;
            Emit(RegexInterpreter.Instruction.PushLocal, indexLocal);
            Emit(RegexInterpreter.Instruction.PushPos);
            var jumpPartial = EmitPartialJump(RegexInterpreter.Instruction.Jump);

            var startLastGreedy = lastBacktraceLoc;

            // emit BacktraceFunc
            var backtraceFuncJumpTargets = new int[alt.Count];
            var backtraceCallPartials = new int[alt.Count];
            var backtraceReturnPartials = new int[alt.Count];
            for (var i = 0; i < alt.Count; i++)
            {
                backtraceFuncJumpTargets[i] = Current;
                backtraceCallPartials[i] = EmitPartialJump(RegexInterpreter.Instruction.Call);
                backtraceReturnPartials[i] = EmitPartialJump(RegexInterpreter.Instruction.Jump);
            }
            backtrackFunc = Current;
            EmitSwitch(indexLocal, backtraceFuncJumpTargets);
            RepairPartialJump(backtraceReturnPartials, Current);
            Emit(RegexInterpreter.Instruction.PopLocal, indexLocal);
            var ifNoBacktrack = Current;
            Emit(RegexInterpreter.Instruction.Return);

            // emit OnBacktraceThrough (when match fails within alt)
            var backtraceThroughJumpTargets = new int[alt.Count];
            var backtraceThroughContinuePartials = new int[alt.Count];
            backtraceThroughContinuePartials[0] = -1;
            for (var i = 0; i < alt.Count; i++)
            {
                backtraceThroughJumpTargets[i] = Current;
                if (i + 1 < alt.Count)
                { // normal case
                    Emit(RegexInterpreter.Instruction.PushPos);
                    Emit(RegexInterpreter.Instruction.IncLocal, indexLocal);
                    backtraceThroughContinuePartials[i + 1] = EmitPartialJump(RegexInterpreter.Instruction.Jump);
                }
                else
                { // last element
                    Emit(RegexInterpreter.Instruction.PopLocal, indexLocal);
                    EmitJump(RegexInterpreter.Instruction.Jump, startLastGreedy);
                }
            }
            var onBacktraceThrough = Current;
            Emit(RegexInterpreter.Instruction.PopPos);
            EmitSwitch(indexLocal, backtraceThroughJumpTargets);

            // emit main execution
            continuePartial = Enumerable.Empty<int>();
            RepairPartialJump(jumpPartial, Current);
            var tailLastGreedyQuantifiers = new int[alt.Count];
            for (var i = 0; i < alt.Count; i++)
            {
                lastBacktraceLoc = onBacktraceThrough;
                RepairPartialJump(backtraceThroughContinuePartials[i], Current);
                var failure = EmitTryMatchExpression(alt[i], out var cont, out var backtrack);
                var trailingJump = EmitPartialJump(RegexInterpreter.Instruction.Jump);
                RepairPartialJump(backtraceCallPartials[i], backtrack ?? ifNoBacktrack);
                RepairPartialJump(failure, onBacktraceThrough);

                tailLastGreedyQuantifiers[i] = lastBacktraceLoc;

                continuePartial = continuePartial.Concat(cont).Append(trailingJump);
            }

            var onBacktrace = Current;
            EmitSwitch(indexLocal, tailLastGreedyQuantifiers);
            EmitJump(RegexInterpreter.Instruction.Jump, startLastGreedy);
            lastBacktraceLoc = onBacktrace;

            return Enumerable.Empty<int>();
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
            var jumpPartial = EmitPartialJump(RegexInterpreter.Instruction.Jump);

            backtrackFunc = Current;
            var firstCallPartial = EmitPartialJump(RegexInterpreter.Instruction.Call);
            var restCallPartial = EmitPartialJump(RegexInterpreter.Instruction.Call);
            var ifNoBacktrace = Current;
            Emit(RegexInterpreter.Instruction.Return);

            RepairPartialJump(jumpPartial, Current);
            var firstFail = EmitTryMatchExpression(quant.Target, out var cont, out var backtraceFirst);
            RepairPartialJump(cont, Current);
            RepairPartialJump(firstCallPartial, backtraceFirst ?? ifNoBacktrace);

            var repeatFail = EmitTryMatchZeroOrMoreQuantifier(quant, out cont, out var backtraceRest);
            RepairPartialJump(restCallPartial, backtraceRest ?? ifNoBacktrace);

            continuePartial = repeatFail.Concat(cont);
            return firstFail;
        }
        
        private IEnumerable<int> EmitTryMatchZeroOrMoreQuantifier(QuantifierExpression quant, out IEnumerable<int> continuePartial, out int? backtrackFunc)
        {
            var matchCounterLocal = LocalCount++;
            Emit(RegexInterpreter.Instruction.PushLocal, matchCounterLocal);
            var jumpPartial = EmitPartialJump(RegexInterpreter.Instruction.Jump);

            // emit backtrack func
            backtrackFunc = Current;
            var backtrackJumpIfZeroPartial = EmitPartialJumpTriple(RegexInterpreter.Instruction.DecLocalOrPopJump, matchCounterLocal);
            var backtrackCallPartial = EmitPartialJump(RegexInterpreter.Instruction.Call);
            Emit(RegexInterpreter.Instruction.PopPos);
            EmitJump(RegexInterpreter.Instruction.Jump, backtrackFunc.Value);
            RepairPartialJump(backtrackJumpIfZeroPartial, Current);
            var ifNoBacktrack = Current;
            Emit(RegexInterpreter.Instruction.Return);

            if (!quant.IsLazy)
            {
                // emit backtrace through func (this is literally copied from ?'s impl, because this does effectively the same thing)
                var prevGreedy = lastBacktraceLoc;
                lastBacktraceLoc = Current;
                var backtraceThroughJumpIfZeroPartial = EmitPartialJumpTriple(RegexInterpreter.Instruction.DecLocalOrPopJump, matchCounterLocal);
                // count is nonzero ; if this is called, then the content has already fully unwound
                Emit(RegexInterpreter.Instruction.PopPos);
                var continuePartialJump = EmitPartialJump(RegexInterpreter.Instruction.Jump);
                // count was zero and is popped, so jump to the last qualifier
                RepairPartialJump(backtraceThroughJumpIfZeroPartial, prevGreedy);


                RepairPartialJump(jumpPartial, Current);
                var matchTarget = Current;
                Emit(RegexInterpreter.Instruction.IncLocal, matchCounterLocal);
                Emit(RegexInterpreter.Instruction.PushPos);
                var failed = EmitTryMatchExpression(quant.Target, out var cont, out var backtrack);
                RepairPartialJump(backtrackCallPartial, backtrack ?? ifNoBacktrack);
                RepairPartialJump(cont, Current);

                EmitJump(RegexInterpreter.Instruction.Jump, matchTarget);

                continuePartial = failed.Append(continuePartialJump);
            }
            else
            {
                var prevGreedy = lastBacktraceLoc;
                var onMatchFailure = Current;
                // count is nonzero, so we've already tried to match and it either failed or we need more
                EmitJump(RegexInterpreter.Instruction.Call, backtrackFunc.Value);
                EmitJump(RegexInterpreter.Instruction.Jump, prevGreedy);

                // emit backtrace through func, which in this case just tries to add a match
                lastBacktraceLoc = Current;
                Emit(RegexInterpreter.Instruction.IncLocal, matchCounterLocal);
                Emit(RegexInterpreter.Instruction.PushPos);
                var failed = EmitTryMatchExpression(quant.Target, out var cont, out var backtrack);
                RepairPartialJump(backtrackCallPartial, backtrack ?? ifNoBacktrack);
                RepairPartialJump(failed, onMatchFailure);

                continuePartial = cont.Append(jumpPartial);
            }

            return Enumerable.Empty<int>();
        }

        private IEnumerable<int> EmitTryMatchOptionalQuantifier(QuantifierExpression quant, out IEnumerable<int> continuePartial, out int? backtrackFunc)
        {
            var matchCounterLocal = LocalCount++;
            Emit(RegexInterpreter.Instruction.PushLocal, matchCounterLocal);
            var jumpPartial = EmitPartialJump(RegexInterpreter.Instruction.Jump);

            // emit backtrack func
            backtrackFunc = Current;
            var backtrackJumpIfZeroPartial = EmitPartialJumpTriple(RegexInterpreter.Instruction.DecLocalOrPopJump, matchCounterLocal);
            Emit(RegexInterpreter.Instruction.PopLocal, matchCounterLocal);
            var backtrackCallPartial = EmitPartialJump(RegexInterpreter.Instruction.Call);
            RepairPartialJump(backtrackJumpIfZeroPartial, Current);
            Emit(RegexInterpreter.Instruction.PopPos);
            var ifNoBacktrack = Current;
            Emit(RegexInterpreter.Instruction.Return);

            if (!quant.IsLazy)
            { // the nonlazy form
                // emit backtrace through func
                var prevGreedy = lastBacktraceLoc;
                lastBacktraceLoc = Current;
                var backtraceThroughJumpIfZeroPartial = EmitPartialJumpTriple(RegexInterpreter.Instruction.DecLocalOrPopJump, matchCounterLocal);
                // count is nonzero ; if this is called, then the content has already fully unwound
                Emit(RegexInterpreter.Instruction.PopPos);
                var continuePartialJump = EmitPartialJump(RegexInterpreter.Instruction.Jump);
                // count was zero and is popped, so jump to the last qualifier
                RepairPartialJump(backtraceThroughJumpIfZeroPartial, prevGreedy);

                RepairPartialJump(jumpPartial, Current);
                Emit(RegexInterpreter.Instruction.PushPos);
                // inc on start, so that when processing the body, it follows the completed path
                Emit(RegexInterpreter.Instruction.IncLocal, matchCounterLocal);
                var failed = EmitTryMatchExpression(quant.Target, out var cont, out var backtrack);
                RepairPartialJump(backtrackCallPartial, backtrack ?? ifNoBacktrack);

                continuePartial = failed.Concat(cont).Append(continuePartialJump);
            }
            else
            {
                // emit backtrace through func
                var prevGreedy = lastBacktraceLoc;
                lastBacktraceLoc = Current;
                var jumpIfZero = EmitPartialJumpTriple(RegexInterpreter.Instruction.JumpIfLocalZero, matchCounterLocal);
                // count is nonzero, so we've already tried to match and it either failed or we need more
                Emit(RegexInterpreter.Instruction.PopLocal, matchCounterLocal);
                EmitJump(RegexInterpreter.Instruction.Jump, prevGreedy);

                RepairPartialJump(jumpIfZero, Current);
                Emit(RegexInterpreter.Instruction.PushPos);
                Emit(RegexInterpreter.Instruction.IncLocal, matchCounterLocal);
                var failed = EmitTryMatchExpression(quant.Target, out var cont, out var backtrack);
                RepairPartialJump(backtrackCallPartial, backtrack ?? ifNoBacktrack);

                continuePartial = failed.Concat(cont).Append(jumpPartial);
            }

            return Enumerable.Empty<int>();
        }

        #region Emit helpers
        private void EmitSwitch(ushort localIndex, int[] targets)
        {
            Emit(RegexInterpreter.Instruction.Switch, localIndex);
            Emit((ushort)targets.Length);
            var switchEnd = Current + targets.Length;
            foreach (var target in targets)
                Emit((ushort)GetJumpArg(switchEnd - 1, target));
        }
        private void EmitJumpIfNotMatch(CharacterGroupExpression group, int target)
        {
            if (group is SingleCharacterGroup single)
            {
                Emit(RegexInterpreter.Instruction.JumpIfCharIsNot, single.Character);
            }
            else
            {
                Emit(RegexInterpreter.Instruction.JumpIfCharNotMatches, (ushort)charGroups.Count);
                charGroups.Add(group);
            }
            Emit((ushort)GetJumpArg(Current, target));
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
