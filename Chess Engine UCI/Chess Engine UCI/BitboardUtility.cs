using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.Intrinsics.X86;

public class BitboardUtility
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

    public static int OccupiedSquaresCount(ulong bitboard)
    {
        return BitOperations.PopCount(bitboard);
    }
}
