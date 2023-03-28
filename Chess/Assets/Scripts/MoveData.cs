using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;

public class MoveData
{
    public static Dictionary<int, ulong[,]> Moves;

    public static ulong[,] Masks;
    public static Dictionary<int, ulong[,]> SpecificMasks;

    public static Dictionary<int, MagicBitboard[]> MagicBitboards;

    private static ulong[] RookTable;
    private static ulong[] BishopTable;

    private static readonly (int direction, int offset)[] BishopDirections = new[] { (7, -1), (-7, 1), (9, 1), (-9, -1) };
    private static readonly (int direction, int offset)[] RookDirections = new[] { (1, 1), (-1, -1), (8, 0), (-8, 0) };
    private static readonly (int direction, int offset)[] KnightDirections = new[] { (6, -2), (-6, 2), (15, -1), (-15, 1), (17, 1), (-17, -1), (10, 2), (-10, -2) };
    private static readonly (int direction, int offset)[] KingDirections = new[] { (1, 1), (-1, -1), (7, -1), (-7, 1), (8, 0), (-8, 0), (9, 1), (-9, -1) };
    private static readonly (int direction, int offset)[] PawnDirections = new[] { (8, 0), (-8, 0) };
    private static readonly (int direction, int offset)[] PawnTakingDirections = new[] { (7, -1), (9, 1), (-7, 1), (-9, -1) };


    public static void ComputeMoveData()
    {
        Moves = new()
        {
            [Piece.King] = new ulong[64, 1],
            [Piece.Pawn] = new ulong[64, 6],
            [Piece.Knight] = new ulong[64, 1],
            [Piece.Bishop] = new ulong[64, 4],
            [Piece.Rook] = new ulong[64, 4],
            [Piece.Queen] = new ulong[64, 8]
        };

        for (int i = 0; i < 64; i++)
        {
            for (int direction = 0; direction < 4; direction++)
            {
                int target = i + BishopDirections[direction].direction;
                int distance = 1;

                while (Board.CheckBoundaries(target) && Board.GetFile(target) == Board.GetFile(i) + BishopDirections[direction].offset * distance)
                {
                    Moves[Piece.Bishop][i, direction] |= 1UL << target;
                    Moves[Piece.Queen][i, direction] |= 1UL << target;

                    target += BishopDirections[direction].direction;
                    distance++;
                }
            }

            for (int direction = 0; direction < 4; direction++)
            {
                int target = i + RookDirections[direction].direction;
                int distance = 1;
                while (Board.CheckBoundaries(target) && Board.GetFile(target) == Board.GetFile(i) + RookDirections[direction].offset * distance)
                {
                    Moves[Piece.Rook][i, direction] |= 1UL << target;
                    Moves[Piece.Queen][i, direction + 4] |= 1UL << target;

                    target += RookDirections[direction].direction;
                    distance++;
                }
            }

            foreach (var direction in KnightDirections)
            {
                int target = i + direction.direction;
                if (Board.CheckBoundaries(target) && Board.GetFile(target) == Board.GetFile(i) + direction.offset)
                {
                    Moves[Piece.Knight][i, 0] |= 1UL << target;
                }
            }

            foreach (var direction in KingDirections)
            {
                int target = i + direction.direction;
                if (Board.CheckBoundaries(target) && Board.GetFile(target) == Board.GetFile(i) + direction.offset)
                {
                    Moves[Piece.King][i, 0] |= 1UL << target;
                }
            }

            float directionIndex = 0;
            foreach (var direction in PawnDirections)
            {
                int target = i + direction.direction;
                if (Board.CheckBoundaries(target) && Board.GetFile(target) == Board.GetFile(i) + direction.offset)
                {
                    Moves[Piece.Pawn][i, (int)directionIndex] |= 1UL << target;

                    if (Board.GetRank(i) == ((int)directionIndex == 0 ? 1 : 6))
                        Moves[Piece.Pawn][i, (int)directionIndex + 2] |= 1UL << target + direction.direction;
                }

                directionIndex++;
            }

            directionIndex += 2;
            foreach (var direction in PawnTakingDirections)
            {
                int target = i + direction.direction;
                if (Board.CheckBoundaries(target) && Board.GetFile(target) == Board.GetFile(i) + direction.offset)
                {
                    Moves[Piece.Pawn][i, (int)directionIndex] |= 1UL << target;
                }

                directionIndex += 0.5f;
            }
        }
    }

    public static void GenerateDirectionalMasks()
    {
        Masks = new ulong[64, 64];
        SpecificMasks = new()
        {
            [Piece.Bishop] = new ulong[64, 64],
            [Piece.Rook] = new ulong[64, 64],
            [Piece.Queen] = new ulong[64, 64]
        };

        for (int square = 0; square < 64; square++)
        {
            for (int diagonalDirection = 0; diagonalDirection < 4; diagonalDirection++)
            {
                int target = square + BishopDirections[diagonalDirection].direction;
                int distance = 1;

                while (Board.CheckBoundaries(target) && Board.GetFile(target) == Board.GetFile(square) + BishopDirections[diagonalDirection].offset * distance)
                {
                    Masks[square, target] |= (1UL << square) | (1UL << target) | (distance > 1 ? Masks[square, target - BishopDirections[diagonalDirection].direction] : 0);
                    SpecificMasks[Piece.Bishop][square, target] |= (1UL << square) | (1UL << target) | (distance > 1 ? Masks[square, target - BishopDirections[diagonalDirection].direction] : 0);
                    SpecificMasks[Piece.Queen][square, target] |= (1UL << square) | (1UL << target) | (distance > 1 ? Masks[square, target - BishopDirections[diagonalDirection].direction] : 0);

                    target += BishopDirections[diagonalDirection].direction;
                    distance++;
                }
            }

            for (int orthogonalDirection = 0; orthogonalDirection < 4; orthogonalDirection++)
            {
                int target = square + RookDirections[orthogonalDirection].direction;
                int distance = 1;

                while (Board.CheckBoundaries(target) && Board.GetFile(target) == Board.GetFile(square) + RookDirections[orthogonalDirection].offset * distance)
                {
                    Masks[square, target] |= (1UL << square) | (1UL << target) | (distance > 1 ? Masks[square, target - RookDirections[orthogonalDirection].direction] : 0);
                    SpecificMasks[Piece.Rook][square, target] |= (1UL << square) | (1UL << target) | (distance > 1 ? Masks[square, target - RookDirections[orthogonalDirection].direction] : 0);
                    SpecificMasks[Piece.Queen][square, target] |= (1UL << square) | (1UL << target) | (distance > 1 ? Masks[square, target - RookDirections[orthogonalDirection].direction] : 0);

                    target += RookDirections[orthogonalDirection].direction;
                    distance++;
                }
            }
        }
    }

    public static void ComputeMagicBitboards()
    {
        MagicBitboards = new()
        {
            [Piece.Rook] = new MagicBitboard[64],
            [Piece.Bishop] = new MagicBitboard[64]
        };

        RookTable = new ulong[0x19000];
        BishopTable = new ulong[0x1480];

        ComputeMagicBitboard(RookTable, MagicBitboards[Piece.Rook], Piece.Rook);
        ComputeMagicBitboard(BishopTable, MagicBitboards[Piece.Bishop], Piece.Bishop);
    }

    // Translated from Stockfish.
    private static void ComputeMagicBitboard(ulong[] table, MagicBitboard[] magics, int pieceType)
    {
        ulong[] occupancy = new ulong[4096];
        ulong[] reference = new ulong[4096];
        ulong edges;
        ulong b;

        int[] epoch = new int[4096];
        int cnt = 0;
        ulong size = 0;

        for (int s = 0; s < 64; s++)
        {
            // Board edges are not considered in the relevant occupancies
            edges = ((Board.Ranks[0][0] | Board.Ranks[0][7]) & ~Board.Ranks[0][Board.GetRank(s)]) | ((Board.Files[0] | Board.Files[7]) & ~Board.Files[Board.GetFile(s)]);

            // Given a square 's', the mask is the bitboard of sliding attacks from
            // 's' computed on an empty board. The index must be big enough to contain
            // all the attacks for each possible subset of the mask and so is 2 power
            // the number of 1s of the mask. Hence we deduce the size of the shift to
            // apply to the 64 or 32 bits word to get the index.
            magics[s] = new(DiagonalAttacks(pieceType, s, 0) & ~edges, table, 0);
            var m = magics[s];

            // Set the offset for the attacks table of the square. We have individual
            // table sizes for each square with "Fancy Magic Bitboards".
            m.Offset = s == 0 ? 0 : magics[s - 1].Offset + size;

            // Use Carry-Rippler trick to enumerate all subsets of masks[s] and
            // store the corresponding sliding attack bitboard in reference[].
            b = 0;
            size = 0;
            do
            {
                occupancy[size] = b;
                reference[size] = DiagonalAttacks(pieceType, s, b);

                m.Attacks[m.Offset + BitboardUtility.ParallelBitExtract(b, m.Mask)] = reference[size];

                size++;
                b = (b - m.Mask) & m.Mask;
            } while (b != 0);
        }

        ulong DiagonalAttacks(int pieceType, int squareIndex, ulong occupiedSquares)
        {
            ulong attacks = 0;

            // Store moves in each direction individually to identify blockers.
            for (int direction = 0; direction < Moves[pieceType].GetLength(1); direction++)
            {
                ulong maskedBlockers = occupiedSquares & Moves[pieceType][squareIndex, direction];

                // Use bitscanning to find first blocker.
                // Directions at even indexes are always positive, and viceversa.
                int firstBlockerIndex = direction % 2 == 0 ? BitboardUtility.FirstSquareIndex(maskedBlockers) : BitboardUtility.LastSquareIndex(maskedBlockers);

                // Add moves in this direction.
                attacks |= Moves[pieceType][squareIndex, direction];

                // Remove moves in the ray from the first blocker in the same direction (only moves between the piece and the first blocker remain).
                if (maskedBlockers != 0) attacks &= ~Moves[pieceType][firstBlockerIndex, direction];
            }

            return attacks;
        }
    }
}

public class MagicBitboard
{
    public ulong Mask;
    public ulong[] Attacks;
    public ulong Offset;

    public MagicBitboard(ulong mask, ulong[] attacks, ulong offset)
    {
        Mask = mask;
        Attacks = attacks;
        Offset = offset;
    }


    public ulong GetAttacks(ulong occupiedSquares) => Attacks[Offset + BitboardUtility.ParallelBitExtract(occupiedSquares, Mask)];


    public static ulong GetAttacks(int pieceType, int squareIndex, ulong occupiedSquares)
    {
        if (pieceType == Piece.Rook) return MoveData.MagicBitboards[Piece.Rook][squareIndex].GetAttacks(occupiedSquares);
        if (pieceType == Piece.Bishop) return MoveData.MagicBitboards[Piece.Bishop][squareIndex].GetAttacks(occupiedSquares);

        if (pieceType == Piece.Queen) return
                MoveData.MagicBitboards[Piece.Rook][squareIndex].GetAttacks(occupiedSquares) |
                MoveData.MagicBitboards[Piece.Bishop][squareIndex].GetAttacks(occupiedSquares);

        return 0;
    }
}
