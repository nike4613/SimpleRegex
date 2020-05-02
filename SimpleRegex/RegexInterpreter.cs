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
            StorePos, LoadPos,
        }

        private readonly IReadOnlyList<ushort> instructions;
        private readonly IReadOnlyList<CharacterGroupExpression> characterGroups;
        public ushort PosArraySize { get; set; }

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

            var positions = new int[PosArraySize];

            var insns = instructions.ToArray();
            int iptr = 0;

            int charPos = startAt;

            while (iptr >= 0 && iptr < insns.Length)
            {
                switch ((Instruction)insns[iptr++])
                {
                    case Instruction.Nop: continue;
                    case Instruction.Match:
                        return Match.FromOffsets(positions[0] /* this is just known */, charPos);
                    case Instruction.Reject:
                        return null;
                    case Instruction.StorePos:
                        {
                            var index = insns[iptr++];
                            if (index >= positions.Length)
                                throw new InvalidOperationException("Bytecode accesses invalid position");
                            positions[index] = charPos;
                            continue;
                        }
                    case Instruction.LoadPos:
                        {
                            var index = insns[iptr++];
                            if (index >= positions.Length)
                                throw new InvalidOperationException("Bytecode accesses invalid position");
                            charPos = positions[index];
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
                Instruction.StorePos => DisassemblePositionInsn(nameof(Instruction.StorePos), insns, ref pos),
                Instruction.LoadPos => DisassemblePositionInsn(nameof(Instruction.LoadPos), insns, ref pos),
                Instruction.Jump => DisassembleJumpInsn(nameof(Instruction.Jump) + "\t", insns, ref pos),
                Instruction.JumpIfOutOfBounds => DisassembleJumpInsn(nameof(Instruction.JumpIfOutOfBounds), insns, ref pos),
                Instruction.JumpIfCharIs => DisassembleJumpIfCharIs(nameof(Instruction.JumpIfCharIs), insns, ref pos),
                Instruction.JumpIfCharIsNot => DisassembleJumpIfCharIs(nameof(Instruction.JumpIfCharIsNot), insns, ref pos),
                Instruction.JumpIfCharMatches => DisassembleJumpIfCharMatches(nameof(Instruction.JumpIfCharMatches), insns, ref pos),
                Instruction.JumpIfCharNotMatches => DisassembleJumpIfCharMatches(nameof(Instruction.JumpIfCharNotMatches), insns, ref pos),
                _ => "<unknown opcode>",
            };

        private string DisassemblePositionInsn(string name, ushort[] insns, ref int pos)
        {
            if (pos >= insns.Length) return $"<partial {name}>";
            var loc = insns[pos++];
            return $"{name}\t{loc:X4}{(loc >= PosArraySize ? " <invalid position>" : "")}";
        }
        private static string DisassembleJumpInsn(string name, ushort[] insns, ref int pos)
        {
            if (pos >= insns.Length) return $"<partial {name}>";
            var target = (short)insns[pos++];
            return $"{name}\t{target:+0;-0;\0} [{pos + target:X4}]";
        }
        private static string DisassembleJumpIfCharIs(string name, ushort[] insns, ref int pos)
        {
            if (pos >= insns.Length) return $"<partial {name}>";
            var compareTo = (char)insns[pos++];
            if (pos >= insns.Length) return $"<partial {name} '{compareTo}'>";
            var target = (short)insns[pos++];
            return $"{name} '{compareTo}'\t{target:+0;-0;\0} [{pos + target:X4}]";
        }
        private string DisassembleJumpIfCharMatches(string name, ushort[] insns, ref int pos)
        {
            if (pos >= insns.Length) return $"<partial {name}>";
            var groupIndex = insns[pos++];
            var groupStr = "<invalid group>";
            if (groupIndex < characterGroups.Count)
                groupStr = characterGroups[groupIndex].ToString();
            if (pos >= insns.Length) return $"<partial {name} {groupStr}>";
            var target = (short)insns[pos++];
            return $"{name} {groupStr}\t{target:+0;-0;\0} [{pos + target:X4}]";
        }
    }
}
