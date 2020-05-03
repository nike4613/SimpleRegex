using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimpleRegex
{
    internal class RegexInterpreter
    {
        public enum Instruction : ushort
        {
            Nop, Match, Reject, Jump, 
            JumpIfCharIs, JumpIfCharMatches,
            JumpIfCharIsNot, JumpIfCharNotMatches,
            Advance, Backtrack,
            JumpIfOutOfBounds,
            PushPos, PopPos,
            Call, Return,

            PushLocal, IncLocal, PopLocal, DecLocalOrPopJump,
            JumpIfLocalZero,
        }

        private readonly IReadOnlyList<ushort> instructions;
        private readonly IReadOnlyList<CharacterGroupExpression> characterGroups;
        internal int localCount;

        public RegexInterpreter(IReadOnlyList<ushort> instructions, IReadOnlyList<CharacterGroupExpression> characterGroups)
        {
            this.instructions = instructions;
            this.characterGroups = characterGroups;
        }

        public Match? MatchOn(string text, int startAt)
        {
            if (text is null)
                throw new ArgumentNullException("String is null", nameof(text));
            if (startAt < 0 || startAt > text.Length)
                throw new ArgumentException("Start position outside of range of string", nameof(text));

            var positions = new Stack<int>();

            var callstack = new Stack<int>();

            var locals = new Stack<int>[localCount];

            var insns = instructions.ToArray();
            int iptr = 0;

            int charPos = startAt;

            while (iptr >= 0 && iptr < insns.Length)
            {
                switch ((Instruction)insns[iptr++])
                {
                    case Instruction.Nop: continue;
                    case Instruction.Match:
                        return Match.FromOffsets(positions.Last(), charPos);
                    case Instruction.Reject:
                        return null;
                    case Instruction.PushPos:
                        {
                            positions.Push(charPos);
                            continue;
                        }
                    case Instruction.PopPos:
                        {
                            charPos = positions.Pop();
                            continue;
                        }
                    case Instruction.Advance:
                        {
                            charPos++;
                            continue;
                        }
                    case Instruction.Backtrack:
                        {
                            charPos--;
                            continue;
                        }
                    case Instruction.Jump:
                        {
                            var target = (short)insns[iptr++];
                            iptr += target;
                            continue;
                        }
                    case Instruction.Call:
                        {
                            var target = (short)insns[iptr++];
                            callstack.Push(iptr);
                            iptr += target;
                            continue;
                        }
                    case Instruction.Return:
                        {
                            if (callstack.Count > 0) // explicitly make this a non-erroring no-op if not in a call
                                iptr = callstack.Pop();
                            continue;
                        }
                    case Instruction.JumpIfOutOfBounds:
                        {
                            var target = (short)insns[iptr++];
                            if (charPos < 0 || charPos >= text.Length)
                                iptr += target;
                            continue;
                        }
                    case Instruction.JumpIfCharIs:
                        {
                            var compareTo = (char)insns[iptr++];
                            var target = (short)insns[iptr++];
                            if (text[charPos] == compareTo)
                                iptr += target;
                            continue;
                        }
                    case Instruction.JumpIfCharIsNot:
                        {
                            var compareTo = (char)insns[iptr++];
                            var target = (short)insns[iptr++];
                            if (text[charPos] != compareTo)
                                iptr += target;
                            continue;
                        }
                    case Instruction.JumpIfCharMatches:
                        {
                            var groupIndex = insns[iptr++];
                            var target = (short)insns[iptr++];
                            if (groupIndex >= characterGroups.Count)
                                throw new InvalidOperationException("Bytecode accesses invalid character group index");
                            if (MatchesGroup(text[charPos], characterGroups[groupIndex]))
                                iptr += target;
                            continue;
                        }
                    case Instruction.JumpIfCharNotMatches:
                        {
                            var groupIndex = insns[iptr++];
                            var target = (short)insns[iptr++];
                            if (groupIndex >= characterGroups.Count)
                                throw new InvalidOperationException("Bytecode accesses invalid character group index");
                            if (!MatchesGroup(text[charPos], characterGroups[groupIndex]))
                                iptr += target;
                            continue;
                        }

                    case Instruction.PushLocal:
                        {
                            var index = insns[iptr++];
                            if (locals[index] == null)
                                locals[index] = new Stack<int>(1);
                            locals[index].Push(0);
                            continue;
                        }
                    case Instruction.IncLocal:
                        {
                            var index = insns[iptr++];
                            var stack = locals[index];
                            stack.Push(stack.Pop() + 1);
                            continue;
                        }
                    case Instruction.PopLocal:
                        {
                            var index = insns[iptr++];
                            locals[index].Pop();
                            continue;
                        }
                    case Instruction.DecLocalOrPopJump:
                        {
                            var index = insns[iptr++];
                            var target = (short)insns[iptr++];

                            var stack = locals[index];
                            var value = stack.Pop() - 1;
                            if (value >= 0) stack.Push(value);
                            else iptr += target;
                            continue;
                        }
                    case Instruction.JumpIfLocalZero:
                        {
                            var index = insns[iptr++];
                            var target = (short)insns[iptr++];

                            var stack = locals[index];
                            if (stack.Peek() == 0)
                                iptr += target;
                            continue;
                        }

                    default:
                        throw new InvalidOperationException("Unknown opcode");
                }
            }

            throw new InvalidOperationException("Reached end of bytecode");
        }

        private static bool MatchesGroup(char c, CharacterGroupExpression group)
        {
            return group.Contains(c);
        }

        public string Disassembly => Disassemble();

        public string Disassemble()
        {
            var sb = new StringBuilder();
            int pos = 0;
            var insns = instructions.ToArray();
            while (pos < insns.Length)
                sb.Append($"{pos:X4}: ").AppendLine(DisassembleInsn(insns, ref pos));
            return sb.ToString();
        }

        private string DisassembleInsn(ushort[] insns, ref int pos)
            => (Instruction)insns[pos++] switch // TODO: report this notification as a bug
            {
                Instruction.Nop => nameof(Instruction.Nop),
                Instruction.Match => nameof(Instruction.Match),
                Instruction.Reject => nameof(Instruction.Reject),
                Instruction.Advance => nameof(Instruction.Advance),
                Instruction.Backtrack => nameof(Instruction.Backtrack),
                Instruction.PushPos => nameof(Instruction.PushPos),
                Instruction.PopPos => nameof(Instruction.PopPos),
                Instruction.Return => nameof(Instruction.Return),
                Instruction.Jump => DisassembleJumpInsn(nameof(Instruction.Jump) + "\t", insns, ref pos),
                Instruction.Call => DisassembleJumpInsn(nameof(Instruction.Call) + "\t", insns, ref pos),
                Instruction.JumpIfOutOfBounds => DisassembleJumpInsn(nameof(Instruction.JumpIfOutOfBounds), insns, ref pos),
                Instruction.JumpIfCharIs => DisassembleJumpIfCharIs(nameof(Instruction.JumpIfCharIs), insns, ref pos),
                Instruction.JumpIfCharIsNot => DisassembleJumpIfCharIs(nameof(Instruction.JumpIfCharIsNot), insns, ref pos),
                Instruction.JumpIfCharMatches => DisassembleJumpIfCharMatches(nameof(Instruction.JumpIfCharMatches), insns, ref pos),
                Instruction.JumpIfCharNotMatches => DisassembleJumpIfCharMatches(nameof(Instruction.JumpIfCharNotMatches), insns, ref pos),
                Instruction.PushLocal => DisassembleLocalInsn(nameof(Instruction.PushLocal) + "\t", insns, ref pos),
                Instruction.IncLocal => DisassembleLocalInsn(nameof(Instruction.IncLocal) + "\t", insns, ref pos),
                Instruction.PopLocal => DisassembleLocalInsn(nameof(Instruction.PopLocal) + "\t", insns, ref pos),
                Instruction.DecLocalOrPopJump => DisassembleLocalJump(nameof(Instruction.DecLocalOrPopJump), insns, ref pos),
                Instruction.JumpIfLocalZero => DisassembleLocalJump(nameof(Instruction.JumpIfLocalZero), insns, ref pos),
                _ => "<unknown opcode>",
            };

        private string DisassembleLocalInsn(string name, ushort[] insns, ref int pos)
        {
            if (pos >= insns.Length) return Partial(name);
            var local = insns[pos++];
            return $"{name}\t{local:X4}" + (local < localCount ? "" : "<invalid local>");
        }
        private string DisassembleLocalJump(string name, ushort[] insns, ref int pos)
        {
            if (pos >= insns.Length) return Partial(name);
            var local = insns[pos++];
            if (pos >= insns.Length) return Partial(name, local.ToString("X4"));
            var target = (short)insns[pos++];
            return $"{name}\t{local:X4} {JumpTarget(target, pos)}" + (local < localCount ? "" : "<invalid local>");
        }
        private static string DisassembleJumpInsn(string name, ushort[] insns, ref int pos)
        {
            if (pos >= insns.Length) return Partial(name);
            var target = (short)insns[pos++];
            return $"{name}\t{JumpTarget(target, pos)}";
        }
        private static string DisassembleJumpIfCharIs(string name, ushort[] insns, ref int pos)
        {
            if (pos >= insns.Length) return Partial(name);
            var compareTo = (char)insns[pos++];
            if (pos >= insns.Length) return Partial(name, $"'{compareTo}'");
            var target = (short)insns[pos++];
            return $"{name} '{compareTo}'\t{JumpTarget(target, pos)}";
        }
        private string DisassembleJumpIfCharMatches(string name, ushort[] insns, ref int pos)
        {
            if (pos >= insns.Length) return Partial(name);
            var groupIndex = insns[pos++];
            var groupStr = "<invalid group>";
            if (groupIndex < characterGroups.Count)
                groupStr = characterGroups[groupIndex].ToString();
            if (pos >= insns.Length) return Partial(name, groupStr);
            var target = (short)insns[pos++];
            return $"{name} {groupStr}\t{JumpTarget(target, pos)}";
        }

        private static string JumpTarget(int target, int pos)
            => $"{target:+0;-0;\0} [{pos + target:X4}]";
        private static string Partial(params string[] parts)
            => $"<partial {string.Join(" ", parts)}>";
    }
}
