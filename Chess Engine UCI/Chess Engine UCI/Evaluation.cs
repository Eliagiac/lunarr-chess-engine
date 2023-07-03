using static Utilities.Bitboard;
using static Score;
using static Piece;
using static Evaluation;

public class Evaluation
{
    /// <summary>The static value of each piece type, based on gamephase.</summary>
    /// <remarks>Used to count material when gamephase information isn't yet available. <br />
    /// Indexed by [PieceType][GamePhase].
    /// </remarks>
    public static short[][] StaticPieceValues =
    {
        new short[] {    0,    0 }, /* None */
        new short[] {  126,  208 }, /* Pawn */
        new short[] {  781,  854 }, /* Knight */
        new short[] {  825,  915 }, /* Bishop */
        new short[] { 1276, 1380 }, /* Rook */
        new short[] { 2538, 2682 }  /* Queen */
    };

    /// <summary>The default value of each piece type.</summary>
    /// <remarks>Indexed by [PieceType].</remarks>
    public static uint[] PieceValues =
        StaticPieceValues.Select(s => S(s[0], s[1])).ToArray();


    /// <summary>Bonus given to pawns that are not blocked by enemy pawns.</summary>
    /// <remarks>The pawn has a clear path to promotion, and should be valued and protected.<br />
    /// Indexed by [RankIndex].</remarks>
    public static uint[] PassedPawnBonus =
    {
        S(   +0,   +0),
        S(   +2,  +38),
        S(  +15,  +36),
        S(  +22,  +50),
        S(  +64,  +81),
        S( +166, +184),
        S( +284, +269),
        S(   +0,   +0),
    };

    /// <summary>Penalty for two or more pawns (of the same color) on one file.</summary>
    /// <remarks>The pawns are less effective because they block each other's movement.</remarks>
    public static uint DoubledPawnPenalty = S(-11, -51);

    /// <summary>Penalty for a pawn with no friendly pawns on neighbouring files.</summary>
    /// <remarks>The pawn is more vulnerable, as well as less useful to other pawns.</remarks>
    public static uint IsolatedPawnPenalty = S(-1, -20);

    /// <summary>Penalty for a pawn whose stop square is controlled by an enemy pawn, and not defended by any friendly pawns.</summary>
    /// <remarks>The pawn is likely to never be able to make progress.</remarks>
    public static uint BackwardPawnPenalty = S(-6, -10);

    /// <summary>Penalty for all pawns based on material imbalance.</summary>
    /// <remarks>If one side is up material, pieces gain importance.</remarks>
    public static uint MaterialImbalancePawnPenalty = S(-5, -3);

    /// <summary>Bonus for having two or more knights.</summary>
    /// <remarks>The knights gain strength when both are available.<br />
    /// Indexed by [OpenPosition]</remarks>
    public static uint[] KnightPairBonus = { S(+50, +30), S(+20, +10) };

    /// <summary>Bonus for having two or more bishops.</summary>
    /// <remarks>The bishops gain strength when both are available.<br />
    /// Indexed by [OpenPosition]</remarks>
    public static uint[] BishopPairBonus = { S(+30, +10), S(+60, +40) };

    /// <summary>Bonus based on how much mobility a piece has.</summary>
    /// <remarks>Pieces are likely to be stronger if they control many squares.<br />
    /// Indexed by [PieceType][ControlledSquares]</remarks>
    public static uint[][] MobilityBonus =
    {
        /* None */
        new uint[] { },

        /* Pawn */
        new uint[] { },

        /* Knight */
        new uint[] { S(-62,-81), S(-53,-56), S(-12,-30), S( -4,-14), S(  3,  8), S( 13, 15), S( 22, 23), S( 28, 27), S( 33, 33) },
    
        /* Bishop */
        new uint[] { S(-48, -59), S(-20, -23), S(16, -3), S(26, 13), S(38, 24), S(51, 42), S(55, 54), S(63, 57), S(63, 65), S(68, 73), S(81, 78), S(81, 86), S(91, 88), S(98, 97) },
        
        /* Rook */
        new uint[] { S(-58, -76), S(-27, -18), S(-15, 28), S(-10, 55), S(-5, 69), S(-2, 82), S(9, 112), S(16, 118), S(30, 132), S(29, 142), S(32, 155), S(38, 165), S(46, 166), S(48, 169), S(58, 171) },

        /* Queen */
        new uint[] { S(-39, -36), S(-21, -15), S(3, 8), S(3, 18), S(14, 34), S(22, 54), S(28, 61), S(41, 73), S(43, 79), S(48, 92), S(56, 94), S(60, 104), S(60, 113), S(66, 120), S(67, 123), S(70, 126), S(71, 133), S(73, 136), S(79, 140), S(88, 143), S(88, 148), S(99, 166), S(102, 170), S(102, 175), S(106, 184), S(109, 191), S(113, 206), S(116, 212) },

        /* King */
        new uint[] { }
    };

    /// <summary>Bonus for pawns protecting the castled king.</summary>
    /// <remarks>Either directly in front of the king or one square further.<br />
    /// Indexed by [DistanceFromKing]</remarks>
    public static uint[] ShieldingPawnBonus = { S(+20, +10), S(+10, +5) };

    /// <summary>Penalty for an exposed king.</summary>
    /// <remarks>A king is more open to attacks if no friendly pawns are in front of it (half-open file), 
    /// and even worse if there are also no enemy pawns in front of it (open file).<br />
    /// Indexed by [OpenFile]</remarks>
    public static uint[] OpenFileNextToKingPenalty = { S(-20, -30), S(-40, -50) };

    public static uint ColorWeaknessPenaltyPerPawn = S(-3, -8);

    public static uint KnightOutpostBonus = S(+54, +34);
    public static uint BishopOutpostBonus = S(+31, +25);


    public const int OpeningPhaseScore = 15258;
    public const int EndgamePhaseScore = 3915;


    public static int Evaluate(out int gamePhase)
    {
        short whiteEarlygameMaterial = EarlygameMaterial(0, out int whitePawnMaterial, out int whitePawnCount, out int whiteKnightCount, out int whiteBishopCount);
        short blackEarlygameMaterial = EarlygameMaterial(1, out int blackPawnMaterial, out int blackPawnCount, out int blackKnightCount, out int blackBishopCount);
        
        short whiteLategameMaterial = LategameMaterial(0);
        short blackLategameMaterial = LategameMaterial(1);


        gamePhase = GetGamePhase(whiteEarlygameMaterial, blackEarlygameMaterial, whitePawnMaterial, blackPawnMaterial);


        //InterpolateAll(gamePhase);


        uint whiteEval = 0;
        uint blackEval = 0;

        // Add material to the evaluation.
        //int whiteMaterial = Interpolate(gamePhase, whiteEarlygameMaterial, whiteLategameMaterial);
        //int blackMaterial = Interpolate(gamePhase, blackEarlygameMaterial, blackLategameMaterial);

        whiteEval += S(whiteEarlygameMaterial, whiteLategameMaterial);
        blackEval += S(blackEarlygameMaterial, blackLategameMaterial);


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
        whiteEval += DoubledPawnPenalty * DoubledPawnsCount(Board.Pawns[0]);
        blackEval += DoubledPawnPenalty * DoubledPawnsCount(Board.Pawns[1]);

        // Give a penalty to isolated pawns.
        whiteEval += IsolatedPawnPenalty * IsolatedPawnsCount(Board.Pawns[0]);
        blackEval += IsolatedPawnPenalty * IsolatedPawnsCount(Board.Pawns[1]);

        // Give a penalty to backward pawns.
        whiteEval += BackwardPawnPenalty * BackwardPawnsCount(0, 1, Board.Pawns[0]);
        blackEval += BackwardPawnPenalty * BackwardPawnsCount(1, 0, Board.Pawns[1]);


        // Reduce the values of pawns in case of material imbalance.
        float materialImbalance = Math.Abs(whiteEarlygameMaterial - blackEarlygameMaterial) / 100;
        uint whiteMaterialImbalanceMultiplier = (uint)(materialImbalance * whitePawnCount);
        uint blackMaterialImbalanceMultiplier = (uint)(materialImbalance * blackPawnCount);
        whiteEval += MaterialImbalancePawnPenalty * whiteMaterialImbalanceMultiplier;
        blackEval += MaterialImbalancePawnPenalty * blackMaterialImbalanceMultiplier;


        // Give a bonus to bishop/knight pairs depending on whether the position is open or closed.
        if (IsPositionOpen())
        {
            if (whiteKnightCount == 2) whiteEval += KnightPairBonus[1];
            if (whiteBishopCount == 2) whiteEval += BishopPairBonus[1];
                                                                    
            if (blackKnightCount == 2) blackEval += KnightPairBonus[1];
            if (blackBishopCount == 2) blackEval += BishopPairBonus[1];
        }

        else
        {
            if (whiteKnightCount == 2) whiteEval += KnightPairBonus[0];
            if (whiteBishopCount == 2) whiteEval += BishopPairBonus[0];
                                                                    
            if (blackKnightCount == 2) blackEval += KnightPairBonus[0];
            if (blackBishopCount == 2) blackEval += BishopPairBonus[0];
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
        whiteEval += KnightOutpostBonus * KnightOutpostsCount(Board.PawnAttackedSquares[0], Board.PawnAttackedSquares[1], Board.Knights[0], Mask.BlackHalf);
        blackEval += KnightOutpostBonus * KnightOutpostsCount(Board.PawnAttackedSquares[1], Board.PawnAttackedSquares[0], Board.Knights[1], Mask.WhiteHalf);

        // Give a bonus to knights defended by a pawn in the opponent's half of the board.
        whiteEval += BishopOutpostBonus * BishopOutpostsCount(Board.PawnAttackedSquares[0], Board.PawnAttackedSquares[1], Board.Bishops[0], Mask.BlackHalf);
        blackEval += BishopOutpostBonus * BishopOutpostsCount(Board.PawnAttackedSquares[1], Board.PawnAttackedSquares[0], Board.Bishops[1], Mask.WhiteHalf);


        int eval = Interpolate(whiteEval - blackEval, gamePhase) * (Board.CurrentTurn == 0 ? 1 : -1);
        return eval;


        short EarlygameMaterial(int turnIndex, out int pawnMaterial, out int pawnCount, out int knightCount, out int bishopCount)
        {
            short material = 0;

            pawnCount = PieceCount(Board.Pawns[turnIndex]);
            material += (short)(pawnMaterial = pawnCount * StaticPieceValues[Pawn][0]);

            knightCount = PieceCount(Board.Knights[turnIndex]);
            material += (short)(knightCount * StaticPieceValues[Knight][0]);

            bishopCount = PieceCount(Board.Bishops[turnIndex]);
            material += (short)(bishopCount * StaticPieceValues[Bishop][0]);

            material += (short)(PieceCount(Board.Rooks[turnIndex]) * StaticPieceValues[Rook][0]);
            material += (short)(PieceCount(Board.Queens[turnIndex]) * StaticPieceValues[Queen][0]);
            return material;
        }

        short LategameMaterial(int turnIndex)
        {
            short material = 0;
            material += (short)(PieceCount(Board.Pawns[turnIndex]) * StaticPieceValues[Pawn][1]);
            material += (short)(PieceCount(Board.Knights[turnIndex]) * StaticPieceValues[Knight][1]);
            material += (short)(PieceCount(Board.Bishops[turnIndex]) * StaticPieceValues[Bishop][1]);
            material += (short)(PieceCount(Board.Rooks[turnIndex]) * StaticPieceValues[Rook][1]);
            material += (short)(PieceCount(Board.Queens[turnIndex]) * StaticPieceValues[Queen][1]);
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

                bonus += PassedPawnBonus[i] * count;
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
                count += (uint)Math.Max(0, PieceCount(pawns & Board.Files[i]) - 1);
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


        bool IsPositionOpen() => PieceCount(Board.AllOccupiedSquares) < 24;


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
                    score += MobilityBonus[pieceType][PieceCount(
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
                    ShieldingPawnBonus[0] * (uint)PieceCount(friendlyPawns & Board.FirstShieldingPawns[Board.KingPosition[colorIndex]]) +
                    ShieldingPawnBonus[1] * (uint)PieceCount(friendlyPawns & Board.SecondShieldingPawns[Board.KingPosition[colorIndex]]);
            }

            int kingFile = Board.GetFile(Board.KingPosition[colorIndex]);
            ulong[] kingFiles = Board.KingFiles[kingFile];

            foreach (var file in kingFiles)
            {
                int friendlyPawnsCount = PieceCount(friendlyPawns & file);
                int opponentPawnsCount = PieceCount(opponentPawns & file);

                if (friendlyPawnsCount == 0)
                {
                    if (opponentPawnsCount != 0) score += OpenFileNextToKingPenalty[0];
                    else score += OpenFileNextToKingPenalty[1];
                }
            }

            return score;
        }

        uint ColorWeaknessScore(ulong pawns, ulong bishops)
        {
            uint score = 0;

            int lightPawnsCount = PieceCount(pawns & Mask.LightSquares);
            int darkPawnsCount = PieceCount(pawns & Mask.DarkSquares);

            int lightBishopsCount = PieceCount(bishops & Mask.LightSquares);
            int darkBishopsCount = PieceCount(bishops & Mask.DarkSquares);

            // Give a penalty for the lack of a bishop on a light square, for each extra pawn on a dark square.
            score += ColorWeaknessPenaltyPerPawn * (uint)(lightBishopsCount == 0 ? 1 : 0) * (uint)Math.Max(0, darkPawnsCount - lightPawnsCount);

            // Give a penalty for the lack of a bishop on a light square, for each extra pawn on a dark square.
            score += ColorWeaknessPenaltyPerPawn * (uint)(darkBishopsCount == 0 ? 1 : 0) * (uint)Math.Max(0, lightPawnsCount - darkPawnsCount);

            return score;
        }

        uint KnightOutpostsCount(ulong pawnDefendedSquares, ulong opponentPawnAttackedSquares, ulong knights, ulong opponentHalf)
        {
            // If a knight is in the opponent's half of the board (or in the center),
            // is defended by a pawn and is not under attack by an opponent pawn,
            // it is considered an "outpost".
            return (uint)PieceCount(pawnDefendedSquares & ~opponentPawnAttackedSquares & knights & opponentHalf);
        }

        uint BishopOutpostsCount(ulong pawnDefendedSquares, ulong opponentPawnAttackedSquares, ulong bishops, ulong opponentHalf)
        {
            // If a bishop is in the opponent's half of the board (or in the center),
            // is defended by a pawn and is not under attack by an opponent pawn,
            // it is considered an "outpost".
            return (uint)PieceCount(pawnDefendedSquares & ~opponentPawnAttackedSquares & bishops & opponentHalf);
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

    public static bool PawnsOnly()
    {
        int materialCount = 0;
        materialCount += PieceCount(Board.Knights[Board.CurrentTurn]);
        materialCount += PieceCount(Board.Bishops[Board.CurrentTurn]);
        materialCount += PieceCount(Board.Rooks[Board.CurrentTurn]);
        materialCount += PieceCount(Board.Queens[Board.CurrentTurn]);
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

            mopUpScore += Math.Max(3 - opponentKingFile, opponentKingFile - 4) + Math.Max(3 - opponentKingRank, opponentKingRank - 4);

            mopUpScore += 14 - (Math.Abs(friendlyKingFile - opponentKingFile) + Math.Abs(friendlyKingRank - opponentKingRank));

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
            new uint[] { S(-175, -96), S( -92, -65), S( -74, -49), S( -73, -21) },
            new uint[] { S( -77, -67), S( -41, -54), S( -27, -18), S( -15,   8) },
            new uint[] { S( -61, -40), S( -17, -27), S(   6,  -8), S(  12,  29) },
            new uint[] { S( -35, -35), S(   8,  -2), S(  40,  13), S(  49,  28) },
            new uint[] { S( -34, -45), S(  13, -16), S(  44,   9), S(  51,  39) },
            new uint[] { S(  -9, -51), S(  22, -44), S(  58, -16), S(  53,  17) },
            new uint[] { S( -67, -69), S( -27, -50), S(   4, -51), S(  37,  12) },
            new uint[] { S(-201,-100), S( -83, -88), S( -56, -56), S( -26, -17) }
        },                                                                                       
                                                                                                 
        new uint[][] { /* Bishop */                                                              
            new uint[] { S( -37, -40), S(  -4, -21), S(  -6, -26), S( -16,  -8) },
            new uint[] { S( -11, -26), S(   6,  -9), S(  13, -12), S(   3,   1) },
            new uint[] { S( -5 , -11), S(  15,  -1), S(  -4,  -1), S(  12,   7) },
            new uint[] { S( -4 , -14), S(   8,  -4), S(  18,   0), S(  27,  12) },
            new uint[] { S( -8 , -12), S(  20,  -1), S(  15, -10), S(  22,  11) },
            new uint[] { S( -11, -21), S(   4,   4), S(   1,   3), S(   8,   4) },
            new uint[] { S( -12, -22), S( -10, -14), S(   4,  -1), S(   0,   1) },
            new uint[] { S( -34, -32), S(   1, -29), S( -10, -26), S( -16, -17) }
        },                                                                                       
                                                                                                 
        new uint[][] { /* Rook */                                                                
            new uint[] { S( -31,  -9), S( -20, -13), S( -14, -10), S(  -5,  -9) },
            new uint[] { S( -21, -12), S( -13,  -9), S(  -8,  -1), S(   6,  -2) },
            new uint[] { S( -25,   6), S( -11,  -8), S(  -1,  -2), S(   3,  -6) },
            new uint[] { S( -13,  -6), S(  -5,   1), S(  -4,  -9), S(  -6,   7) },
            new uint[] { S( -27,  -5), S( -15,   8), S(  -4,   7), S(   3,  -6) },
            new uint[] { S( -22,   6), S(  -2,   1), S(   6,  -7), S(  12,  10) },
            new uint[] { S(  -2,   4), S(  12,   5), S(  16,  20), S(  18,  -5) },
            new uint[] { S( -17,  18), S( -19,   0), S(  -1,  19), S(   9,  13) }
        },                                                                                       
                                                                                                 
        new uint[][] { /* Queen */                                                               
            new uint[] { S(   3, -69), S(  -5, -57), S(  -5, -47), S(   4, -26) },
            new uint[] { S(  -3, -54), S(   5, -31), S(   8, -22), S(  12,  -4) },
            new uint[] { S(  -3, -39), S(   6, -18), S(  13,  -9), S(   7,   3) },
            new uint[] { S(   4, -23), S(   5,  -3), S(   9,  13), S(   8,  24) },
            new uint[] { S(   0, -29), S(  14,  -6), S(  12,   9), S(   5,  21) },
            new uint[] { S(  -4, -38), S(  10, -18), S(   6, -11), S(   8,   1) },
            new uint[] { S(  -5, -50), S(   6, -27), S(  10, -24), S(   8,  -8) },
            new uint[] { S(  -2, -74), S(  -2, -52), S(   1, -43), S(  -2, -34) }
        },                                                                                       
                                                                                                 
        new uint[][] { /* King */                                                                
            new uint[] { S( 271,   1), S( 327,  45), S( 271,  85), S( 198,  76) },
            new uint[] { S( 278,  53), S( 303, 100), S( 234, 133), S( 179, 135) },
            new uint[] { S( 195,  88), S( 258, 130), S( 169, 169), S( 120, 175) },
            new uint[] { S( 164, 103), S( 190, 156), S( 138, 172), S(  98, 172) },
            new uint[] { S( 154,  96), S( 179, 166), S( 105, 199), S(  70, 199) },
            new uint[] { S( 123,  92), S( 145, 172), S(  81, 184), S(  31, 191) },
            new uint[] { S(  88,  47), S( 120, 121), S(  65, 116), S(  33, 131) },
            new uint[] { S(  59,  11), S(  89,  59), S(  45,  73), S(  -1,  78) }
        }
    };

    // Values from Stockfish.
    // Accessed by s_pawnPieceSquareTables[rankIndex][fileIndex].
    private static readonly uint[][] s_pawnPieceSquareTables =
    {
        new uint[] { },
        new uint[] { S(  2, -8), S(  4, -6), S( 11,  9), S( 18,  5), S( 16, 16), S( 21,  6), S(  9, -6), S( -3,-18) },
        new uint[] { S( -9, -9), S(-15, -7), S( 11,-10), S( 15,  5), S( 31,  2), S( 23,  3), S(  6, -8), S(-20, -5) },
        new uint[] { S( -3,  7), S(-20,  1), S(  8, -8), S( 19, -2), S( 39,-14), S( 17,-13), S(  2,-11), S( -5, -6) },
        new uint[] { S( 11, 12), S( -4,  6), S(-11,  2), S(  2, -6), S( 11, -5), S(  0, -4), S(-12, 14), S(  5,  9) },
        new uint[] { S(  3, 27), S(-11, 18), S( -6, 19), S( 22, 29), S( -8, 30), S( -5,  9), S(-14,  8), S(-11, 14) },
        new uint[] { S( -7, -1), S(  6,-14), S( -2, 13), S(-11, 22), S(  4, 24), S(-14, 17), S( 10,  7), S( -9,  7) }
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

public struct Score
{
    // A score is stored in a single unsigned integer,
    // where the first 16 bits store the endgame score
    // and the last 16 bits store the opening score.
    public static uint S(short opening, short endgame) => (((uint)endgame) << 16) + (uint)opening;

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
