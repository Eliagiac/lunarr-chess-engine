using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.Intrinsics.X86;

namespace Utilities
{
    public class Bitboard
    {
        public static int FirstSquareIndex(ulong bitboard)
        {
            if (bitboard == 0) return 0;
            return BitOperations.TrailingZeroCount(bitboard);
        }

        public static int LastSquareIndex(ulong bitboard)
        {
            if (bitboard == 0) return 0;
            return 63 - BitOperations.LeadingZeroCount(bitboard);
        }
        
        /// <summary>The amount of pieces in a bitboard (or generically the amount of positive bits).</summary>
        public static int PieceCount(ulong bitboard)
        {
            return BitOperations.PopCount(bitboard);
        }

        public static ulong ParallelBitExtract(ulong bitboard, ulong mask)
        {
            if (!Bmi2.X64.IsSupported) Console.WriteLine("BMI2 not supported!");
            return Bmi2.X64.ParallelBitExtract(bitboard, mask);
        }

        public static ulong MultiplyHigh64Bits(ulong a, ulong b)
        {
            return (ulong)(((UInt128)a * (UInt128)b) >> 64);
        }
    }
}
