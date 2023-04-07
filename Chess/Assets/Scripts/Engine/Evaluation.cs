using System;
using System.Collections;
using System.Collections.Generic;
using static Score;
using static Piece;
using static Evaluation;
using static Utilities.Bitboard;
using static System.Math;

public class Evaluation
{
    // The value of each piece depends on the game phase.
    // The scores below are organized by { openingValue, endgameValue }.
    public static readonly int[][] StaticPieceValues =
    {
        new[] {    0,    0}, /* None */
        new[] {  126,  208}, /* Pawn */
        new[] {  781,  854}, /* Knight */
        new[] {  825,  915}, /* Bishop */
        new[] { 1276, 1380}, /* Rook */
        new[] { 2538, 2682}  /* Queen */
    };


    public const int OpeningPhaseScore = 15258;
    public const int EndgamePhaseScore = 3915;


    public static int Evaluate(out int gamePhase, EvaluationData evaluationData)
    {
        short whiteEarlygameMaterial = EarlygameMaterial(0, out int whitePawnMaterial, out int whitePawnCount, out int whiteKnightCount, out int whiteBishopCount);
        short blackEarlygameMaterial = EarlygameMaterial(1, out int blackPawnMaterial, out int blackPawnCount, out int blackKnightCount, out int blackBishopCount);
        
        short whiteLategameMaterial = LategameMaterial(0);
        short blackLategameMaterial = LategameMaterial(1);


        gamePhase = GetGamePhase(whiteEarlygameMaterial, blackEarlygameMaterial, whitePawnMaterial, blackPawnMaterial);


        //evaluationData.InterpolateAll(gamePhase);


        uint whiteEval = 0;
        uint blackEval = 0;

        // Add material to the evaluation.
        //int whiteMaterial = Interpolate(gamePhase, whiteEarlygameMaterial, whiteLategameMaterial);
        //int blackMaterial = Interpolate(gamePhase, blackEarlygameMaterial, blackLategameMaterial);

        whiteEval += MakeScore(whiteEarlygameMaterial, whiteLategameMaterial);
        blackEval += MakeScore(blackEarlygameMaterial, blackLategameMaterial);


        // Offset the evaluation towards better endgame tactics.
        //whiteEval += EndgameEval(0, 1, whiteMaterial, blackMaterial, gamePhase);
        //blackEval += EndgameEval(1, 0, blackMaterial, whiteMaterial, gamePhase);


        // Add positional advantages to the evaluation.
        //Board.EvaluatePieceSquareTablesTimer.Start();
        whiteEval += Board.PsqtScore[0];
        blackEval += Board.PsqtScore[1];
        //Board.EvaluatePieceSquareTablesTimer.Stop();


        // Give a bonus to passed pawns.
        whiteEval += PassedPawnsBonus(0, Board.Pawns[0], Board.Pawns[1]);
        blackEval += PassedPawnsBonus(1, Board.Pawns[1], Board.Pawns[0]);

        // Give a penalty to doubled pawns.
        whiteEval += evaluationData.DoubledPawnPenalty * DoubledPawnsCount(Board.Pawns[0]);
        blackEval += evaluationData.DoubledPawnPenalty * DoubledPawnsCount(Board.Pawns[1]);

        // Give a penalty to isolated pawns.
        whiteEval += evaluationData.IsolatedPawnPenalty * IsolatedPawnsCount(Board.Pawns[0]);
        blackEval += evaluationData.IsolatedPawnPenalty * IsolatedPawnsCount(Board.Pawns[1]);

        // Give a penalty to backward pawns.
        whiteEval += evaluationData.BackwardPawnPenalty * BackwardPawnsCount(0, 1, Board.Pawns[0]);
        blackEval += evaluationData.BackwardPawnPenalty * BackwardPawnsCount(1, 0, Board.Pawns[1]);


        // Reduce the values of pawns in case of material imbalance.
        float materialImbalance = Abs(whiteEarlygameMaterial - blackEarlygameMaterial) / 100;
        uint whiteMaterialImbalanceMultiplier = (uint)(materialImbalance * whitePawnCount);
        uint blackMaterialImbalanceMultiplier = (uint)(materialImbalance * blackPawnCount);
        whiteEval += evaluationData.MaterialImbalancePawnPenaltyPerPawn * whiteMaterialImbalanceMultiplier;
        blackEval += evaluationData.MaterialImbalancePawnPenaltyPerPawn * blackMaterialImbalanceMultiplier;


        // Give a bonus to bishop/knight pairs depending on wheter the position is open or closed.
        if (IsPositionOpen())
        {
            if (whiteKnightCount == 2) whiteEval += evaluationData.KnightPairInOpenPositionBonus;
            if (whiteBishopCount == 2) whiteEval += evaluationData.BishopPairInOpenPositionBonus;

            if (blackKnightCount == 2) blackEval += evaluationData.KnightPairInOpenPositionBonus;
            if (blackBishopCount == 2) blackEval += evaluationData.BishopPairInOpenPositionBonus;
        }

        else
        {
            if (whiteKnightCount == 2) whiteEval += evaluationData.KnightPairInClosedPositionBonus;
            if (whiteBishopCount == 2) whiteEval += evaluationData.BishopPairInClosedPositionBonus;

            if (blackKnightCount == 2) blackEval += evaluationData.KnightPairInClosedPositionBonus;
            if (blackBishopCount == 2) blackEval += evaluationData.BishopPairInClosedPositionBonus;
        }


        // Give a bonus based on piece mobility.
        whiteEval += MobilityScore(0);
        blackEval += MobilityScore(1);


        // Give a bonus in case of a well protected king.
        whiteEval += KingSafetyScore(0, Board.Pawns[0], Board.Pawns[1]);
        blackEval += KingSafetyScore(1, Board.Pawns[1], Board.Pawns[0]);


        // Give a bonus to bishops that help with color weakness caused by an imbalanced pawn structure.
        whiteEval += ColorWeaknessScore(Board.Pawns[0], Board.Bishops[0]);
        blackEval += ColorWeaknessScore(Board.Pawns[1], Board.Bishops[1]);


        // Give a bonus to knights defended by a pawn in the opponent's half of the board.
        whiteEval += evaluationData.KnightOutpostBonus * KnightOutpostsCount(Board.PawnAttackedSquares[0], Board.PawnAttackedSquares[1], Board.Knights[0], Mask.BlackHalf);
        blackEval += evaluationData.KnightOutpostBonus * KnightOutpostsCount(Board.PawnAttackedSquares[1], Board.PawnAttackedSquares[0], Board.Knights[1], Mask.WhiteHalf);

        // Give a bonus to knights defended by a pawn in the opponent's half of the board.
        whiteEval += evaluationData.BishopOutpostBonus * BishopOutpostsCount(Board.PawnAttackedSquares[0], Board.PawnAttackedSquares[1], Board.Bishops[0], Mask.BlackHalf);
        blackEval += evaluationData.BishopOutpostBonus * BishopOutpostsCount(Board.PawnAttackedSquares[1], Board.PawnAttackedSquares[0], Board.Bishops[1], Mask.WhiteHalf);


        int eval = Interpolate(whiteEval - blackEval, gamePhase) * (Board.CurrentTurn == 0 ? 1 : -1);
        return eval;


        short EarlygameMaterial(int turnIndex, out int pawnMaterial, out int pawnCount, out int knightCount, out int bishopCount)
        {
            short material = 0;

            pawnCount = OccupiedSquaresCount(Board.Pawns[turnIndex]);
            material += (short)(pawnMaterial = pawnCount * StaticPieceValues[Pawn][0]);

            knightCount = OccupiedSquaresCount(Board.Knights[turnIndex]);
            material += (short)(knightCount * StaticPieceValues[Knight][0]);

            bishopCount = OccupiedSquaresCount(Board.Bishops[turnIndex]);
            material += (short)(bishopCount * StaticPieceValues[Bishop][0]);

            material += (short)(OccupiedSquaresCount(Board.Rooks[turnIndex]) * StaticPieceValues[Rook][0]);
            material += (short)(OccupiedSquaresCount(Board.Queens[turnIndex]) * StaticPieceValues[Queen][0]);
            return material;
        }

        short LategameMaterial(int turnIndex)
        {
            short material = 0;
            material += (short)(OccupiedSquaresCount(Board.Pawns[turnIndex]) * StaticPieceValues[Pawn][1]);
            material += (short)(OccupiedSquaresCount(Board.Knights[turnIndex]) * StaticPieceValues[Knight][1]);
            material += (short)(OccupiedSquaresCount(Board.Bishops[turnIndex]) * StaticPieceValues[Bishop][1]);
            material += (short)(OccupiedSquaresCount(Board.Rooks[turnIndex]) * StaticPieceValues[Rook][1]);
            material += (short)(OccupiedSquaresCount(Board.Queens[turnIndex]) * StaticPieceValues[Queen][1]);
            return material;
        }


        uint PassedPawnsBonus(int colorIndex, ulong pawns, ulong opponentPawns)
        {
            uint bonus = 0;
            for (int i = 1; i < 7; i++)
            {
                uint count = 0;

                foreach (var pawn in Board.GetIndexes(pawns & Board.Ranks[colorIndex][i]))
                {
                    // If there are no enemy pawns on this or adjacent files,
                    // and the pawn doesn't have another friendly pawn in front,
                    // it is considered a "passed pawn".
                    if ((Board.Spans[colorIndex, pawn] & opponentPawns) == 0)
                        if ((Board.Fills[colorIndex, pawn] & pawns) == 0) count++;
                }

                bonus += evaluationData.PassedPawnBonus[i] * count;
            }
            return bonus;
        }

        uint DoubledPawnsCount(ulong pawns)
        {
            uint count = 0;
            for (int i = 0; i < 8; i++)
            {
                // If there are multiple pawns on this file,
                // they are considered "doubled pawns".
                count += (uint)Max(0, OccupiedSquaresCount(pawns & Board.Files[i]) - 1);
            }
            return count;
        }

        uint IsolatedPawnsCount(ulong pawns)
        {
            uint count = 0;
            foreach (var pawn in Board.GetIndexes(pawns))
            {
                // If there are no friendly pawns in the pawn's neighbouring
                // files, it is considered an "isolated pawn".
                if ((Board.NeighbouringFiles[Board.GetFile(pawn)] & pawns) == 0) count++;
            }
            return count;
        }

        uint BackwardPawnsCount(int colorIndex, ulong opponentColorIndex, ulong pawns)
        {
            uint count = 0;
            foreach (var pawn in Board.GetIndexes(pawns))
            {
                // If there are no friendly pawns protecting the pawn
                // and its stop square is attacked by an enemy pawn,
                // it is considered a "backward pawn".
                if ((Board.BackwardProtectors[colorIndex, pawn] & pawns) == 0) 
                    if ((Board.StopSquare[colorIndex, pawn] & Board.PawnAttackedSquares[opponentColorIndex]) != 0) count++;
            }
            return count;
        }


        bool IsPositionOpen() => OccupiedSquaresCount(Board.AllOccupiedSquares) < 24;


        uint MobilityScore(int colorIndex)
        {
            ulong blockedSquares = colorIndex == 0 ?
                Board.AllOccupiedSquares >> 8 :
                Board.AllOccupiedSquares << 8;

            ulong lowRanks = Board.Ranks[colorIndex][1] | Board.Ranks[colorIndex][2];

            ulong mobilityArea = ~(
                (Board.Pawns[colorIndex] & (blockedSquares | lowRanks)) |   /* Exclude blocked pawns or pawns on a low rank. */
                (Board.Kings[colorIndex] | Board.Queens[colorIndex]) |      /* Exclude our king/queens. */
                Board.PawnAttackedSquares[colorIndex ^ 1]);                 /* Exclude squares controlled by enemy pawns. */

            uint score = 0;
            for (int pieceType = Knight; pieceType <= Queen; pieceType++)
            {
                foreach (var piece in Board.GetIndexes(Board.Pieces[pieceType][colorIndex]))
                {
                    score += evaluationData.MobilityBonus[pieceType][OccupiedSquaresCount(
                        Board.AttacksFrom(piece, pieceType, Board.AllOccupiedSquares) & mobilityArea)];
                }
            }

            return score;
        }

        uint KingSafetyScore(int colorIndex, ulong friendlyPawns, ulong opponentPawns)
        {
            uint score = 0;

            if ((Board.Kings[colorIndex] & (colorIndex == 0 ? Mask.WhiteCastledKingPosition : Mask.BlackCastledKingPosition)) != 0)
            {
                score +=
                    evaluationData.FirstShieldingPawnKingSafetyBonus * (uint)OccupiedSquaresCount(friendlyPawns & Board.FirstShieldingPawns[Board.KingPosition[colorIndex]]) +
                    evaluationData.SecondShieldingPawnKingSafetyBonus * (uint)OccupiedSquaresCount(friendlyPawns & Board.SecondShieldingPawns[Board.KingPosition[colorIndex]]);
            }

            int kingFile = Board.GetFile(Board.KingPosition[colorIndex]);
            ulong[] kingFiles = Board.KingFiles[kingFile];

            foreach (var file in kingFiles)
            {
                int friendlyPawnsCount = OccupiedSquaresCount(friendlyPawns & file);
                int opponentPawnsCount = OccupiedSquaresCount(opponentPawns & file);

                if (friendlyPawnsCount == 0)
                {
                    if (opponentPawnsCount != 0) score += evaluationData.HalfOpenFileNextToKingPenalty;
                    else score += evaluationData.OpenFileNextToKingPenalty;
                }
            }

            return score;
        }

        uint ColorWeaknessScore(ulong pawns, ulong bishops)
        {
            uint score = 0;

            int lightPawnsCount = OccupiedSquaresCount(pawns & Mask.LightSquares);
            int darkPawnsCount = OccupiedSquaresCount(pawns & Mask.DarkSquares);

            int lightBishopsCount = OccupiedSquaresCount(bishops & Mask.LightSquares);
            int darkBishopsCount = OccupiedSquaresCount(bishops & Mask.DarkSquares);

            // Give a penalty for the lack of a bishop on a light square, for each extra pawn on a dark square.
            score += evaluationData.ColorWeaknessPenaltyPerPawn * (uint)(lightBishopsCount == 0 ? 1 : 0) * (uint)Max(0, darkPawnsCount - lightPawnsCount);

            // Give a penalty for the lack of a bishop on a light square, for each extra pawn on a dark square.
            score += evaluationData.ColorWeaknessPenaltyPerPawn * (uint)(darkBishopsCount == 0 ? 1 : 0) * (uint)Max(0, lightPawnsCount - darkPawnsCount);

            return score;
        }

        uint KnightOutpostsCount(ulong pawnDefendedSquares, ulong opponentPawnAttackedSquares, ulong knights, ulong opponentHalf)
        {
            // If a knight is in the opponent's half of the board (or in the center),
            // is defended by a pawn and is not under attack by an opponent pawn,
            // it is considered an "outpost".
            return (uint)OccupiedSquaresCount(pawnDefendedSquares & ~opponentPawnAttackedSquares & knights & opponentHalf);
        }

        uint BishopOutpostsCount(ulong pawnDefendedSquares, ulong opponentPawnAttackedSquares, ulong bishops, ulong opponentHalf)
        {
            // If a bishop is in the opponent's half of the board (or in the center),
            // is defended by a pawn and is not under attack by an opponent pawn,
            // it is considered an "outpost".
            return (uint)OccupiedSquaresCount(pawnDefendedSquares & ~opponentPawnAttackedSquares & bishops & opponentHalf);
        }
    }


    public static int GetPieceValue(int piece)
    {
        switch (piece)
        {
            case Pawn :   return StaticPieceValues[Pawn][0];
            case Knight:  return StaticPieceValues[Knight][0];
            case Bishop:  return StaticPieceValues[Bishop][0];
            case Rook:    return StaticPieceValues[Rook][0];
            case Queen:   return StaticPieceValues[Queen][0];
            default:            return 0;
        }
    }

    public static int GetGamePhase(int whiteMaterial, int blackMaterial, int whitePawnMaterial, int blackPawnMaterial) => 
        (whiteMaterial + blackMaterial) - (whitePawnMaterial + blackPawnMaterial);

    public static bool PawnsOnly(EvaluationData evaluationData)
    {
        int materialCount = 0;
        materialCount += OccupiedSquaresCount(Board.Knights[Board.CurrentTurn]);
        materialCount += OccupiedSquaresCount(Board.Bishops[Board.CurrentTurn]);
        materialCount += OccupiedSquaresCount(Board.Rooks[Board.CurrentTurn]);
        materialCount += OccupiedSquaresCount(Board.Queens[Board.CurrentTurn]);
        return materialCount == 0;
    }
    
    public static int EndgameEval(int currentTurnIndex, int opponentTurnIndex, int myMaterial, int opponentMaterial, int gamePhase)
    {
        int mopUpScore = 0;
        if (myMaterial > opponentMaterial + 200 && gamePhase < EndgamePhaseScore)
        {
            int friendlyKingSquare = Board.KingPosition[currentTurnIndex];
            int opponentKingSquare = Board.KingPosition[opponentTurnIndex];

            int opponentKingRank = Board.GetRank(opponentKingSquare);
            int friendlyKingRank = Board.GetRank(friendlyKingSquare);

            int opponentKingFile = Board.GetFile(opponentKingSquare);
            int friendlyKingFile = Board.GetFile(friendlyKingSquare);

            mopUpScore += Max(3 - opponentKingFile, opponentKingFile - 4) + Max(3 - opponentKingRank, opponentKingRank - 4);

            mopUpScore += 14 - (Abs(friendlyKingFile - opponentKingFile) + Abs(friendlyKingRank - opponentKingRank));

            return ((mopUpScore * 10 * (OpeningPhaseScore - gamePhase)) / OpeningPhaseScore);
        }
        return 0;
    }
}


public class PieceSquareTables
{
    public static uint EvaluateAllPsqt(int turnIndex)
    {
        uint score = 0;
        bool isWhite = turnIndex == 0;
        score += EvaluatePsqtScore(Pawn, Board.Pawns[turnIndex], isWhite);
        score += EvaluatePsqtScore(Rook, Board.Rooks[turnIndex], isWhite);
        score += EvaluatePsqtScore(Knight, Board.Knights[turnIndex], isWhite);
        score += EvaluatePsqtScore(Bishop, Board.Bishops[turnIndex], isWhite);
        score += EvaluatePsqtScore(Queen, Board.Queens[turnIndex], isWhite);
        score += EvaluatePsqtScore(King, Board.Kings[turnIndex], isWhite);
        return score;
    }

    public static uint EvaluatePsqtScore(int pieceType, ulong pieceList, bool isWhite)
    {
        List<int> pieces = Board.GetIndexes(pieceList);
        uint score = 0;
        foreach (var piece in pieces)
        {
            score += ReadScore(pieceType, piece, isWhite);
        }
        return score;
    }

    public static int Read(int pieceType, int square, bool isWhite, int gamePhase)
    {
        int rank = Board.GetRank(square);
        int file = Board.GetFile(square);
        
        if (!isWhite) rank = 7 - rank;
        
        if (pieceType != Pawn) return Interpolate(s_pieceSquareTables[pieceType][rank][FileIndex()], gamePhase);
        return Interpolate(s_pawnPieceSquareTables[rank][file], gamePhase);
        
        
        int FileIndex() => file >= 4 ? 7 - file : file;
    }

    public static uint ReadScore(int pieceType, int square, bool isWhite)
    {
        int rank = Board.GetRank(square);
        int file = Board.GetFile(square);

        if (!isWhite) rank = 7 - rank;

        if (pieceType != Pawn) return s_pieceSquareTables[pieceType][rank][FileIndex()];
        return s_pawnPieceSquareTables[rank][file];


        int FileIndex() => file >= 4 ? 7 - file : file;
    }


    // Piece square tables are represented vertically symmetrical to what we would see them as.
    // This means that no extra operations are needed for black, and white has index = 63 - square.

    private static readonly Dictionary<int, int[]> s_earlygamePieceSquareTables = new()
    {
        [Pawn] = new int[] {
       0,   0,   0,   0,   0,   0,   0,   0,
      -1,  45,  49,  47,  47,  61,  68,  -4,
      -6,  14,  24,  24,  33,  25,  16,   6,
      11,  -1,   8,  20,  28,   9,  -1,   0,
      -6,   6,  -5,  14,  14,   6,   4,   6,
       5,  -1, -12,   2,   4,  -4,  -8,  -1,
       8,  15,  13, -14, -14,  16,  16,   5,
       0,   0,   0,   0,   0,   0,   0,   0 },

        [Knight] = new int[] {
     -50, -40, -30, -26, -28, -30, -40, -55,
     -40, -17,   3,   4,  -6,   0, -15, -37,
     -29,   6,   6,  11,  12,  16,   5, -25,
     -26,  11,   9,  18,  14,  21,   5, -24,
     -24,   2,  21,  24,  23,   9,  -5, -36,
     -25,  11,   4,  20,  19,   4,  -1, -32,
     -34, -18,  -5,   8,  -1,   4, -22, -38,
     -50, -34, -25, -36, -24, -32, -46, -50 },

        [Bishop] = new int[] {
     -20, -10, -12, -10,  -5, -12, -15, -16,
      -6,   3,  -5,  -6,   1,   6,   5, -13,
      -5,  -6,  -1,   7,  16,  -1,   6, -16,
     -13,  11,  10,   4,  14,  11,  -1, -14,
     -16,   2,   6,   4,  16,  12,   5,  -4,
     -15,  16,   4,  16,   8,  14,   4, -15,
      -7,   9,   6,   3,   6,   6,   6,  -5,
     -14, -12,  -4,  -9,  -4, -15,  -4, -14 },

        [Rook] = new int[] {
      -2,   4,  -6,  -2,   2,   6,  -2,   5,
       4,  16,  15,  11,  15,  11,  13,   8,
      -5,   3,  -2,   1,   6,   3,   3,  -6,
      -2,   3,  -6,  -1,  -4,  -4,   5, -10,
       1,  -5,   1,   4,  -2,   5,   3,  -4,
      -9,   1,   6,   5,  -3,   1,   1,   0,
      -9,  -6,  -6,   5,   0,  -4,  -1, -10,
      -5,   4,   5,   9,   9,  -6,  -2,  -1 },

        [Queen] = new int[] {
     -17, -10, -13,  -7,  -3, -11,  -9, -25,
      -5,   4,   6,  -1,  -5,   4,  -6,  -4,
      -5,  -4,   6,   7,   0,   2,  -5,  -8,
       1,   0,  11,   7,  -1,   7,  -4,   0,
      -2,  -6,   2,  -1,   1,   7,   4,  -6,
     -15,   6,  11,   4,  11,  11,  11, -15,
     -10,   3,  -3,   3,  -6,   1,  -6,  -5,
     -24,  -8, -16,  -3,  -8, -13,  -4, -15 },

        [King] = new int[] {
     -30, -40, -40, -50, -50, -40, -40, -30,
     -30, -40, -39, -50, -49, -43, -37, -30,
     -30, -46, -40, -49, -46, -40, -41, -32,
     -30, -39, -39, -54, -52, -39, -38, -32,
     -19, -30, -29, -44, -42, -29, -33, -20,
     -13, -20, -21, -22, -20, -17, -18, -10,
      14,  15,  -1,   4,  -1,  -1,  18,  14,
      22,  32,  14,   1,   6,  11,  35,  21 }
    };

    private static readonly Dictionary<int, int[]> s_endgamePieceSquareTables = new()
    {
        [Pawn] = new int[] {
       0,   0,   0,   0,   0,   0,   0,   0,
       4,  68,  98,  85,  94, 120, 174,  -4,
      -6,  37,  38,  31,  45,  44,  48,   6,
      -2,  -2,  -1,   2,  -6,  -1,  -4,  -6,
      -3,   4,  -5,   0,  -3,   5,   2,   2,
       1,   0,  -1,   0,   5,   1,   0,  -2,
      -4,   4,   3,   0,  -6,   6,   5,  -2,
       0,   0,   0,   0,   0,   0,   0,   0 },

        [Knight] = new int[] {
     -50, -40, -35, -24, -24, -30, -40, -50,
     -40, -15,  -4,   5,  -5,   6, -17, -38,
     -26,  -6,  10,  15,   9,  15,   3, -24,
     -28,  10,   9,  18,  17,  21,   5, -29,
     -26,   5,  20,  14,  16,  18,  -5, -36,
     -27,   9,  15,  11,  20,   5,   7, -32,
     -40, -22,   1,   5,  -1,   5, -20, -43,
     -50, -35, -25, -30, -27, -32, -40, -50 },

        [Bishop] = new int[] {
     -20, -16,  -9, -14,  -7,  -4, -13, -14,
      -4,   5,  -3,   4,  -6,   3,   6, -11,
     -10,  -5,  -1,   4,  15,   5,  -3, -11,
     -13,   7,  11,   5,  16,  11,  -1,  -7,
     -16,   4,  12,   6,  16,  10,   4,  -4,
     -12,   7,   7,  10,  12,  11,   4,  -4,
      -7,   1,   2,  -3,   6,   6,   7, -11,
     -17,  -6, -10, -10,  -4, -11,  -4, -15 },

        [Rook] = new int[] {
      -3,   6,  -6,  -4,  -4,   1,  -6,   5,
     -15,   4,   3,  -1,   5,   2,   4,  -6,
      -9,   5,  -6,  -1,   0,   3,   3, -15,
      -4,  -4,  -3,  -3,  -6,   0,   6, -16,
     -10,  -6,   0,   6,  -6,   2,   6, -15,
     -15,   0,   5,   6,  -2,   3,  -1,  -6,
      -5,  -6,  -5,   3,  -4,   1,  -4,  -8,
       0,   2,   4,   1,   1,  -2,   0,   1 },

        [Queen] = new int[] {
     -16, -10, -15,  -8,   1,  -6,  -7,  -21,
     -10,   3,   6,   2,  -4,   3,  -5,   -4,
      -6,  -4,  10,   6,   2,   7,  -2,  -13,
      -2,  -2,   8,   8,   1,   3,  -4,   -1,
      -3,   0,   1,  -1,   1,   8,   6,    0,
     -10,   4,   9,   7,   3,   6,  10,  -11,
     -10,   4,  -5,   0,   0,   5,  -6,  -12,
     -20,  -9, -12,  -4,  -7,  -7,  -6,  -20 },

        [King] = new int[] {
     -50, -40, -30, -20, -20, -30, -40,  -50,
     -30, -24,  -6,   3,   6, -15, -18,  -30,
     -30, -11,  14,  34,  32,  20, -16,  -35,
     -35, -16,  35,  34,  35,  24,  -5,  -34,
     -31, -12,  34,  34,  34,  31,  -7,  -36,
     -33, -13,  16,  36,  33,  14,  -7,  -30,
     -33, -31,  -1,   5,   2,   5, -27,  -36,
     -51, -30, -25, -28, -26, -26, -26,  -48 }
    };


    // Values from Stockfish: https://github.com/official-stockfish/Stockfish/blob/master/src/psqt.cpp#L29-L102
    // Accessed by s_pieceSquareTables[pieceType][rankIndex][fileIndex (up to 4, mirrored after)].
    private static readonly uint[][][] s_pieceSquareTables =
    {
        /* None */
        new uint[][] { },
        
        /* Pawn (separate table) */
        new uint[][] { },

        /* Knight */
        new uint[][] {
            new uint[] { MakeScore(-175, -96), MakeScore( -92, -65), MakeScore( -74, -49), MakeScore( -73, -21) },
            new uint[] { MakeScore( -77, -67), MakeScore( -41, -54), MakeScore( -27, -18), MakeScore( -15,   8) },
            new uint[] { MakeScore( -61, -40), MakeScore( -17, -27), MakeScore(   6,  -8), MakeScore(  12,  29) },
            new uint[] { MakeScore( -35, -35), MakeScore(   8,  -2), MakeScore(  40,  13), MakeScore(  49,  28) },
            new uint[] { MakeScore( -34, -45), MakeScore(  13, -16), MakeScore(  44,   9), MakeScore(  51,  39) },
            new uint[] { MakeScore(  -9, -51), MakeScore(  22, -44), MakeScore(  58, -16), MakeScore(  53,  17) },
            new uint[] { MakeScore( -67, -69), MakeScore( -27, -50), MakeScore(   4, -51), MakeScore(  37,  12) },
            new uint[] { MakeScore(-201,-100), MakeScore( -83, -88), MakeScore( -56, -56), MakeScore( -26, -17) }
        },                                                                                       
                                                                                                 
        new uint[][] { /* Bishop */                                                              
            new uint[] { MakeScore( -37, -40), MakeScore(  -4, -21), MakeScore(  -6, -26), MakeScore( -16,  -8) },
            new uint[] { MakeScore( -11, -26), MakeScore(   6,  -9), MakeScore(  13, -12), MakeScore(   3,   1) },
            new uint[] { MakeScore( -5 , -11), MakeScore(  15,  -1), MakeScore(  -4,  -1), MakeScore(  12,   7) },
            new uint[] { MakeScore( -4 , -14), MakeScore(   8,  -4), MakeScore(  18,   0), MakeScore(  27,  12) },
            new uint[] { MakeScore( -8 , -12), MakeScore(  20,  -1), MakeScore(  15, -10), MakeScore(  22,  11) },
            new uint[] { MakeScore( -11, -21), MakeScore(   4,   4), MakeScore(   1,   3), MakeScore(   8,   4) },
            new uint[] { MakeScore( -12, -22), MakeScore( -10, -14), MakeScore(   4,  -1), MakeScore(   0,   1) },
            new uint[] { MakeScore( -34, -32), MakeScore(   1, -29), MakeScore( -10, -26), MakeScore( -16, -17) }
        },                                                                                       
                                                                                                 
        new uint[][] { /* Rook */                                                                
            new uint[] { MakeScore( -31,  -9), MakeScore( -20, -13), MakeScore( -14, -10), MakeScore(  -5,  -9) },
            new uint[] { MakeScore( -21, -12), MakeScore( -13,  -9), MakeScore(  -8,  -1), MakeScore(   6,  -2) },
            new uint[] { MakeScore( -25,   6), MakeScore( -11,  -8), MakeScore(  -1,  -2), MakeScore(   3,  -6) },
            new uint[] { MakeScore( -13,  -6), MakeScore(  -5,   1), MakeScore(  -4,  -9), MakeScore(  -6,   7) },
            new uint[] { MakeScore( -27,  -5), MakeScore( -15,   8), MakeScore(  -4,   7), MakeScore(   3,  -6) },
            new uint[] { MakeScore( -22,   6), MakeScore(  -2,   1), MakeScore(   6,  -7), MakeScore(  12,  10) },
            new uint[] { MakeScore(  -2,   4), MakeScore(  12,   5), MakeScore(  16,  20), MakeScore(  18,  -5) },
            new uint[] { MakeScore( -17,  18), MakeScore( -19,   0), MakeScore(  -1,  19), MakeScore(   9,  13) }
        },                                                                                       
                                                                                                 
        new uint[][] { /* Queen */                                                               
            new uint[] { MakeScore(   3, -69), MakeScore(  -5, -57), MakeScore(  -5, -47), MakeScore(   4, -26) },
            new uint[] { MakeScore(  -3, -54), MakeScore(   5, -31), MakeScore(   8, -22), MakeScore(  12,  -4) },
            new uint[] { MakeScore(  -3, -39), MakeScore(   6, -18), MakeScore(  13,  -9), MakeScore(   7,   3) },
            new uint[] { MakeScore(   4, -23), MakeScore(   5,  -3), MakeScore(   9,  13), MakeScore(   8,  24) },
            new uint[] { MakeScore(   0, -29), MakeScore(  14,  -6), MakeScore(  12,   9), MakeScore(   5,  21) },
            new uint[] { MakeScore(  -4, -38), MakeScore(  10, -18), MakeScore(   6, -11), MakeScore(   8,   1) },
            new uint[] { MakeScore(  -5, -50), MakeScore(   6, -27), MakeScore(  10, -24), MakeScore(   8,  -8) },
            new uint[] { MakeScore(  -2, -74), MakeScore(  -2, -52), MakeScore(   1, -43), MakeScore(  -2, -34) }
        },                                                                                       
                                                                                                 
        new uint[][] { /* King */                                                                
            new uint[] { MakeScore( 271,   1), MakeScore( 327,  45), MakeScore( 271,  85), MakeScore( 198,  76) },
            new uint[] { MakeScore( 278,  53), MakeScore( 303, 100), MakeScore( 234, 133), MakeScore( 179, 135) },
            new uint[] { MakeScore( 195,  88), MakeScore( 258, 130), MakeScore( 169, 169), MakeScore( 120, 175) },
            new uint[] { MakeScore( 164, 103), MakeScore( 190, 156), MakeScore( 138, 172), MakeScore(  98, 172) },
            new uint[] { MakeScore( 154,  96), MakeScore( 179, 166), MakeScore( 105, 199), MakeScore(  70, 199) },
            new uint[] { MakeScore( 123,  92), MakeScore( 145, 172), MakeScore(  81, 184), MakeScore(  31, 191) },
            new uint[] { MakeScore(  88,  47), MakeScore( 120, 121), MakeScore(  65, 116), MakeScore(  33, 131) },
            new uint[] { MakeScore(  59,  11), MakeScore(  89,  59), MakeScore(  45,  73), MakeScore(  -1,  78) }
        }
    };

    // Values from Stockfish.
    // Accessed by s_pawnPieceSquareTables[rankIndex][fileIndex].
    private static readonly uint[][] s_pawnPieceSquareTables =
    {
        new uint[] { },
        new uint[] { MakeScore(  2, -8), MakeScore(  4, -6), MakeScore( 11,  9), MakeScore( 18,  5), MakeScore( 16, 16), MakeScore( 21,  6), MakeScore(  9, -6), MakeScore( -3,-18) },
        new uint[] { MakeScore( -9, -9), MakeScore(-15, -7), MakeScore( 11,-10), MakeScore( 15,  5), MakeScore( 31,  2), MakeScore( 23,  3), MakeScore(  6, -8), MakeScore(-20, -5) },
        new uint[] { MakeScore( -3,  7), MakeScore(-20,  1), MakeScore(  8, -8), MakeScore( 19, -2), MakeScore( 39,-14), MakeScore( 17,-13), MakeScore(  2,-11), MakeScore( -5, -6) },
        new uint[] { MakeScore( 11, 12), MakeScore( -4,  6), MakeScore(-11,  2), MakeScore(  2, -6), MakeScore( 11, -5), MakeScore(  0, -4), MakeScore(-12, 14), MakeScore(  5,  9) },
        new uint[] { MakeScore(  3, 27), MakeScore(-11, 18), MakeScore( -6, 19), MakeScore( 22, 29), MakeScore( -8, 30), MakeScore( -5,  9), MakeScore(-14,  8), MakeScore(-11, 14) },
        new uint[] { MakeScore( -7, -1), MakeScore(  6,-14), MakeScore( -2, 13), MakeScore(-11, 22), MakeScore(  4, 24), MakeScore(-14, 17), MakeScore( 10,  7), MakeScore( -9,  7) }
    };


    //public static readonly int[] whiteSquaresEquivalent =
    //{
    //   56, 57,  58,  59,  60,  61,  62,  63,
    //   48, 49,  50,  51,  52,  53,  54,  55,
    //   40, 41,  42,  43,  44,  45,  46,  47,
    //   32, 33,  34,  35,  36,  37,  38,  39,
    //   24, 25,  26,  27,  28,  29,  30,  31,
    //   16, 17,  18,  19,  20,  21,  22,  23,
    //    8,  9,  10,  11,  12,  13,  14,  15,
    //    0,  1,   2,   3,   4,   5,   6,   7
    //};


    //public static readonly int[] blackSquaresEquivalent =
    //{
    //    7,   6,   5,   4,   3,   2,   1,   0,
    //   15,  14,  13,  12,  11,  10,   9,   8,
    //   23,  22,  21,  20,  19,  18,  17,  16,
    //   31,  30,  29,  28,  27,  26,  25,  24,
    //   39,  38,  37,  36,  35,  34,  33,  32,
    //   47,  46,  45,  44,  43,  42,  41,  40,
    //   55,  54,  53,  52,  51,  50,  49,  48,
    //   63,  62,  61,  60,  59,  58,  57,  56
    //};
}


// Default values from Stockfish.
[System.Serializable]
public class EvaluationData
{
    public uint[] PieceValues =
    {
        MakeScore(   0,   0), /* None */
        MakeScore( 126, 208), /* Pawn */
        MakeScore( 781, 854), /* Knight */
        MakeScore( 825, 915), /* Bishop */
        MakeScore(1276,1380), /* Rook */
        MakeScore(2538,2682)  /* Queen */
    };


    public uint[] PassedPawnBonus = 
    {
        MakeScore(   +0,   +0),
        MakeScore(   +2,  +38),
        MakeScore(  +15,  +36),
        MakeScore(  +22,  +50),
        MakeScore(  +64,  +81),
        MakeScore( +166, +184),
        MakeScore( +284, +269),
        MakeScore(   +0,   +0),
    };

    public uint DoubledPawnPenalty = MakeScore(-11, -51);
    public uint IsolatedPawnPenalty = MakeScore(-1, -20);
    public uint BackwardPawnPenalty = MakeScore(-6, -10);

    // Not from Stockfish.
    public uint MaterialImbalancePawnPenaltyPerPawn = MakeScore(-5, -3);

    // Not from Stockfish.
    public uint KnightPairInOpenPositionBonus = MakeScore(+20, +10);
    public uint BishopPairInOpenPositionBonus = MakeScore(+60, +40);
    public uint KnightPairInClosedPositionBonus = MakeScore(+50, +30);
    public uint BishopPairInClosedPositionBonus = MakeScore(+30, +10);

    // MobilityBonus[PieceType][attacked] contains bonuses for middle and end game,
    // indexed by piece type and number of attacked squares in the mobility area.
    public uint[][] MobilityBonus =
    {
        /* None */
        new uint[] { },

        /* Pawn */
        new uint[] { },

        /* Knight */
        new uint[] { MakeScore(-62,-81), MakeScore(-53,-56), MakeScore(-12,-30), MakeScore( -4,-14), MakeScore(  3,  8), MakeScore( 13, 15), MakeScore( 22, 23), MakeScore( 28, 27), MakeScore( 33, 33) },
    
        /* Bishop */
        new uint[] { MakeScore(-48, -59), MakeScore(-20, -23), MakeScore(16, -3), MakeScore(26, 13), MakeScore(38, 24), MakeScore(51, 42), MakeScore(55, 54), MakeScore(63, 57), MakeScore(63, 65), MakeScore(68, 73), MakeScore(81, 78), MakeScore(81, 86), MakeScore(91, 88), MakeScore(98, 97) },
        
        /* Rook */
        new uint[] { MakeScore(-58, -76), MakeScore(-27, -18), MakeScore(-15, 28), MakeScore(-10, 55), MakeScore(-5, 69), MakeScore(-2, 82), MakeScore(9, 112), MakeScore(16, 118), MakeScore(30, 132), MakeScore(29, 142), MakeScore(32, 155), MakeScore(38, 165), MakeScore(46, 166), MakeScore(48, 169), MakeScore(58, 171) },

        /* Queen */
        new uint[] { MakeScore(-39, -36), MakeScore(-21, -15), MakeScore(3, 8), MakeScore(3, 18), MakeScore(14, 34), MakeScore(22, 54), MakeScore(28, 61), MakeScore(41, 73), MakeScore(43, 79), MakeScore(48, 92), MakeScore(56, 94), MakeScore(60, 104), MakeScore(60, 113), MakeScore(66, 120), MakeScore(67, 123), MakeScore(70, 126), MakeScore(71, 133), MakeScore(73, 136), MakeScore(79, 140), MakeScore(88, 143), MakeScore(88, 148), MakeScore(99, 166), MakeScore(102, 170), MakeScore(102, 175), MakeScore(106, 184), MakeScore(109, 191), MakeScore(113, 206), MakeScore(116, 212) },

        /* King */
        new uint[] { }
    };

    // Not from Stockfish.
    public uint FirstShieldingPawnKingSafetyBonus = MakeScore(+20, +10);
    public uint SecondShieldingPawnKingSafetyBonus = MakeScore(+10, +5);

    // Not from Stockfish.
    public uint HalfOpenFileNextToKingPenalty = MakeScore(-20, -30);
    public uint OpenFileNextToKingPenalty = MakeScore(-40, -50);

    public uint ColorWeaknessPenaltyPerPawn = MakeScore(-3, -8);

    public uint KnightOutpostBonus = MakeScore(+54, +34);
    public uint BishopOutpostBonus = MakeScore(+31, +25);

    public EvaluationData()
    {
        ResetAllValues();
    }


    public void ResetAllValues()
    {
        PieceValues = new[]
        {
            MakeScore(   0,   0), /* None */
            MakeScore( 126, 208), /* Pawn */
            MakeScore( 781, 854), /* Knight */
            MakeScore( 825, 915), /* Bishop */
            MakeScore(1276,1380), /* Rook */
            MakeScore(2538,2682)  /* Queen */
        };
    
    
        PassedPawnBonus = new[]
        {
            MakeScore(   +0,   +0),
            MakeScore(   +2,  +38),
            MakeScore(  +15,  +36),
            MakeScore(  +22,  +50),
            MakeScore(  +64,  +81),
            MakeScore( +166, +184),
            MakeScore( +284, +269),
            MakeScore(   +0,   +0),
        };
    
        DoubledPawnPenalty = MakeScore(-11, -51);
        IsolatedPawnPenalty = MakeScore(-1, -20);
        BackwardPawnPenalty = MakeScore(-6, -10);
    
        MaterialImbalancePawnPenaltyPerPawn = MakeScore(-5, -3);
    
        KnightPairInOpenPositionBonus = MakeScore(+20, +10);
        BishopPairInOpenPositionBonus = MakeScore(+60, +40);
        KnightPairInClosedPositionBonus = MakeScore(+50, +30);
        BishopPairInClosedPositionBonus = MakeScore(+30, +10);

        MobilityBonus = new[]
        {
            /* None */
            new uint[] { },

            /* Pawn */
            new uint[] { },

            /* Knight */
            new uint[] { MakeScore(-62, -81), MakeScore(-53, -56), MakeScore(-12, -30), MakeScore(-4, -14), MakeScore(3, 8), MakeScore(13, 15), MakeScore(22, 23), MakeScore(28, 27), MakeScore(33, 33) },
        
            /* Bishop */
            new uint[] { MakeScore(-48, -59), MakeScore(-20, -23), MakeScore(16, -3), MakeScore(26, 13), MakeScore(38, 24), MakeScore(51, 42), MakeScore(55, 54), MakeScore(63, 57), MakeScore(63, 65), MakeScore(68, 73), MakeScore(81, 78), MakeScore(81, 86), MakeScore(91, 88), MakeScore(98, 97) },
            
            /* Rook */
            new uint[] { MakeScore(-58, -76), MakeScore(-27, -18), MakeScore(-15, 28), MakeScore(-10, 55), MakeScore(-5, 69), MakeScore(-2, 82), MakeScore(9, 112), MakeScore(16, 118), MakeScore(30, 132), MakeScore(29, 142), MakeScore(32, 155), MakeScore(38, 165), MakeScore(46, 166), MakeScore(48, 169), MakeScore(58, 171) },

            /* Queen */
            new uint[] { MakeScore(-39, -36), MakeScore(-21, -15), MakeScore(3, 8), MakeScore(3, 18), MakeScore(14, 34), MakeScore(22, 54), MakeScore(28, 61), MakeScore(41, 73), MakeScore(43, 79), MakeScore(48, 92), MakeScore(56, 94), MakeScore(60, 104), MakeScore(60, 113), MakeScore(66, 120), MakeScore(67, 123), MakeScore(70, 126), MakeScore(71, 133), MakeScore(73, 136), MakeScore(79, 140), MakeScore(88, 143), MakeScore(88, 148), MakeScore(99, 166), MakeScore(102, 170), MakeScore(102, 175), MakeScore(106, 184), MakeScore(109, 191), MakeScore(113, 206), MakeScore(116, 212) },

            /* King */
            new uint[] { }
        };

        FirstShieldingPawnKingSafetyBonus = MakeScore(+20, +10);
        SecondShieldingPawnKingSafetyBonus = MakeScore(+10, +5);
    
        HalfOpenFileNextToKingPenalty = MakeScore(-20, -30);
        OpenFileNextToKingPenalty = MakeScore(-40, -50);
    
        ColorWeaknessPenaltyPerPawn = MakeScore(-3, -8);
    
        KnightOutpostBonus = MakeScore(+54, +34);
        BishopOutpostBonus = MakeScore(+31, +25);
    }
}

public struct Score
{
    // A score is stored in a single unsigned integer,
    // where the first 16 bits store the endgame score
    // and the last 16 bits store the opening score.
    public static uint MakeScore(short opening, short endgame) => (((uint)endgame) << 16) + (uint)opening;

    public static int Interpolate(uint score, int gamePhase)
    {
        short opening = (short)(score & 0x0000ffff);
        short endgame = (short)(score & 0xffff0000);

        int result;

        if (gamePhase > OpeningPhaseScore) result = opening;
        else if (gamePhase < EndgamePhaseScore) result = endgame;

        else
        {
            // Interpolate for middle game
            result = (opening * gamePhase +
                endgame * (OpeningPhaseScore - gamePhase)
                ) / OpeningPhaseScore;
        }

        return result;
    }
}
