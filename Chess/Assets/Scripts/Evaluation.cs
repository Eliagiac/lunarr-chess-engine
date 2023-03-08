using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Evaluation
{
    //public static readonly int[][] PawnValue = { new int[] { 89, 92 }, new int[] { 96, 102 } };
    //public static readonly int[][] KnightValue = { new int[] { 308, 307 }, new int[] { 319, 318 } };
    //public static readonly int[][] BishopValue = { new int[] { 319, 323 }, new int[] { 331, 334 } };
    //public static readonly int[][] RookValue = { new int[] { 488, 492 }, new int[] { 497, 501 } };
    //public static readonly int[][] QueenValue = { new int[] { 888, 888 }, new int[] { 853, 845 } };
    //public static readonly int[][] KingValue = { new int[] { 20001, 20002 }, new int[] { 19998, 20000 } };


    public static readonly int[][] StaticPieceValues =
    {
        new[] {   0,   0}, /* None */
        new[] { 126, 208}, /* Pawn */
        new[] { 781, 854}, /* Knight */
        new[] { 825, 915}, /* Bishop */
        new[] {1276,1380}, /* Rook */
        new[] {2538,2682}  /* Queen */
    };


    public const int OpeningPhaseScore = 15258;
    public const int EndgamePhaseScore = 3915;


    public static int Evaluate(out int gamePhase, EvaluationData evaluationData)
    {
        Board.EvaluationTimer.Start();
        Board.EvaluationCounter++;

        int whiteEarlygameMaterial = EarlygameMaterial(0, out int whitePawnMaterial, out int whitePawnCount, out int whiteKnightCount, out int whiteBishopCount);
        int blackEarlygameMaterial = EarlygameMaterial(1, out int blackPawnMaterial, out int blackPawnCount, out int blackKnightCount, out int blackBishopCount);

        int whiteLategameMaterial = LategameMaterial(0);
        int blackLategameMaterial = LategameMaterial(1);


        gamePhase = GetGamePhase(whiteEarlygameMaterial, blackEarlygameMaterial, whitePawnMaterial, blackPawnMaterial);


        evaluationData.InterpolateAll(gamePhase);


        int whiteEval = 0;
        int blackEval = 0;

        // Add material to the evaluation.
        int whiteMaterial = Interpolate(gamePhase, whiteEarlygameMaterial, whiteLategameMaterial);
        int blackMaterial = Interpolate(gamePhase, blackEarlygameMaterial, blackLategameMaterial);

        whiteEval += whiteMaterial;
        blackEval += blackMaterial;


        // Offset the evaluation towards better endgame tactics.
        whiteEval += EndgameEval(0, 1, whiteMaterial, blackMaterial, gamePhase);
        blackEval += EndgameEval(1, 0, blackMaterial, whiteMaterial, gamePhase);


        // Add positional advantages to the evaluation.
        Board.EvaluatePieceSquareTablesTimer.Start();
        whiteEval += PieceSquareTables.EvaluatePieceSquareTables(0, gamePhase);
        blackEval += PieceSquareTables.EvaluatePieceSquareTables(1, gamePhase);
        Board.EvaluatePieceSquareTablesTimer.Stop();


        // Give a bonus to passed pawns.
        whiteEval += PassedPawnsBonus(0, Board.Pawns[0], Board.Pawns[1]);
        blackEval += PassedPawnsBonus(1, Board.Pawns[1], Board.Pawns[0]);

        // Give a penalty to doubled pawns.
        whiteEval += DoubledPawnsCount(Board.Pawns[0]) * evaluationData.DoubledPawnPenalty.Value;
        blackEval += DoubledPawnsCount(Board.Pawns[1]) * evaluationData.DoubledPawnPenalty.Value;

        // Give a penalty to isolated pawns.
        whiteEval += IsolatedPawnsCount(Board.Pawns[0]) * evaluationData.IsolatedPawnPenalty.Value;
        blackEval += IsolatedPawnsCount(Board.Pawns[1]) * evaluationData.IsolatedPawnPenalty.Value;

        // Give a penalty to backward pawns.
        whiteEval += BackwardPawnsCount(0, 1, Board.Pawns[0]) * evaluationData.BackwardPawnPenalty.Value;
        blackEval += BackwardPawnsCount(1, 0, Board.Pawns[1]) * evaluationData.BackwardPawnPenalty.Value;


        // Reduce the values of pawns in case of material imbalance.
        float materialImbalance = Mathf.Abs(whiteMaterial - blackMaterial) / 100;
        int whiteMaterialImbalanceMultiplier = (int)(materialImbalance * whitePawnCount);
        int blackMaterialImbalanceMultiplier = (int)(materialImbalance * blackPawnCount);
        whiteEval += whiteMaterialImbalanceMultiplier * evaluationData.MaterialImbalancePawnPenaltyPerPawn.Value;
        blackEval += blackMaterialImbalanceMultiplier * evaluationData.MaterialImbalancePawnPenaltyPerPawn.Value;


        // Give a bonus to bishop/knight pairs depending on wheter the position is open or closed.
        if (IsPositionOpen())
        {
            if (whiteKnightCount == 2) whiteEval += evaluationData.KnightPairInOpenPositionBonus.Value;
            if (whiteBishopCount == 2) whiteEval += evaluationData.BishopPairInOpenPositionBonus.Value;

            if (blackKnightCount == 2) blackEval += evaluationData.KnightPairInOpenPositionBonus.Value;
            if (blackBishopCount == 2) blackEval += evaluationData.BishopPairInOpenPositionBonus.Value;
        }

        else
        {
            if (whiteKnightCount == 2) whiteEval += evaluationData.KnightPairInClosedPositionBonus.Value;
            if (whiteBishopCount == 2) whiteEval += evaluationData.BishopPairInClosedPositionBonus.Value;

            if (blackKnightCount == 2) blackEval += evaluationData.KnightPairInClosedPositionBonus.Value;
            if (blackBishopCount == 2) blackEval += evaluationData.BishopPairInClosedPositionBonus.Value;
        }


        // Give a bonus based on piece mobility.
        whiteEval += MobilityScore(Board.AttackedSquares[0]);
        blackEval += MobilityScore(Board.AttackedSquares[1]);


        // Give a bonus in case of a well protected king.
        whiteEval += KingSafetyScore(0, Board.Pawns[0], Board.Pawns[1]);
        blackEval += KingSafetyScore(1, Board.Pawns[1], Board.Pawns[0]);


        // Give a bonus to bishops that help with colour weakness caused by an imbalanced pawn structure.
        whiteEval += ColourWeaknessScore(Board.Pawns[0], Board.Bishops[0]);
        blackEval += ColourWeaknessScore(Board.Pawns[1], Board.Bishops[1]);


        // Give a bonus to knights defended by a pawn in the opponent's half of the board.
        whiteEval += KnightOutpostsCount(Board.PawnAttackedSquares[0], Board.PawnAttackedSquares[1], Board.Knights[0], Mask.BlackHalf) * evaluationData.KnightOutpostBonus.Value;
        blackEval += KnightOutpostsCount(Board.PawnAttackedSquares[1], Board.PawnAttackedSquares[0], Board.Knights[1], Mask.WhiteHalf) * evaluationData.KnightOutpostBonus.Value;

        // Give a bonus to knights defended by a pawn in the opponent's half of the board.
        whiteEval += BishopOutpostsCount(Board.PawnAttackedSquares[0], Board.PawnAttackedSquares[1], Board.Bishops[0], Mask.BlackHalf) * evaluationData.BishopOutpostBonus.Value;
        blackEval += BishopOutpostsCount(Board.PawnAttackedSquares[1], Board.PawnAttackedSquares[0], Board.Bishops[1], Mask.WhiteHalf) * evaluationData.BishopOutpostBonus.Value;


        int eval = (whiteEval - blackEval) * (Board.CurrentTurn == 0 ? 1 : -1);

        Board.EvaluationTimer.Stop();
        return eval;


        int EarlygameMaterial(int turnIndex, out int pawnMaterial, out int pawnCount, out int knightCount, out int bishopCount)
        {
            int material = 0;

            pawnCount = BitboardUtility.OccupiedSquaresCount(Board.Pawns[turnIndex]);
            material += pawnMaterial = pawnCount * StaticPieceValues[Piece.Pawn][0];

            knightCount = BitboardUtility.OccupiedSquaresCount(Board.Knights[turnIndex]);
            material += knightCount * StaticPieceValues[Piece.Knight][0];

            bishopCount = BitboardUtility.OccupiedSquaresCount(Board.Bishops[turnIndex]);
            material += bishopCount * StaticPieceValues[Piece.Bishop][0];

            material += BitboardUtility.OccupiedSquaresCount(Board.Rooks[turnIndex]) * StaticPieceValues[Piece.Rook][0];
            material += BitboardUtility.OccupiedSquaresCount(Board.Queens[turnIndex]) * StaticPieceValues[Piece.Queen][0];
            return material;
        }

        int LategameMaterial(int turnIndex)
        {
            int material = 0;
            material += BitboardUtility.OccupiedSquaresCount(Board.Pawns[turnIndex]) * StaticPieceValues[Piece.Pawn][1];
            material += BitboardUtility.OccupiedSquaresCount(Board.Knights[turnIndex]) * StaticPieceValues[Piece.Knight][1];
            material += BitboardUtility.OccupiedSquaresCount(Board.Bishops[turnIndex]) * StaticPieceValues[Piece.Bishop][1];
            material += BitboardUtility.OccupiedSquaresCount(Board.Rooks[turnIndex]) * StaticPieceValues[Piece.Rook][1];
            material += BitboardUtility.OccupiedSquaresCount(Board.Queens[turnIndex]) * StaticPieceValues[Piece.Queen][1];
            return material;
        }


        int PassedPawnsBonus(int colourIndex, ulong pawns, ulong opponentPawns)
        {
            int bonus = 0;
            for (int i = 1; i < 7; i++)
            {
                int count = 0;

                foreach (var pawn in Board.GetIndexes(pawns & Board.Ranks[colourIndex][i]))
                {
                    // If there are no enemy pawns on this or adjacent files,
                    // and the pawn doesn't have another friendly pawn in front,
                    // it is considered a "passed pawn".
                    if ((Board.Spans[colourIndex, pawn] & opponentPawns) == 0)
                        if ((Board.Fills[colourIndex, pawn] & pawns) == 0) count++;
                }

                bonus += evaluationData.PassedPawnBonus[i].Value * count;
            }
            return bonus;
        }

        int DoubledPawnsCount(ulong pawns)
        {
            int count = 0;
            for (int i = 0; i < 8; i++)
            {
                // If there are multiple pawns on this file,
                // they are considered "doubled pawns".
                count += Mathf.Max(0, BitboardUtility.OccupiedSquaresCount(pawns & Board.Files[i]) - 1);
            }
            return count;
        }

        int IsolatedPawnsCount(ulong pawns)
        {
            int count = 0;
            foreach (var pawn in Board.GetIndexes(pawns))
            {
                // If there are no friendly pawns in the pawn's neighbouring
                // files, it is considered an "isolated pawn".
                if ((Board.NeighbouringFiles[Board.GetFile(pawn)] & pawns) == 0) count++;
            }
            return count;
        }

        int BackwardPawnsCount(int colourIndex, ulong opponentColourIndex, ulong pawns)
        {
            int count = 0;
            foreach (var pawn in Board.GetIndexes(pawns))
            {
                // If there are no friendly pawns protecting the pawn
                // and its stop square is attacked by an enemy pawn,
                // it is considered a "backward pawn".
                if ((Board.BackwardProtectors[colourIndex, pawn] & pawns) == 0) 
                    if ((Board.StopSquare[colourIndex, pawn] & Board.PawnAttackedSquares[opponentColourIndex]) != 0) count++;
            }
            return count;
        }


        bool IsPositionOpen() => BitboardUtility.OccupiedSquaresCount(Board.AllOccupiedSquares) < 24;


        int MobilityScore(ulong attackMap)
        {
            return
                BitboardUtility.OccupiedSquaresCount(attackMap) *
                evaluationData.MobilityBonusPerSquare.Value;
        }

        int KingSafetyScore(int colourIndex, ulong friendlyPawns, ulong opponentPawns)
        {
            int score = 0;

            if ((Board.Kings[colourIndex] & (colourIndex == 0 ? Mask.WhiteCastledKingPosition : Mask.BlackCastledKingPosition)) != 0)
            {
                score +=
                    BitboardUtility.OccupiedSquaresCount(friendlyPawns & Board.FirstShieldingPawns[Board.KingPosition[colourIndex]]) * evaluationData.FirstShieldingPawnKingSafetyBonus.Value +
                    BitboardUtility.OccupiedSquaresCount(friendlyPawns & Board.SecondShieldingPawns[Board.KingPosition[colourIndex]]) * evaluationData.SecondShieldingPawnKingSafetyBonus.Value;
            }

            int kingFile = Board.GetFile(Board.KingPosition[colourIndex]);

            ulong[] kingFiles = new ulong[3];
            if (kingFile == 0) kingFiles = new[] { Board.Files[0], Board.Files[1] };
            else if (kingFile == 7) kingFiles = new[] { Board.Files[6], Board.Files[7] };
            else kingFiles = new[] { Board.Files[kingFile - 1], Board.Files[kingFile], Board.Files[kingFile + 1] };

            foreach (var file in kingFiles)
            {
                int friendlyPawnsCount = BitboardUtility.OccupiedSquaresCount(friendlyPawns & file);
                int opponentPawnsCount = BitboardUtility.OccupiedSquaresCount(opponentPawns & file);

                if (friendlyPawnsCount == 0)
                {
                    if (opponentPawnsCount != 0) score += evaluationData.HalfOpenFileNextToKingPenalty.Value;
                    else score += evaluationData.OpenFileNextToKingPenalty.Value;
                }
            }

            return score;
        }

        int ColourWeaknessScore(ulong pawns, ulong bishops)
        {
            int score = 0;

            int lightPawnsCount = BitboardUtility.OccupiedSquaresCount(pawns & Mask.LightSquares);
            int darkPawnsCount = BitboardUtility.OccupiedSquaresCount(pawns & Mask.DarkSquares);

            int lightBishopsCount = BitboardUtility.OccupiedSquaresCount(bishops & Mask.LightSquares);
            int darkBishopsCount = BitboardUtility.OccupiedSquaresCount(bishops & Mask.DarkSquares);

            // Give a penalty for the lack of a bishop on a light square, for each extra pawn on a dark square.
            score += (lightBishopsCount == 0 ? 1 : 0) * Mathf.Max(0, darkPawnsCount - lightPawnsCount) * evaluationData.ColourWeaknessPenaltyPerPawn.Value;

            // Give a penalty for the lack of a bishop on a light square, for each extra pawn on a dark square.
            score += (darkBishopsCount == 0 ? 1 : 0) * Mathf.Max(0, lightPawnsCount - darkPawnsCount) * evaluationData.ColourWeaknessPenaltyPerPawn.Value;

            return score;
        }

        int KnightOutpostsCount(ulong pawnDefendedSquares, ulong opponentPawnAttackedSquares, ulong knights, ulong opponentHalf)
        {
            // If a knight is in the opponent's half of the board (or in the center),
            // is defended by a pawn and is not under attack by an opponent pawn,
            // it is considered an "outpost".
            return BitboardUtility.OccupiedSquaresCount(pawnDefendedSquares & ~opponentPawnAttackedSquares & knights & opponentHalf);
        }

        int BishopOutpostsCount(ulong pawnDefendedSquares, ulong opponentPawnAttackedSquares, ulong bishops, ulong opponentHalf)
        {
            // If a bishop is in the opponent's half of the board (or in the center),
            // is defended by a pawn and is not under attack by an opponent pawn,
            // it is considered an "outpost".
            return BitboardUtility.OccupiedSquaresCount(pawnDefendedSquares & ~opponentPawnAttackedSquares & bishops & opponentHalf);
        }
    }


    public static int GetPieceValue(int piece, int pieceColour, int gamePhase)
    {
        switch (piece)
        {
            case Piece.Pawn :   return StaticPieceValues[Piece.Pawn][0];
            case Piece.Knight:  return StaticPieceValues[Piece.Knight][0];
            case Piece.Bishop:  return StaticPieceValues[Piece.Bishop][0];
            case Piece.Rook:    return StaticPieceValues[Piece.Rook][0];
            case Piece.Queen:   return StaticPieceValues[Piece.Queen][0];
            default:            return 0;
        }
    }

    public static int GetGamePhase(int whiteMaterial, int blackMaterial, int whitePawnMaterial, int blackPawnMaterial) => 
        (whiteMaterial + blackMaterial) - (whitePawnMaterial + blackPawnMaterial);

    public static bool PawnsOnly(EvaluationData evaluationData)
    {
        int materialCount = 0;
        materialCount += BitboardUtility.OccupiedSquaresCount(Board.Knights[Board.CurrentTurn]);
        materialCount += BitboardUtility.OccupiedSquaresCount(Board.Bishops[Board.CurrentTurn]);
        materialCount += BitboardUtility.OccupiedSquaresCount(Board.Rooks[Board.CurrentTurn]);
        materialCount += BitboardUtility.OccupiedSquaresCount(Board.Queens[Board.CurrentTurn]);
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

            mopUpScore += Mathf.Max(3 - opponentKingFile, opponentKingFile - 4) + Mathf.Max(3 - opponentKingRank, opponentKingRank - 4);

            mopUpScore += 14 - (Mathf.Abs(friendlyKingFile - opponentKingFile) + Mathf.Abs(friendlyKingRank - opponentKingRank));

            return ((mopUpScore * 10 * (OpeningPhaseScore - gamePhase)) / OpeningPhaseScore);
        }
        return 0;
    }


    public static int Interpolate(int gamePhase, int earlygameValue, int endgameValue)
    {
        if (gamePhase > OpeningPhaseScore) return earlygameValue;
        else if (gamePhase < EndgamePhaseScore) return endgameValue;

        else
        {
            // Interpolate for middle game
            return (earlygameValue * gamePhase +
                endgameValue * (OpeningPhaseScore - gamePhase)
                ) / OpeningPhaseScore;
        }
    }
}


public class PieceSquareTables
{
    public static int EvaluatePieceSquareTables(int turnIndex, int gamePhase)
    {
        int value = 0;
        bool isWhite = turnIndex == 0;
        value += EvaluatePieceSquareTable(Piece.Pawn, Board.Pawns[turnIndex], isWhite, gamePhase);
        value += EvaluatePieceSquareTable(Piece.Rook, Board.Rooks[turnIndex], isWhite, gamePhase);
        value += EvaluatePieceSquareTable(Piece.Knight, Board.Knights[turnIndex], isWhite, gamePhase);
        value += EvaluatePieceSquareTable(Piece.Bishop, Board.Bishops[turnIndex], isWhite, gamePhase);
        value += EvaluatePieceSquareTable(Piece.Queen, Board.Queens[turnIndex], isWhite, gamePhase);
        value += EvaluatePieceSquareTable(Piece.King, Board.Kings[turnIndex], isWhite, gamePhase);
        return value;
    }

    public static int EvaluatePieceSquareTable(int pieceType, ulong pieceList, bool isWhite, int gamePhase)
    {
        List<int> pieces = Board.GetIndexes(pieceList);
        int value = 0;
        foreach (var piece in pieces)
        {
            value += Read(pieceType, piece, isWhite, gamePhase);
        }
        return value;
    }

    public static int Read(int pieceType, int square, bool isWhite, int gamePhase)
    {
        //if (isWhite) square = 63 - square;
        //return Evaluation.Interpolate(gamePhase, s_earlygamePieceSquareTables[pieceType][square], s_endgamePieceSquareTables[pieceType][square]);

        int rank = Board.GetRank(square);
        int file = Board.GetFile(square);
        
        if (!isWhite) rank = 7 - rank;
        
        if (pieceType != Piece.Pawn) return s_pieceSquareTables[pieceType][rank][FileIndex()].Interpolate(gamePhase);
        return s_pawnPieceSquareTables[rank][file].Interpolate(gamePhase);
        
        
        int FileIndex() => file >= 4 ? 7 - file : file;
    }


    // Piece square tables are represented vertically symmetrical to what we would see them as.
    // This means that no extra operations are needed for black, and white has index = 63 - square.

    private static readonly Dictionary<int, int[]> s_earlygamePieceSquareTables = new()
    {
        [Piece.Pawn] = new int[] {
       0,   0,   0,   0,   0,   0,   0,   0,
      -1,  45,  49,  47,  47,  61,  68,  -4,
      -6,  14,  24,  24,  33,  25,  16,   6,
      11,  -1,   8,  20,  28,   9,  -1,   0,
      -6,   6,  -5,  14,  14,   6,   4,   6,
       5,  -1, -12,   2,   4,  -4,  -8,  -1,
       8,  15,  13, -14, -14,  16,  16,   5,
       0,   0,   0,   0,   0,   0,   0,   0 },

        [Piece.Knight] = new int[] {
     -50, -40, -30, -26, -28, -30, -40, -55,
     -40, -17,   3,   4,  -6,   0, -15, -37,
     -29,   6,   6,  11,  12,  16,   5, -25,
     -26,  11,   9,  18,  14,  21,   5, -24,
     -24,   2,  21,  24,  23,   9,  -5, -36,
     -25,  11,   4,  20,  19,   4,  -1, -32,
     -34, -18,  -5,   8,  -1,   4, -22, -38,
     -50, -34, -25, -36, -24, -32, -46, -50 },

        [Piece.Bishop] = new int[] {
     -20, -10, -12, -10,  -5, -12, -15, -16,
      -6,   3,  -5,  -6,   1,   6,   5, -13,
      -5,  -6,  -1,   7,  16,  -1,   6, -16,
     -13,  11,  10,   4,  14,  11,  -1, -14,
     -16,   2,   6,   4,  16,  12,   5,  -4,
     -15,  16,   4,  16,   8,  14,   4, -15,
      -7,   9,   6,   3,   6,   6,   6,  -5,
     -14, -12,  -4,  -9,  -4, -15,  -4, -14 },

        [Piece.Rook] = new int[] {
      -2,   4,  -6,  -2,   2,   6,  -2,   5,
       4,  16,  15,  11,  15,  11,  13,   8,
      -5,   3,  -2,   1,   6,   3,   3,  -6,
      -2,   3,  -6,  -1,  -4,  -4,   5, -10,
       1,  -5,   1,   4,  -2,   5,   3,  -4,
      -9,   1,   6,   5,  -3,   1,   1,   0,
      -9,  -6,  -6,   5,   0,  -4,  -1, -10,
      -5,   4,   5,   9,   9,  -6,  -2,  -1 },

        [Piece.Queen] = new int[] {
     -17, -10, -13,  -7,  -3, -11,  -9, -25,
      -5,   4,   6,  -1,  -5,   4,  -6,  -4,
      -5,  -4,   6,   7,   0,   2,  -5,  -8,
       1,   0,  11,   7,  -1,   7,  -4,   0,
      -2,  -6,   2,  -1,   1,   7,   4,  -6,
     -15,   6,  11,   4,  11,  11,  11, -15,
     -10,   3,  -3,   3,  -6,   1,  -6,  -5,
     -24,  -8, -16,  -3,  -8, -13,  -4, -15 },

        [Piece.King] = new int[] {
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
        [Piece.Pawn] = new int[] {
       0,   0,   0,   0,   0,   0,   0,   0,
       4,  68,  98,  85,  94, 120, 174,  -4,
      -6,  37,  38,  31,  45,  44,  48,   6,
      -2,  -2,  -1,   2,  -6,  -1,  -4,  -6,
      -3,   4,  -5,   0,  -3,   5,   2,   2,
       1,   0,  -1,   0,   5,   1,   0,  -2,
      -4,   4,   3,   0,  -6,   6,   5,  -2,
       0,   0,   0,   0,   0,   0,   0,   0 },

        [Piece.Knight] = new int[] {
     -50, -40, -35, -24, -24, -30, -40, -50,
     -40, -15,  -4,   5,  -5,   6, -17, -38,
     -26,  -6,  10,  15,   9,  15,   3, -24,
     -28,  10,   9,  18,  17,  21,   5, -29,
     -26,   5,  20,  14,  16,  18,  -5, -36,
     -27,   9,  15,  11,  20,   5,   7, -32,
     -40, -22,   1,   5,  -1,   5, -20, -43,
     -50, -35, -25, -30, -27, -32, -40, -50 },

        [Piece.Bishop] = new int[] {
     -20, -16,  -9, -14,  -7,  -4, -13, -14,
      -4,   5,  -3,   4,  -6,   3,   6, -11,
     -10,  -5,  -1,   4,  15,   5,  -3, -11,
     -13,   7,  11,   5,  16,  11,  -1,  -7,
     -16,   4,  12,   6,  16,  10,   4,  -4,
     -12,   7,   7,  10,  12,  11,   4,  -4,
      -7,   1,   2,  -3,   6,   6,   7, -11,
     -17,  -6, -10, -10,  -4, -11,  -4, -15 },

        [Piece.Rook] = new int[] {
      -3,   6,  -6,  -4,  -4,   1,  -6,   5,
     -15,   4,   3,  -1,   5,   2,   4,  -6,
      -9,   5,  -6,  -1,   0,   3,   3, -15,
      -4,  -4,  -3,  -3,  -6,   0,   6, -16,
     -10,  -6,   0,   6,  -6,   2,   6, -15,
     -15,   0,   5,   6,  -2,   3,  -1,  -6,
      -5,  -6,  -5,   3,  -4,   1,  -4,  -8,
       0,   2,   4,   1,   1,  -2,   0,   1 },

        [Piece.Queen] = new int[] {
     -16, -10, -15,  -8,   1,  -6,  -7,  -21,
     -10,   3,   6,   2,  -4,   3,  -5,   -4,
      -6,  -4,  10,   6,   2,   7,  -2,  -13,
      -2,  -2,   8,   8,   1,   3,  -4,   -1,
      -3,   0,   1,  -1,   1,   8,   6,    0,
     -10,   4,   9,   7,   3,   6,  10,  -11,
     -10,   4,  -5,   0,   0,   5,  -6,  -12,
     -20,  -9, -12,  -4,  -7,  -7,  -6,  -20 },

        [Piece.King] = new int[] {
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
    private static readonly Score[][][] s_pieceSquareTables =
    {
        /* None */
        new Score[][] { },
        
        /* Pawn (separate table) */
        new Score[][] { },

        /* Knight */
        new Score[][] {
            new Score[] { new(-175, -96), new( -92, -65), new( -74, -49), new( -73, -21) },
            new Score[] { new( -77, -67), new( -41, -54), new( -27, -18), new( -15,   8) },
            new Score[] { new( -61, -40), new( -17, -27), new(   6,  -8), new(  12,  29) },
            new Score[] { new( -35, -35), new(   8,  -2), new(  40,  13), new(  49,  28) },
            new Score[] { new( -34, -45), new(  13, -16), new(  44,   9), new(  51,  39) },
            new Score[] { new(  -9, -51), new(  22, -44), new(  58, -16), new(  53,  17) },
            new Score[] { new( -67, -69), new( -27, -50), new(   4, -51), new(  37,  12) },
            new Score[] { new(-201,-100), new( -83, -88), new( -56, -56), new( -26, -17) }
        },

        new Score[][] { /* Bishop */
            new Score[] { new( -37, -40), new(  -4, -21), new(  -6, -26), new( -16,  -8) },
            new Score[] { new( -11, -26), new(   6,  -9), new(  13, -12), new(   3,   1) },
            new Score[] { new( -5 , -11), new(  15,  -1), new(  -4,  -1), new(  12,   7) },
            new Score[] { new( -4 , -14), new(   8,  -4), new(  18,   0), new(  27,  12) },
            new Score[] { new( -8 , -12), new(  20,  -1), new(  15, -10), new(  22,  11) },
            new Score[] { new( -11, -21), new(   4,   4), new(   1,   3), new(   8,   4) },
            new Score[] { new( -12, -22), new( -10, -14), new(   4,  -1), new(   0,   1) },
            new Score[] { new( -34, -32), new(   1, -29), new( -10, -26), new( -16, -17) }
        },

        new Score[][] { /* Rook */
            new Score[] { new( -31,  -9), new( -20, -13), new( -14, -10), new(  -5,  -9) },
            new Score[] { new( -21, -12), new( -13,  -9), new(  -8,  -1), new(   6,  -2) },
            new Score[] { new( -25,   6), new( -11,  -8), new(  -1,  -2), new(   3,  -6) },
            new Score[] { new( -13,  -6), new(  -5,   1), new(  -4,  -9), new(  -6,   7) },
            new Score[] { new( -27,  -5), new( -15,   8), new(  -4,   7), new(   3,  -6) },
            new Score[] { new( -22,   6), new(  -2,   1), new(   6,  -7), new(  12,  10) },
            new Score[] { new(  -2,   4), new(  12,   5), new(  16,  20), new(  18,  -5) },
            new Score[] { new( -17,  18), new( -19,   0), new(  -1,  19), new(   9,  13) }
        },

        new Score[][] { /* Queen */
            new Score[] { new(   3, -69), new(  -5, -57), new(  -5, -47), new(   4, -26) },
            new Score[] { new(  -3, -54), new(   5, -31), new(   8, -22), new(  12,  -4) },
            new Score[] { new(  -3, -39), new(   6, -18), new(  13,  -9), new(   7,   3) },
            new Score[] { new(   4, -23), new(   5,  -3), new(   9,  13), new(   8,  24) },
            new Score[] { new(   0, -29), new(  14,  -6), new(  12,   9), new(   5,  21) },
            new Score[] { new(  -4, -38), new(  10, -18), new(   6, -11), new(   8,   1) },
            new Score[] { new(  -5, -50), new(   6, -27), new(  10, -24), new(   8,  -8) },
            new Score[] { new(  -2, -74), new(  -2, -52), new(   1, -43), new(  -2, -34) }
        },

        new Score[][] { /* King */
            new Score[] { new( 271,   1), new( 327,  45), new( 271,  85), new( 198,  76) },
            new Score[] { new( 278,  53), new( 303, 100), new( 234, 133), new( 179, 135) },
            new Score[] { new( 195,  88), new( 258, 130), new( 169, 169), new( 120, 175) },
            new Score[] { new( 164, 103), new( 190, 156), new( 138, 172), new(  98, 172) },
            new Score[] { new( 154,  96), new( 179, 166), new( 105, 199), new(  70, 199) },
            new Score[] { new( 123,  92), new( 145, 172), new(  81, 184), new(  31, 191) },
            new Score[] { new(  88,  47), new( 120, 121), new(  65, 116), new(  33, 131) },
            new Score[] { new(  59,  11), new(  89,  59), new(  45,  73), new(  -1,  78) }
        }
    };

    // Values from Stockfish.
    // Accessed by s_pawnPieceSquareTables[rankIndex][fileIndex].
    private static readonly Score[][] s_pawnPieceSquareTables =
    {
        new Score[] { },
        new Score[] { new(  2, -8), new(  4, -6), new( 11,  9), new( 18,  5), new( 16, 16), new( 21,  6), new(  9, -6), new( -3,-18) },
        new Score[] { new( -9, -9), new(-15, -7), new( 11,-10), new( 15,  5), new( 31,  2), new( 23,  3), new(  6, -8), new(-20, -5) },
        new Score[] { new( -3,  7), new(-20,  1), new(  8, -8), new( 19, -2), new( 39,-14), new( 17,-13), new(  2,-11), new( -5, -6) },
        new Score[] { new( 11, 12), new( -4,  6), new(-11,  2), new(  2, -6), new( 11, -5), new(  0, -4), new(-12, 14), new(  5,  9) },
        new Score[] { new(  3, 27), new(-11, 18), new( -6, 19), new( 22, 29), new( -8, 30), new( -5,  9), new(-14,  8), new(-11, 14) },
        new Score[] { new( -7, -1), new(  6,-14), new( -2, 13), new(-11, 22), new(  4, 24), new(-14, 17), new( 10,  7), new( -9,  7) }
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
    public Score[] PieceValues =
    {
        new(   0,   0), /* None */
        new( 126, 208), /* Pawn */
        new( 781, 854), /* Knight */
        new( 825, 915), /* Bishop */
        new(1276,1380), /* Rook */
        new(2538,2682)  /* Queen */
    };


    public Score[] PassedPawnBonus = 
    {
        new(   +0,   +0),
        new(   +2,  +38),
        new(  +15,  +36),
        new(  +22,  +50),
        new(  +64,  +81),
        new( +166, +184),
        new( +284, +269),
        new(   +0,   +0),
    };

    public Score DoubledPawnPenalty = new(-11, -51);
    public Score IsolatedPawnPenalty = new(-1, -20);
    public Score BackwardPawnPenalty = new(-6, -10);

    // Not from Stockfish.
    public Score MaterialImbalancePawnPenaltyPerPawn = new(-5, -3);

    // Not from Stockfish.
    public Score KnightPairInOpenPositionBonus = new(+20, +10);
    public Score BishopPairInOpenPositionBonus = new(+60, +40);
    public Score KnightPairInClosedPositionBonus = new(+50, +30);
    public Score BishopPairInClosedPositionBonus = new(+30, +10);

    // Not from Stockfish.
    public Score MobilityBonusPerSquare = new(+5, +3);

    // Not from Stockfish.
    public Score FirstShieldingPawnKingSafetyBonus = new(+20, +10);
    public Score SecondShieldingPawnKingSafetyBonus = new(+10, +5);

    // Not from Stockfish.
    public Score HalfOpenFileNextToKingPenalty = new(-20, -30);
    public Score OpenFileNextToKingPenalty = new(-40, -50);

    public Score ColourWeaknessPenaltyPerPawn = new(-3, -8);

    public Score KnightOutpostBonus = new(+54, +34);
    public Score BishopOutpostBonus = new(+31, +25);

    public EvaluationData()
    {
        ResetAllValues();
    }


    public void InterpolateAll(int gamePhase)
    {
        foreach (var value in PieceValues) value.Interpolate(gamePhase);

        foreach (var bonus in PassedPawnBonus) bonus.Interpolate(gamePhase);
        DoubledPawnPenalty.Interpolate(gamePhase);
        IsolatedPawnPenalty.Interpolate(gamePhase);
        BackwardPawnPenalty.Interpolate(gamePhase);

        MaterialImbalancePawnPenaltyPerPawn.Interpolate(gamePhase);

        KnightPairInOpenPositionBonus.Interpolate(gamePhase);
        BishopPairInOpenPositionBonus.Interpolate(gamePhase);
        KnightPairInClosedPositionBonus.Interpolate(gamePhase);
        BishopPairInClosedPositionBonus.Interpolate(gamePhase);

        MobilityBonusPerSquare.Interpolate(gamePhase);

        FirstShieldingPawnKingSafetyBonus.Interpolate(gamePhase);
        SecondShieldingPawnKingSafetyBonus.Interpolate(gamePhase);

        HalfOpenFileNextToKingPenalty.Interpolate(gamePhase);
        OpenFileNextToKingPenalty.Interpolate(gamePhase);

        ColourWeaknessPenaltyPerPawn.Interpolate(gamePhase);

        KnightOutpostBonus.Interpolate(gamePhase);
        BishopOutpostBonus.Interpolate(gamePhase);
    }

    public void SetAllValuesToInspector()
    {
        foreach (var value in PieceValues) value.SetValuesToInspector();

        foreach (var bonus in PassedPawnBonus) bonus.SetValuesToInspector();
        DoubledPawnPenalty.SetValuesToInspector();
        IsolatedPawnPenalty.SetValuesToInspector();
        BackwardPawnPenalty.SetValuesToInspector();

        MaterialImbalancePawnPenaltyPerPawn.SetValuesToInspector();

        KnightPairInOpenPositionBonus.SetValuesToInspector();
        BishopPairInOpenPositionBonus.SetValuesToInspector();
        KnightPairInClosedPositionBonus.SetValuesToInspector();
        BishopPairInClosedPositionBonus.SetValuesToInspector();

        MobilityBonusPerSquare.SetValuesToInspector();

        FirstShieldingPawnKingSafetyBonus.SetValuesToInspector();
        SecondShieldingPawnKingSafetyBonus.SetValuesToInspector();

        HalfOpenFileNextToKingPenalty.SetValuesToInspector();
        OpenFileNextToKingPenalty.SetValuesToInspector();

        ColourWeaknessPenaltyPerPawn.SetValuesToInspector();

        KnightOutpostBonus.SetValuesToInspector();
        BishopOutpostBonus.SetValuesToInspector();
    }

    public void ResetAllValues()
    {
        PieceValues = new Score[]
        {
            new(   0,   0),
            new( 126, 208),
            new( 781, 854),
            new( 825, 915),
            new(1276,1380),
            new(2538,2682)
        };


        PassedPawnBonus = new Score[] 
        {
            new(   +0,   +0),
            new(   +2,  +38),
            new(  +15,  +36),
            new(  +22,  +50),
            new(  +64,  +81),
            new( +166, +184),
            new( +284, +269),
            new(   +0,   +0),
        };

        DoubledPawnPenalty = new(-11, -51);
        IsolatedPawnPenalty = new(-1, -20);
        BackwardPawnPenalty = new(-6, -10);

        MaterialImbalancePawnPenaltyPerPawn = new(-5, -3);

        KnightPairInOpenPositionBonus = new(+20, +10);
        BishopPairInOpenPositionBonus = new(+60, +40);
        KnightPairInClosedPositionBonus = new(+50, +30);
        BishopPairInClosedPositionBonus = new(+30, +10);

        MobilityBonusPerSquare = new(+5, +3);

        FirstShieldingPawnKingSafetyBonus = new(+20, +10);
        SecondShieldingPawnKingSafetyBonus = new(+10, +5);

        HalfOpenFileNextToKingPenalty = new(-20, -30);
        OpenFileNextToKingPenalty = new(-40, -50);

        ColourWeaknessPenaltyPerPawn = new(-3, -8);

        KnightOutpostBonus = new(+54, +34);
        BishopOutpostBonus = new(+31, +25);
    }
}

[System.Serializable]
public class Score
{
    [SerializeField]
    private int _opening;
    [SerializeField]
    private int _endgame;

    public Score(int opening, int endgame)
    {
        Opening = opening;
        Endgame = endgame;

        _opening = Opening;
        _endgame = Endgame;
    }

    
    public int Opening { get; private set; }

    public int Endgame { get; private set; }

    public int Value { get; private set; }


    public int Interpolate(int gamePhase)
    {
        if (gamePhase > Evaluation.OpeningPhaseScore) Value = Opening;
        else if (gamePhase < Evaluation.EndgamePhaseScore) Value = Endgame;

        else
        {
            // Interpolate for middle game
            Value = (Opening * gamePhase +
                Endgame * (Evaluation.OpeningPhaseScore - gamePhase)
                ) / Evaluation.OpeningPhaseScore;
        }

        return Value;
    }

    public void SetValuesToInspector()
    {
        Opening = _opening;
        Endgame = _endgame;
    }
}
