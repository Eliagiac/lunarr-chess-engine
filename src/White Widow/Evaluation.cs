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
        new short[] { 2538, 2682 }, /* Queen */
        new short[] {    0,    0 }, /* King */
    };

    /// <summary>The default value of each piece type.</summary>
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

    /// <summary>Penalty for two or more pawns (of the same color) on one file, given to each of the pawns.</summary>
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

    /// <summary>Penalty for each pawn of the same color of a bishop.</summary>
    public static uint ColorWeaknessPenalty = S(-3, -8);

    public static uint KnightOutpostBonus = S(+54, +34);
    public static uint BishopOutpostBonus = S(+31, +25);

    /// <summary>Bonus for a bishop or knight directly behind a pawn.</summary>
    public static uint MinorPieceBehindPawnBonus = S(+18, +3);


    public const int OpeningPhaseScore = 15258;
    public const int EndgamePhaseScore = 3915;


    /// <summary>The bitboard of squares considered when computing the mobility of a piece.</summary>
    /// <remarks>Squares outside this area are not relevant when considering mobility.</remarks>
    private static ulong[] MobilityArea = new ulong[2];

    /// <summary>The bitboard of squares considered when checking if a knight or bishop is an outpost.</summary>
    private static ulong[] OutpostSquares = new ulong[2];


    public static int Evaluate(out int gamePhase)
    {
        // Before the evaluation can start, some constants need to be computed.
        uint evaluation = 0;

        // The game phase will be used to interpolate between opening and endgame scores.
        gamePhase = CurrentGamePhase();

        // Squares outside the mobility area are irrelevant to mobility calculations.
        MobilityArea[0] = FindMobilityArea(0);
        MobilityArea[1] = FindMobilityArea(1);

        // Knights and bishops on outpost squares are given a bonus.
        OutpostSquares[0] = FindOutpostSquares(0, 1);
        OutpostSquares[1] = FindOutpostSquares(1, 0);


        // The first step to evaluating the position is getting the material and piece square tables score.
        // These values are updated any time a move is made.
        evaluation += Board.MaterialScore[0] - Board.MaterialScore[1];
        evaluation += Board.PsqtScore[0] - Board.PsqtScore[1];

        // Add bonuses to each piece based on various positional considerations.
        evaluation +=
            EvaluatePieces(Knight, 0, 1) - EvaluatePieces(Knight, 1, 0) +
            EvaluatePieces(Bishop, 0, 1) - EvaluatePieces(Bishop, 1, 0) +
            EvaluatePieces(Rook, 0, 1) - EvaluatePieces(Rook, 1, 0) +
            EvaluatePieces(Queen, 0, 1) - EvaluatePieces(Queen, 1, 0);

        // Give a score to the pawn structure of the position.
        evaluation += EvaluatePawnStructure(0, 1) - EvaluatePawnStructure(1, 0);

        // Add bonuses for a well defended king.
        evaluation += KingSafetyScore(0, 1) - KingSafetyScore(1, 0);

        // Give a bonus to bishop and knight pairs depending on whether the position is open or closed.
        bool isPositionOpen = PieceCount(Board.AllOccupiedSquares) < 24;
        evaluation += PiecePairBonus(0, isPositionOpen) - PiecePairBonus(1, isPositionOpen);


        return Interpolate(evaluation, gamePhase) * (Board.CurrentTurn == 0 ? 1 : -1);


        int CurrentGamePhase()
        {
            // The opening material value is used to compute the game phase.
            int whiteMaterial = OpeningValue(Board.MaterialScore[0]);
            int blackMaterial = OpeningValue(Board.MaterialScore[1]);

            int whitePawnMaterial = PieceCount(Board.Pawns[0]) * StaticPieceValues[Pawn][0];
            int blackPawnMaterial = PieceCount(Board.Pawns[1]) * StaticPieceValues[Pawn][0];

            return (whiteMaterial + blackMaterial) - (whitePawnMaterial + blackPawnMaterial);
        }

        ulong FindMobilityArea(int colorIndex)
        {
            ulong blockedSquares = colorIndex == 0 ?
                Board.AllOccupiedSquares >> 8 :
                Board.AllOccupiedSquares << 8;

            // Exclude pawns that are blocked or on the first two ranks.
            ulong excludedPawns = Board.Pawns[colorIndex] & (blockedSquares | Board.LowRanks[colorIndex]);

            // Exclude our king and queens.
            ulong excludedPieces = Board.Kings[colorIndex] | Board.Queens[colorIndex];

            // Exclude squares controlled by enemy panwns.
            ulong excludedControlledSquares = Board.PawnAttackedSquares[colorIndex ^ 1];

            // TODO: Exclude pieces that are blocking an attack to our king.

            return ~(excludedPawns | excludedPieces | excludedControlledSquares);
        }

        ulong FindOutpostSquares(int colorIndex, int opponentColorIndex)
        {
            // An outpost must be in enemy territory.
            ulong outpostRanks = colorIndex == 0 ? Mask.WhiteOutpostRanks : Mask.BlackOutpostRanks;

            // An outpost must be protected by a pawn.
            ulong pawnProtectedSquares = Board.PawnAttackedSquares[colorIndex];

            // An outpost must not be attacked by an enemy pawn.
            // Note: in Stockfish, instead of checking for enemy pawn attacks, the whole enemy pawn attack span is considered
            // (all of the squares that could be attacked by an enemy pawn if it were to move up, until the edge of the board).
            ulong enemyPawnAttackedSquares = Board.PawnAttackedSquares[opponentColorIndex];

            return outpostRanks & pawnProtectedSquares & ~enemyPawnAttackedSquares;
        }


        uint KingSafetyScore(int color, int opponentColor)
        {
            uint score = 0;

            ulong friendlyPawns = Board.Pawns[color];
            ulong opponentPawns = Board.Pawns[opponentColor];

            // A castled king gets a bonus if it has pawns in front of it.
            if ((Board.Kings[color] & (color == 0 ? Mask.WhiteCastledKingPosition : Mask.BlackCastledKingPosition)) != 0)
            {
                score +=
                    ShieldingPawnBonus[0] * (uint)PieceCount(friendlyPawns & Board.FirstShieldingPawns[Board.KingPosition[color]]) +
                    ShieldingPawnBonus[1] * (uint)PieceCount(friendlyPawns & Board.SecondShieldingPawns[Board.KingPosition[color]]);
            }

            // If the file the king is on (or adjacent files) is open, the king is more exposed to attacks.
            foreach (var file in Board.KingFiles[Board.GetFile(Board.KingPosition[color])])
            {
                if (PieceCount(friendlyPawns & file) == 0)
                {
                    if (PieceCount(opponentPawns & file) != 0) score += OpenFileNextToKingPenalty[0];
                    else score += OpenFileNextToKingPenalty[1];
                }
            }

            return score;
        }

        uint PiecePairBonus(int color, bool isPositionOpen)
        {
            uint bonus = 0;
            if (PieceCount(Board.Knights[color]) >= 2) bonus += KnightPairBonus[isPositionOpen ? 1 : 0];
            if (PieceCount(Board.Bishops[color]) >= 2) bonus += BishopPairBonus[isPositionOpen ? 1 : 0];
            return bonus;
        }
    }

    /// <summary>Compute the added score of all pieces of the specified type and color.</summary>
    private static uint EvaluatePieces(int pieceType, int color, int opponentColor)
    {
        // Pawn and king evaluation is done separately.
        if (pieceType == Pawn || pieceType == King) return 0;

        uint score = 0;
        
        ulong pieces = Board.Pieces[pieceType][color];
        while (pieces != 0)
        {
            // Isolate the first piece.
            int squareIndex = FirstSquareIndex(pieces);
            pieces &= pieces - 1;

            ulong square = 1UL << squareIndex;


            // All of the squares attacked by this piece (including friendly pieces).
            // Note: Stockfish adds x-ray attacks of bishops and rooks, as well as the full line from the piece to the king if the piece is blocking an attack (should investigate since a pinned piece is always already attacking the king).
            ulong attackedSquares = Board.AttacksFrom(squareIndex, pieceType, Board.AllOccupiedSquares);

            // Add a bonus based on how many squares are attacked by this piece inside the mobility area.
            score += MobilityBonus[pieceType][PieceCount(attackedSquares & MobilityArea[color])];


            if (pieceType == Knight || pieceType == Bishop)
            {
                // If a knight or bishop is in the opponent's territory, is defended
                // by a pawn and is not under attack by an opponent pawn, it is considered an "outpost".
                if ((square & OutpostSquares[color]) != 0)
                    score += pieceType == Knight ?
                        KnightOutpostBonus : BishopOutpostBonus;


                ulong squaresBehindPawns = color == 0 ?
                    Board.Pawns[color] >> 8 : Board.Pawns[color] << 8;

                // A minor piece directly behind a pawn should be given a bonus.
                if ((square & squaresBehindPawns) != 0)
                    score += MinorPieceBehindPawnBonus;
            }

            if (pieceType == Bishop)
            {
                ulong sameColorSquares = (square & Mask.LightSquares) != 0 ? Mask.LightSquares : Mask.DarkSquares;

                // Penalty for each pawn on a square of the same color as this bishop.
                score += (uint)(ColorWeaknessPenalty * PieceCount(Board.Pawns[color] & sameColorSquares));
            }
        }

        return score;
    }

    /// <summary>Evaluate the given player's pawn structure.</summary>
    private static uint EvaluatePawnStructure(int color, int opponentColor)
    {
        uint score = 0;

        ulong pieces = Board.Pieces[Pawn][color];
        while (pieces != 0)
        {
            // Isolate the first pawn.
            int squareIndex = FirstSquareIndex(pieces);
            pieces &= pieces - 1;

            // Keep track of the pawn structure (instead of using 'pieces', where each analysed piece is removed).
            ulong pawns = Board.Pieces[Pawn][color];
            ulong opponentPawns = Board.Pieces[Pawn][opponentColor];

            int file = Board.GetFile(squareIndex);


            // If there are no enemy pawns on this or adjacent files,
            // and the pawn doesn't have another friendly pawn in front,
            // it is considered a "passed pawn".
            if ((Board.Spans[color, squareIndex] & opponentPawns) == 0 &&
                (Board.Fills[color, squareIndex] & pawns) == 0)
                score += PassedPawnBonus[file];

            // If this pawn isn't the only one in the file, it is considered doubled and deserves a penalty.
            // Each of the pawns on file will receive the penalty.
            if (PieceCount(Board.Files[file] & pawns) >= 2)
                score += DoubledPawnPenalty;

            // If there are no friendly pawns in the pawn's neighbouring
            // files, it is considered an "isolated pawn".
            if ((Board.NeighbouringFiles[file] & pawns) == 0)
                score += IsolatedPawnPenalty;

            // If there are no friendly pawns protecting the pawn
            // and its stop square is attacked by an enemy pawn,
            // it is considered a "backward pawn".
            if ((pawns & Board.BackwardProtectors[color, squareIndex]) == 0 &&
                (Board.StopSquare[color, squareIndex] & Board.PawnAttackedSquares[opponentColor]) != 0)
                score += BackwardPawnPenalty;
        }

        return score;
    }


    /// <summary>Compute the material of the specified player in the current position.</summary>
    /// <remarks>The material score should be updated on every move. 
    /// This function should only be called on board initialization.</remarks>
    public static uint ComputeMaterial(int colorIndex)
    {
        uint material = 0;
        material += (uint)PieceCount(Board.Pawns[colorIndex]) * PieceValues[Pawn];
        material += (uint)PieceCount(Board.Knights[colorIndex]) * PieceValues[Knight];
        material += (uint)PieceCount(Board.Bishops[colorIndex]) * PieceValues[Bishop];
        material += (uint)PieceCount(Board.Rooks[colorIndex]) * PieceValues[Rook];
        material += (uint)PieceCount(Board.Queens[colorIndex]) * PieceValues[Queen];
        return material;
    }
}


public class PieceSquareTables
{
    /// <summary>Evaluate the total score of all pieces of the given color.</summary>
    /// <remarks>Should only be used on board initialization.</remarks>
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

    /// <summary>Evaluate the total score of all given pieces.</summary>
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

    /// <summary>Read the score of the given piece.</summary>
    public static uint ReadScore(int pieceType, int square, bool isWhite)
    {
        int rank = Board.GetRank(square);
        int file = Board.GetFile(square);

        if (!isWhite) rank = 7 - rank;

        if (pieceType != Pawn) return s_pieceSquareTables[pieceType][rank][FileIndex()];
        return s_pawnPieceSquareTables[rank][file];


        int FileIndex() => file >= 4 ? 7 - file : file;
    }


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


    /// <summary>Indexed by [PieceType][RankIndex][FileIndex (up to 3, then mirrored)].</summary>
    /// <remarks>Values from Stockfish: <see href="https://github.com/official-stockfish/Stockfish/blob/master/src/psqt.cpp#L29-L102"/>.</remarks>
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

    /// <summary>Indexed by [RankIndex][FileIndex].</summary>
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
}

/// <summary>
/// A score is stored in a single unsigned integer,
/// where the first 16 bits store the endgame value
/// and the last 16 bits store the opening value.
/// </summary>
public struct Score
{
    public static uint S(short opening, short endgame) => (((uint)endgame) << 16) + (uint)opening;

    /// <summary>The last 16 bits of the score.</summary>
    public static int OpeningValue(uint score) => (short)(score & 0x0000ffff);

    /// <summary>The first 16 bits of the score.</summary>
    public static int EndgameValue(uint score) => (short)((score & 0xffff0000) >> 16);

    /// <summary>
    /// Interpolate between the opening value and the 
    /// endgame value based on the current gamephase.
    /// </summary>
    public static int Interpolate(uint score, int gamePhase)
    {
        short opening = (short)(score & 0x0000ffff);
        short endgame = (short)((score & 0xffff0000) >> 16);

        if (gamePhase > OpeningPhaseScore) return opening;
        else if (gamePhase < EndgamePhaseScore) return endgame;

        else return 
                (opening * gamePhase +
                endgame * (OpeningPhaseScore - gamePhase)) / 
                OpeningPhaseScore;
    }
}
