using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Burst.Intrinsics;

public class BitboardUtility
{
    public static int FirstSquareIndex(ulong bitboard)
    {
        if (bitboard == 0) return 0;
        return math.tzcnt(bitboard);
    }

    public static int LastSquareIndex(ulong bitboard)
    {
        if (bitboard == 0) return 0;
        return 63 - math.lzcnt(bitboard);
    }

    public static int OccupiedSquaresCount(ulong bitboard)
    {
        return X86.Popcnt.popcnt_u64(bitboard);
    }

    public static ulong ParallelBitExtract(ulong bitboard, ulong mask)
    {
        return X86.Bmi2.pext_u64(bitboard, mask);
    }
}
