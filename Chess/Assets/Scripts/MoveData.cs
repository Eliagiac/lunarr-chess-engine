using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveData
{
    public static Dictionary<int, ulong[,]> Moves;

    public static ulong[,] Masks;
    public static Dictionary<int, ulong[,]> SpecificMasks;


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
}
