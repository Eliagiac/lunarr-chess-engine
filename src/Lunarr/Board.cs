using Utilities;
using static Utilities.Bitboard;
using static Utilities.Zobrist;
using static Piece;
using static Move;

public class Board
{
    // List representation of the board.
    public int[] Squares { get; set; }

    // Bitboard representations of the board.
    public Dictionary<int, ulong[]> Pieces { get; set; }


    public ulong[] Pawns => Pieces[Pawn];
    public ulong[] Knights => Pieces[Knight];
    public ulong[] Bishops => Pieces[Bishop];
    public ulong[] Rooks => Pieces[Rook];
    public ulong[] Queens => Pieces[Queen];
    public ulong[] Kings => Pieces[King];


    // 0 = white to move.
    // 1 = black to move.
    public int Friendly { get; set; }
    public int Opponent { get; set; }


    // Board information storage for quick lookup.
    public ulong[] OccupiedSquares = new ulong[2];
    public ulong AllOccupiedSquares;

    public ulong[] SlidingPieces = new ulong[2];
    public ulong AllSlidingPieces;

    public int[] KingPosition = new int[2];

    public ulong[] CheckingPieces = new ulong[2];

    /// <summary>
    /// Updated when <see cref="GenerateAllLegalMoves"/> is called.
    /// </summary>
    private bool[] IsKingInCheck = new bool[2];
    private bool IsCheckDataOutdated = true;

    public ulong[] Pins = new ulong[2];


    // All the squares currently attacked by each piece.
    // Updated every time a move is made/unmade.
    //public static ulong[,] Attacks = new ulong[2, 64];

    // All the squares currently attacked by a color.
    //public static ulong[] AttackedSquares = new ulong[2];

    /// <summary>Bitboards of the squares currently attacked by pawns of the given color.</summary>
    public ulong[] PawnAttackedSquares = new ulong[2];


    // En passant is the only move that allows the capture of
    // a piece on a different square from the move's target,
    // so it needs some extra variables.

    // Square where en passant can be performed (3rd or 6th rank).
    public ulong EnPassantSquare;
    // Square with the en passant target (4th or 5th rank).
    public ulong EnPassantTarget;

    public ulong EnPassantSquareBackup;
    public ulong EnPassantTargetBackup;


    // Castling is a move that allows the king to move 2 squares
    // towards one of the initial rooks.
    // To be able to castle, the following rules apply:
    // 1. The king must have never moved from it's starting square.
    // 2. The rook must have never moved from it's starting square.
    // 3. The rook must not have been captured.
    // 4. The king must not currently be in check.
    // 5. The king must not travel through, or land on a square that is attacked by an enemy piece.

    // The positive bits represent the squares the king can castle to.
    public ulong CastlingRights;


    // 64-bit key that is unique to (almost) every position.
    // Multiple positions may share a key because of the limited size
    // compared to the amount of possible chess positions, but the tradeoff
    // is necessary since Zobrist Hashing is (to my knowledge) the fastest and most efficient
    // method of retrieving a unique key used to look up the position in a transposition table.
    public ulong ZobristKey;

    public static int CapturedPieceType;


    public static ulong[] Files =
    {
        0x101010101010101,
        0x202020202020202,
        0x404040404040404,
        0x808080808080808,
        0x1010101010101010,
        0x2020202020202020,
        0x4040404040404040,
        0x8080808080808080
    };
    public static ulong[] NeighbouringFiles =
    {
        Files[1],
        Files[0] | Files[2],
        Files[1] | Files[3],
        Files[2] | Files[4],
        Files[3] | Files[5],
        Files[4] | Files[6],
        Files[5] | Files[7],
        Files[6]
    };
    public static ulong[][] KingFiles =
    {
        new ulong[] { Files[0], Files[1],        0 },
        new ulong[] { Files[0], Files[1], Files[2] },
        new ulong[] { Files[1], Files[2], Files[3] },
        new ulong[] { Files[2], Files[3], Files[4] },
        new ulong[] { Files[3], Files[4], Files[5] },
        new ulong[] { Files[4], Files[5], Files[6] },
        new ulong[] { Files[5], Files[6], Files[7] },
        new ulong[] {        0, Files[6], Files[7] }
    };

    public static ulong[][] Ranks =
    {
        new ulong[]
        { 0xff,
        0xff00,
        0xff0000,
        0xff000000,
        0xff00000000,
        0xff0000000000,
        0xff000000000000,
        0xff00000000000000 },

        new ulong[]
        { 0xff00000000000000,
        0xff000000000000,
        0xff0000000000,
        0xff00000000,
        0xff000000,
        0xff0000,
        0xff00,
        0xff },
    };

    /// <summary>Bitboards of the second and third rank for each color.</summary>
    public static ulong[] LowRanks =
    {
        Ranks[0][1] | Ranks[0][2],
        Ranks[1][1] | Ranks[1][2],
    };

    public static ulong[,] Spans;
    public static ulong[,] Fills;

    public static ulong[,] BackwardProtectors;
    public static ulong[,] StopSquare;


    public static ulong[] FirstShieldingPawns;
    public static ulong[] SecondShieldingPawns;


    /// <summary>
    /// Contains the history of positions reached up to this point, including their zobrist key,
    /// castling rights and possible en passant target. 
    /// </summary>
    /// <remarks>
    /// Updated when making or unmaking a move or null move; used to unmake moves and null moves.
    /// </remarks>
    public Stack<GameState> GameStateHistory;

    /// <summary>
    /// Contains the history of positions (zobrist keys) reached up to this point, used for detecting draws by repetition.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="GameStateHistory"/>, it's not updated when making or unmaking a null move.
    /// </remarks>
    public Stack<ulong> PositionKeyHistory;


    public uint[] PsqtScore;

    public uint[] MaterialScore;

    public int PlyCount;
    public int FiftyMovePlyCount;


    public void Init()
    {
        Squares = new int[64];
        Pieces = new()
        {
            [King] = new ulong[2],
            [Pawn] = new ulong[2],
            [Knight] = new ulong[2],
            [Bishop] = new ulong[2],
            [Rook] = new ulong[2],
            [Queen] = new ulong[2],
        };
        GameStateHistory = new();
        PositionKeyHistory = new();
        PsqtScore = new uint[2];
        MaterialScore = new uint[2];
        PlyCount = 0;
        FiftyMovePlyCount = 0;


        Fills = new ulong[2, 64];
        for (int i = 0; i < 64; i++)
        {
            int rankModifier = 8;
            while (i + rankModifier < 64)
            {
                Fills[0, i] |= 1UL << (i + rankModifier);
                rankModifier += 8;
            }
            rankModifier = 8;
            while (i - rankModifier >= 0)
            {
                Fills[1, i] |= 1UL << (i - rankModifier);
                rankModifier += 8;
            }
        }

        Spans = new ulong[2, 64];
        for (int i = 0; i < 64; i++)
        {
            int rankModifier = 8;
            while (i + rankModifier < 64)
            {
                Spans[0, i] |= 1UL << (i + rankModifier);
                if (GetFile(i + rankModifier - 1) == GetFile(i) - 1) Spans[0, i] |= 1UL << (i + rankModifier - 1);
                if (GetFile(i + rankModifier + 1) == GetFile(i) + 1) Spans[0, i] |= 1UL << (i + rankModifier + 1);
                rankModifier += 8;
            }
            rankModifier = 8;
            while (i - rankModifier >= 0)
            {
                Spans[1, i] |= 1UL << (i - rankModifier);
                if (GetFile(i - rankModifier - 1) == GetFile(i) - 1) Spans[1, i] |= 1UL << (i - rankModifier - 1);
                if (GetFile(i - rankModifier + 1) == GetFile(i) + 1) Spans[1, i] |= 1UL << (i - rankModifier + 1);
                rankModifier += 8;
            }
        }

        BackwardProtectors = new ulong[2, 64];
        for (int i = 0; i < 64; i++)
        {
            if (GetFile(i - 9) == GetFile(i) - 1) BackwardProtectors[0, i] |= 1UL << (i - 9);
            if (GetFile(i - 7) == GetFile(i) + 1) BackwardProtectors[0, i] |= 1UL << (i - 7);

            if (GetFile(i + 7) == GetFile(i) - 1) BackwardProtectors[1, i] |= 1UL << (i + 7);
            if (GetFile(i + 9) == GetFile(i) + 1) BackwardProtectors[1, i] |= 1UL << (i + 9);
        }

        StopSquare = new ulong[2, 64];
        for (int i = 0; i < 64; i++)
        {
            if (i + 8 < 64) StopSquare[0, i] |= 1UL << (i + 8);
            if (i - 8 >= 0) StopSquare[1, i] |= 1UL << (i - 8);

            if (GetRank(i) == 1) if (i + 16 < 64)
                    StopSquare[0, i] |= 1UL << (i + 16);
            if (GetRank(i) == 6) if (i - 16 >= 0)
                    StopSquare[1, i] |= 1UL << (i - 16);
        }


        FirstShieldingPawns = new ulong[64];

        FirstShieldingPawns[0] = 0x300;
        FirstShieldingPawns[1] = 0x700;
        FirstShieldingPawns[2] = 0xe00;
        FirstShieldingPawns[5] = 0x7000;
        FirstShieldingPawns[6] = 0xe000;
        FirstShieldingPawns[7] = 0xc000;

        FirstShieldingPawns[56] = 0x3000000000000;
        FirstShieldingPawns[57] = 0x7000000000000;
        FirstShieldingPawns[58] = 0xe000000000000;
        FirstShieldingPawns[61] = 0x70000000000000;
        FirstShieldingPawns[62] = 0xe0000000000000;
        FirstShieldingPawns[63] = 0xc0000000000000;


        SecondShieldingPawns = new ulong[64];

        SecondShieldingPawns[0] = 0x30000;
        SecondShieldingPawns[1] = 0x70000;
        SecondShieldingPawns[2] = 0xe0000;
        SecondShieldingPawns[5] = 0x700000;
        SecondShieldingPawns[6] = 0xe00000;
        SecondShieldingPawns[7] = 0xc00000;

        SecondShieldingPawns[56] = 0x30000000000;
        SecondShieldingPawns[57] = 0x70000000000;
        SecondShieldingPawns[58] = 0xe0000000000;
        SecondShieldingPawns[61] = 0x700000000000;
        SecondShieldingPawns[62] = 0xe00000000000;
        SecondShieldingPawns[63] = 0xc00000000000;
    }

    public void UpdateAllOccupiedSquares()
    {
        UpdateOccupiedSquares(0);
        UpdateOccupiedSquares(1);

        AllOccupiedSquares = OccupiedSquares[0] | OccupiedSquares[1];
        AllSlidingPieces = SlidingPieces[0] | SlidingPieces[1];

        void UpdateOccupiedSquares(int colorIndex)
        {
            SlidingPieces[colorIndex] = Bishops[colorIndex] | Rooks[colorIndex] | Queens[colorIndex];
            OccupiedSquares[colorIndex] = Kings[colorIndex] | Pawns[colorIndex] | Knights[colorIndex] | SlidingPieces[colorIndex];
        }
    }

    public GameState CurrentGameState() => 
        new(ZobristKey, CapturedPieceType, CastlingRights, EnPassantSquare, EnPassantTarget, FiftyMovePlyCount);

    public void MakeMove(Move move, out int pieceType, out int capturedPieceType)
    {
        PlyCount++;
        FiftyMovePlyCount++;

        int moveFlag = move.Flag;
        bool isEnPassant = moveFlag == EnPassantCaptureFlag;

        int startSquareIndex = move.StartSquareIndex;
        ulong startSquare = move.StartSquare;

        int targetSquareIndex = move.TargetSquareIndex;
        ulong targetSquare = move.TargetSquare;

        pieceType = PieceType(startSquareIndex);
        int pieceTypeOrPromotion = move.PromotionPieceType == None ? pieceType : move.PromotionPieceType;

        capturedPieceType = isEnPassant ? Pawn : PieceType(targetSquareIndex);
        CapturedPieceType = capturedPieceType;

        // Empty the move's start square.
        RemovePiece(startSquare, startSquareIndex, pieceType, Friendly);

        // Remove any captured piece. En passant is handled later, so if the target square is empty skip this step.
        if (capturedPieceType != None && (AllOccupiedSquares & move.TargetSquare) != 0)
        {
            FiftyMovePlyCount = 0;
            RemovePiece(targetSquare, targetSquareIndex, capturedPieceType, Opponent);
        }

        // Place moved piece on the target square (unless promoting).
        AddPiece(targetSquare, targetSquareIndex, pieceTypeOrPromotion, Friendly);


        // Remove old en passant square.
        ZobristKey ^= EnPassantFileKeys[EnPassantSquare != 0 ? GetFile(FirstSquareIndex(EnPassantSquare)) + 1 : 0];

        // Special pawn moves.
        if (pieceType == Pawn) MakePawnMove();

        // Remove previous en passant target.
        else
        {
            EnPassantTarget = 0;
            EnPassantSquare = 0;
        }

        // Update en passant square.
        ZobristKey ^= EnPassantFileKeys[EnPassantSquare != 0 ? GetFile(FirstSquareIndex(EnPassantSquare)) + 1 : 0];


        // Save the current castling rights
        // as 4 bits for Zobrist hashing.
        uint oldCastlingRights = FourBitCastlingRights();

        // Remove castling rights if a rook is captured.
        if (capturedPieceType == Rook) RemoveRookCastlingRights(targetSquare, Opponent == 0 ? Mask.WhiteRank : Mask.BlackRank);

        // Remove castling rights if the king moves.
        if (pieceType == King) CastlingRights &= ~(Friendly == 0 ? Mask.WhiteRank : Mask.BlackRank);

        // Remove castling rights if the rook moves.
        if (pieceType == Rook) RemoveRookCastlingRights(startSquare, Friendly == 0 ? Mask.WhiteRank : Mask.BlackRank);


        // If the king is castling, the rook should follow it.
        if (pieceType == King)
        {
            // Queenside castling
            if (startSquare >> 2 == targetSquare)
            {
                // Add rook on the target square.
                ulong castledRookTarget = targetSquare << 1;
                AddPiece(castledRookTarget, FirstSquareIndex(castledRookTarget), Rook, Friendly);

                // Remove it from its start square.
                ulong castledRookSquare = startSquare >> 4;
                RemovePiece(castledRookSquare, FirstSquareIndex(castledRookSquare), Rook, Friendly);
            }

            // Kingside castling
            else if (startSquare << 2 == targetSquare)
            {
                // Add rook on the target square.
                ulong castledRookTarget = targetSquare >> 1;
                AddPiece(castledRookTarget, FirstSquareIndex(castledRookTarget), Rook, Friendly);

                // Remove it from its start square.
                ulong castledRookSquare = startSquare << 3;
                RemovePiece(castledRookSquare, FirstSquareIndex(castledRookSquare), Rook, Friendly);
            }
        }


        uint newCastlingRights = FourBitCastlingRights();

        if (newCastlingRights != oldCastlingRights)
        {
            ZobristKey ^= CastlingRightsKeys[oldCastlingRights];
            ZobristKey ^= CastlingRightsKeys[newCastlingRights];
        }

        Friendly ^= 1;
        Opponent ^= 1;
        ZobristKey ^= BlackToMoveKey;

        TT.CalculateCurrentEntryIndex(this);

        IsCheckDataOutdated = true;

        GameStateHistory.Push(CurrentGameState());
        PositionKeyHistory.Push(ZobristKey);


        void MakePawnMove()
        {
            FiftyMovePlyCount = 0;

            bool isDoublePawnPush = moveFlag == PawnDoublePushFlag;

            // En passant.
            if (isEnPassant)
            {
                // Remove en passant target.
                RemovePiece(EnPassantTarget, FirstSquareIndex(EnPassantTarget), Pawn, Opponent);

                EnPassantTarget = 0;
                EnPassantSquare = 0;
            }

            // White double pawn push.
            else if (isDoublePawnPush && Friendly == 0)
            {
                EnPassantTarget = targetSquare;
                EnPassantSquare = startSquare << 8;
            }

            // Black double pawn push.
            else if (isDoublePawnPush && Friendly == 1)
            {
                EnPassantTarget = targetSquare;
                EnPassantSquare = startSquare >> 8;
            }

            else
            {
                EnPassantTarget = 0;
                EnPassantSquare = 0;
            }
        }

        void RemoveRookCastlingRights(ulong rookPosition, ulong rank)
        {
            // Only consider rooks at the edge of the board.
            ulong edgeRookPosition = rookPosition & Mask.RookEdge & rank;

            // Remove castling rights if the captured rook was 2 squares
            // to the left or 1 square to the right of the castling target.
            CastlingRights &= ~(edgeRookPosition << 2 | edgeRookPosition >> 1);
        }
    }

    public void UnmakeMove(Move move)
    {
        GameState currentPosition = GameStateHistory.Pop();
        GameState previousPosition = GameStateHistory.Peek();

        int moveFlag = move.Flag;
        bool isEnPassant = moveFlag == EnPassantCaptureFlag;

        int startSquareIndex = move.StartSquareIndex;
        ulong startSquare = move.StartSquare;

        int targetSquareIndex = move.TargetSquareIndex;
        ulong targetSquare = move.TargetSquare;

        int pieceType = PieceType(targetSquareIndex);
        int pieceTypeOrPromotedPawn = move.PromotionPieceType == None ? pieceType : Pawn;
        int pieceTypeOrPromotion = move.PromotionPieceType == None ? pieceType : move.PromotionPieceType;

        int capturedPieceType = currentPosition.CapturedPieceType;

        // Restore current turn before anything else.
        Friendly ^= 1;
        Opponent ^= 1;
        ZobristKey ^= BlackToMoveKey;

        // Reset castling rights.
        ulong oldCastlingRights = FourBitCastlingRights();

        CastlingRights = previousPosition.CastlingRights;

        ulong newCastlingRights = FourBitCastlingRights();

        if (newCastlingRights != oldCastlingRights)
        {
            ZobristKey ^= CastlingRightsKeys[oldCastlingRights];
            ZobristKey ^= CastlingRightsKeys[newCastlingRights];
        }


        // Remove old en passant square.
        ZobristKey ^= EnPassantFileKeys[EnPassantSquare != 0 ? GetFile(FirstSquareIndex(EnPassantSquare)) + 1 : 0];

        // Reset en passant data.
        EnPassantSquare = previousPosition.EnPassantSquare;
        EnPassantTarget = previousPosition.EnPassantTarget;

        // Add new en passant square.
        ZobristKey ^= EnPassantFileKeys[EnPassantSquare != 0 ? GetFile(FirstSquareIndex(EnPassantSquare)) + 1 : 0];


        // Place moved piece back on the start square.
        AddPiece(startSquare, startSquareIndex, pieceTypeOrPromotedPawn, Friendly);

        // Empty the move's target square.
        RemovePiece(targetSquare, targetSquareIndex, pieceTypeOrPromotion, Friendly);

        // Restore any captured piece.
        if (capturedPieceType != None)
        {
            if (isEnPassant) AddPiece(EnPassantTarget, FirstSquareIndex(EnPassantTarget), capturedPieceType, Opponent);

            else AddPiece(targetSquare, targetSquareIndex, capturedPieceType, Opponent);
        }


        // Note: since this function only deals with en passant, promoted pawns do not go through it, so pieceType is used instead of pieceTypeOrPromotedPawn.
        // Note: removed because en passant is handled above.
        //if (pieceType == Pawn) UnmakePawnMove();

        // If the king is castling, the rook should follow it.
        if (pieceType == King)
        {
            // Queenside castling
            if (startSquare >> 2 == targetSquare)
            {
                // Remove rook from the target square.
                ulong castledRookTarget = targetSquare << 1;
                RemovePiece(castledRookTarget, FirstSquareIndex(castledRookTarget), Rook, Friendly);

                // Add it to its start square.
                ulong castledRookSquare = startSquare >> 4;
                AddPiece(castledRookSquare, FirstSquareIndex(castledRookSquare), Rook, Friendly);
            }

            // Kingside castling
            else if (startSquare << 2 == targetSquare)
            {
                // Remove rook from the target square.
                ulong castledRookTarget = targetSquare >> 1;
                RemovePiece(castledRookTarget, FirstSquareIndex(castledRookTarget), Rook, Friendly);

                // Add it to its start square.
                ulong castledRookSquare = startSquare << 3;
                AddPiece(castledRookSquare, FirstSquareIndex(castledRookSquare), Rook, Friendly);
            }
        }

        CapturedPieceType = previousPosition.CapturedPieceType;

        PlyCount--;
        FiftyMovePlyCount = previousPosition.FiftyMovePlyCount;

        TT.CalculateCurrentEntryIndex(this);

        PositionKeyHistory.Pop();

        // NOTE: If the check data was up to date before making the move, unmaking it would restore the correct data.
        // A possible optimization is to check for this to avoid potentially having to recalculate it unnecessaraly,
        // for example by caching and extra "backup" set of data.
        IsCheckDataOutdated = true;
    }

    private void AddPiece(ulong square, int squareIndex, int pieceType, int colorIndex)
    {
        Squares[squareIndex] = (colorIndex << 3) | pieceType;

        // Add the piece to its bitboard.
        Pieces[pieceType][colorIndex] |= square;

        // Update occupied squares.
        OccupiedSquares[colorIndex] |= square;
        AllOccupiedSquares |= square;

        if (pieceType.IsSlidingPiece())
        {
            SlidingPieces[colorIndex] |= square;
            AllSlidingPieces |= square;
        }

        // Dynamically update the king's position.
        if (pieceType == King) KingPosition[colorIndex] = squareIndex;

        if (pieceType == Pawn)
        {
            if (colorIndex == 0)
                PawnAttackedSquares[0] |= (square & ~Files[0]) << 7 | (square & ~Files[7]) << 9;

            if (colorIndex == 1)
                PawnAttackedSquares[1] |= (square & ~Files[0]) >> 9 | (square & ~Files[7]) >> 7;
        }

        ZobristKey ^= PieceKeys[pieceType, colorIndex, squareIndex];

        // Update material score.
        MaterialScore[colorIndex] += Evaluation.PieceValues[pieceType];

        // Update piece square tables score.
        PsqtScore[colorIndex] += PieceSquareTables.ReadScore(pieceType, squareIndex, colorIndex == 0);
    }

    private void RemovePiece(ulong square, int squareIndex, int pieceType, int colorIndex)
    {
        Squares[squareIndex] = None;

        // Remove the piece from its bitboard.
        Pieces[pieceType][colorIndex] &= ~square;

        // Update occupied squares.
        OccupiedSquares[colorIndex] &= ~square;
        AllOccupiedSquares &= ~square;

        if (pieceType.IsSlidingPiece())
        {
            SlidingPieces[colorIndex] &= ~square;
            AllSlidingPieces &= ~square;
        }

        // Dynamically update the pawn attacked squares map.
        // Note: if a pawn is removed, its attacked squares should
        // only be removed if they weren't also attacked by other pawns.
        if (pieceType == Pawn)
        {
            // The pawn must already have been removed from the Pawns bitboard.
            if (colorIndex == 0)
                PawnAttackedSquares[0] &= ~(
                    ((square & ~Files[0]) << 7 | (square & ~Files[7]) << 9) &
                    ~((Pawns[0] & ~Files[0]) << 7 | (Pawns[0] & ~Files[7]) << 9));

            if (colorIndex == 1)
                PawnAttackedSquares[1] &= ~(
                    ((square & ~Files[0]) >> 9 | (square & ~Files[7]) >> 7) &
                    ~((Pawns[1] & ~Files[0]) >> 9 | (Pawns[1] & ~Files[7]) >> 7));
        }

        // Update the Zobrist key.
        ZobristKey ^= PieceKeys[pieceType, colorIndex, squareIndex];

        // Update material score.
        MaterialScore[colorIndex] -= Evaluation.PieceValues[pieceType];

        // Update piece square tables score.
        PsqtScore[colorIndex] -= PieceSquareTables.ReadScore(pieceType, squareIndex, colorIndex == 0);
    }

    public uint FourBitCastlingRights()
    {
        return (uint)
        ((CastlingRights >> 6) | /* White kingside castling. */
        (CastlingRights >> 1) | /* White queenside castling. */
        (CastlingRights >> 60) | /* Black kingside castling. */
        (CastlingRights >> 55)) /* Black queenside castling. */
        & 0b1111;
    }


    public void MakeNullMove()
    {
        PlyCount++;
        FiftyMovePlyCount++;

        CapturedPieceType = None;

        // Remove old en passant square.
        ZobristKey ^= EnPassantFileKeys[EnPassantSquare != 0 ? GetFile(FirstSquareIndex(EnPassantSquare)) + 1 : 0];

        EnPassantSquare = 0;
        EnPassantTarget = 0;

        // Add new en passant square (none).
        ZobristKey ^= EnPassantFileKeys[0];

        // NOTE: Making a null move doesn't change the castling rights.

        ZobristKey ^= BlackToMoveKey;

        Friendly ^= 1;
        Opponent ^= 1;

        TT.CalculateCurrentEntryIndex(this);

        GameStateHistory.Push(CurrentGameState());

        // NOTE: Null moves should not be added to PositionKeyHistory.

        IsCheckDataOutdated = true;
    }

    public void UnmakeNullMove()
    {
        GameState currentPosition = GameStateHistory.Pop();
        GameState previousPosition = GameStateHistory.Peek();

        // Remove old en passant square (none).
        ZobristKey ^= EnPassantFileKeys[0];

        // Reset en passant data.
        EnPassantSquare = previousPosition.EnPassantSquare;
        EnPassantTarget = previousPosition.EnPassantTarget;

        // Add new en passant square.
        ZobristKey ^= EnPassantFileKeys[EnPassantSquare != 0 ? GetFile(FirstSquareIndex(EnPassantSquare)) + 1 : 0];

        CapturedPieceType = previousPosition.CapturedPieceType;
        CastlingRights = previousPosition.CastlingRights;

        ZobristKey ^= BlackToMoveKey;

        Friendly ^= 1;
        Opponent ^= 1;

        PlyCount--;
        FiftyMovePlyCount--;

        TT.CalculateCurrentEntryIndex(this);

        IsCheckDataOutdated = true;
    }


    public List<Move> GenerateAllLegalMoves(bool capturesOnly = false)
    {
        UpdateCheckData();

        List<Move> movesList = new();
        movesList.AddRange(GenerateLegalMoves(Pawn,   MoveType.Pawn,      capturesOnly, false));
        movesList.AddRange(GenerateLegalMoves(Knight, MoveType.Normal,    capturesOnly, false));
        movesList.AddRange(GenerateLegalMoves(Bishop, MoveType.Sliding,   capturesOnly, false));
        movesList.AddRange(GenerateLegalMoves(Rook,   MoveType.Sliding,   capturesOnly, false));
        movesList.AddRange(GenerateLegalMoves(Queen,  MoveType.Sliding,   capturesOnly, false));
        movesList.AddRange(GenerateLegalMoves(King,   MoveType.Normal,    capturesOnly, false));
        return movesList;
    }

    private List<Move> GenerateLegalMoves(int pieceType, MoveType moveType, bool capturesOnly, bool friendlyCaptures)
    {
        List<Move> movesList = new();

        ulong pieces = Pieces[pieceType][Friendly];
        while (pieces != 0)
        {
            // Isolate the first piece.
            int squareIndex = FirstSquareIndex(pieces);
            pieces &= pieces - 1;

            ulong square = 1UL << squareIndex;

            ulong moves = 0;
            switch (moveType)
            {
                case MoveType.Normal:

                    moves = PrecomputedMoveData.Moves[pieceType][squareIndex, 0];

                    // Check if the king is allowed to castle.
                    if (pieceType == King)
                    {
                        // Find any pieces in bewtween the king and the rooks.
                        const ulong squaresBetweenKingAndRooks = 0x6e;
                        ulong blockers = AllOccupiedSquares & (squaresBetweenKingAndRooks << (Friendly * 56));

                        // Find the castling moves based on the current castling rights.
                        const ulong initalCastlingRights = 0x44;
                        ulong castlingMoves = CastlingRights & (initalCastlingRights << (Friendly * 56));

                        // Exclude any moves that are blocked by other pieces.
                        // This is done by subtracting from the castling moves
                        // the map of the blocking pieces shifted left and right.
                        moves |= castlingMoves & ~(blockers << 1 | blockers | blockers >> 1);
                    }

                    break;

                case MoveType.Sliding:
                    // Sliding pieces have precomputed bitboards with every possible combination of blockers.
                    moves = MagicBitboard.GetAttacks(pieceType, squareIndex, AllOccupiedSquares);
                    break;

                case MoveType.Pawn:
                    ulong allBlockers = AllOccupiedSquares;

                    // Pawns can only move forward if there is not a blocking piece.
                    moves |= PrecomputedMoveData.Moves[Pawn][squareIndex, Friendly] & ~allBlockers;

                    // If the pawn was able to move forward, it may be able to push again if on the second rank.
                    if (moves != 0) moves |= PrecomputedMoveData.Moves[Pawn][squareIndex, Friendly + 2] & ~allBlockers;

                    // Capture diagonally only if there is a piece or if en passant can be performed.
                    moves |= PrecomputedMoveData.Moves[pieceType][squareIndex, Friendly + 4] & (allBlockers | EnPassantSquare);

                    break;
            }

            // Only include occupied squares if capturesOnly is on. Also include en passant captures.
            if (capturesOnly) moves &= AllOccupiedSquares | (pieceType == Pawn ? EnPassantSquare : 0);

            // Remove friendly blockers (all captures were included, but only enemy pieces can actually be captured).
            // Note: en passant is not included as it's not possible to perform on pawns of the same color.
            if (!friendlyCaptures) moves &= ~OccupiedSquares[Friendly];

            // Add each move in the bitboard to the movesList.
            while (moves != 0)
            {
                int targetSquareIndex = FirstSquareIndex(moves);
                moves &= moves - 1;

                ulong targetSquare = 1UL << targetSquareIndex;

                bool isPromotion = false;
                int moveFlag = 0;

                if (pieceType == Pawn)
                {
                    if ((targetSquare & Mask.PromotionRanks) != 0)
                        isPromotion = true;

                    else if ((targetSquare & EnPassantSquare) != 0)
                        moveFlag = EnPassantCaptureFlag;

                    else if ((square & Mask.SecondRank) != 0 && (targetSquare & Mask.FourthRank) != 0)
                        moveFlag = PawnDoublePushFlag;
                }

                if (isPromotion) 
                { 
                    Move queenPromotion = new(squareIndex, targetSquareIndex, PromotionToQueenFlag);

                    // If one promotion is legal, all of them are.
                    if (IsLegal(queenPromotion))
                    {
                        movesList.Add(queenPromotion);
                        movesList.Add(new(squareIndex, targetSquareIndex, PromotionToKnightFlag));
                        movesList.Add(new(squareIndex, targetSquareIndex, PromotionToBishopFlag));
                        movesList.Add(new(squareIndex, targetSquareIndex, PromotionToRookFlag));
                    }
                }

                else
                {
                    Move move = new(squareIndex, targetSquareIndex, moveFlag);
                    if (IsLegal(move)) movesList.Add(move);
                }
            }
        }

        return movesList;
    }

    public bool IsLegal(Move move)
    {
        int startSquareIndex = move.StartSquareIndex;
        ulong startSquare = 1UL << startSquareIndex;

        int targetSquareIndex = move.TargetSquareIndex;
        ulong targetSquare = 1UL << targetSquareIndex;

        int pieceType = PieceType(startSquareIndex);


        bool inCheck = IsKingInCheck[Friendly];
        bool isKing = pieceType == King;

        int kingPosition = KingPosition[Friendly];

        // King moves legality is handled separately.
        if (isKing)
        {
            // The king cannot move to an attacked square.
            if (AttackersTo(targetSquareIndex, Friendly, Opponent, AllOccupiedSquares) != 0)
            {
                return false;
            }

            // Queenside castling
            if (startSquare >> 2 == targetSquare)
            {
                // Castling is not allowed while in check.
                if (inCheck)
                {
                    return false;
                }

                // The king cannot castle through an attack.
                if (AttackersTo(startSquareIndex - 1, Friendly, Opponent, AllOccupiedSquares) != 0)
                {
                    return false;
                }
            }

            // Kingside castling
            if (startSquare << 2 == targetSquare)
            {
                // Castling is not allowed while in check.
                if (inCheck)
                {
                    return false;
                }

                // The king cannot castle through an attack.
                if (AttackersTo(startSquareIndex + 1, Friendly, Opponent, AllOccupiedSquares) != 0)
                {
                    return false;
                }
            }

            // The king must not remain in the line of sight of the checking piece, even if going backwards.
            // TODO: This can likely be simplified by just removing moves the square behind the king the direction of the attack. 
            if (inCheck)
            {
                ulong checkingPieces = CheckingPieces[Friendly];

                int firstCheckingPieceIndex = FirstSquareIndex(checkingPieces);
                int secondCheckingPieceIndex = LastSquareIndex(checkingPieces);

                // Is the king being attacked by a rook or by a bishop?
                // Note that in case a queen is attacking, it will be assigned rook or bishop
                // depending on the direction of the attack (horizontal/vertical or diagonal).
                int firstCheckingPieceType = 
                    GetRank(firstCheckingPieceIndex) == GetRank(startSquareIndex) || 
                    GetFile(firstCheckingPieceIndex) == GetFile(startSquareIndex) ? Rook : Bishop;

                // If there is an attack from a sliding piece, remove any moves in the same line as the attack.
                // Note that this is not handled by removing attacked squares because squares currently behind the
                // king are not considered attacked (the king is blocking the attack), but still need to be avoided.
                if (IsSlidingPiece(1UL << firstCheckingPieceIndex) && firstCheckingPieceType != None)
                {
                    if (PrecomputedMoveData.XRay[firstCheckingPieceType][firstCheckingPieceIndex, targetSquareIndex] != 0)
                    {
                        return false;
                    }
                }

                // In case of a double check, the king must also avoid the line of sight of the second piece.
                if (secondCheckingPieceIndex != firstCheckingPieceIndex)
                {
                    int secondCheckingPieceType = 
                        GetRank(secondCheckingPieceIndex) == GetRank(startSquareIndex) || 
                        GetFile(secondCheckingPieceIndex) == GetFile(startSquareIndex) ? Rook : Bishop;

                    if (IsSlidingPiece(1UL << secondCheckingPieceIndex) && secondCheckingPieceType != None)
                    {
                        if (PrecomputedMoveData.XRay[secondCheckingPieceType][secondCheckingPieceIndex, targetSquareIndex] != 0)
                        {
                            return false;
                        }
                    }
                }
            }
        }

        // Other pieces.
        else
        {
            // Checks must be stopped immediately, by either capturing the
            // checking piece or blocking its line of sight to the king.
            if (inCheck)
            {
                ulong checkingPieces = CheckingPieces[Friendly];

                int firstCheckingPieceIndex = FirstSquareIndex(checkingPieces);
                int secondCheckingPieceIndex = LastSquareIndex(checkingPieces);

                // In case of a double attack on the king, the only option
                // is to move the king to safety: other pieces cannot move.
                if (firstCheckingPieceIndex != secondCheckingPieceIndex)
                {
                    return false;
                }

                if (IsSlidingPiece(1UL << firstCheckingPieceIndex))
                {
                    // A check must be stoppped by blocking the attacker's line of sight.
                    if ((targetSquare & PrecomputedMoveData.Line[firstCheckingPieceIndex, kingPosition]) == 0)
                    {
                        return false;
                    }
                }

                // In case of a non-sliding attack, the attacker must be captured.
                else if (targetSquareIndex != firstCheckingPieceIndex)
                {
                    // The attacker can also be captured with en passant.
                    bool attackerIsEnPassantTarget = (1UL << firstCheckingPieceIndex) == EnPassantTarget;
                    bool moveIsEnPassant = pieceType == Pawn && targetSquare == EnPassantSquare;
                    
                    if (!(attackerIsEnPassantTarget && moveIsEnPassant))
                    {
                        return false;
                    }
                }
            }

            // If the piece is pinned to the king, it can only move in the line between the attacker and the king.
            if ((startSquare & Pins[Friendly]) != 0)
            {
                // Since the piece cannot jump over the attacker or the king, the line from the attacker to the king
                // is the same as the line from the piece to the king extended to reach the edges of the board.
                if ((targetSquare & PrecomputedMoveData.LineToEdges[startSquareIndex, kingPosition]) == 0)
                {
                    // Pawns need extra consideration, because a pawn may be pinned even though a second piece is blocking the attack as well.
                    // If 4 pieces are involved in the pin, the only illegal move is en passant. See FindPinRays for more info.
                    if (pieceType == Pawn &&
                        PieceCount((Pins[Friendly] & PrecomputedMoveData.LineToEdges[startSquareIndex, kingPosition]) & AllOccupiedSquares) == 4)
                    {
                        if ((targetSquare & EnPassantSquare) != 0) return false;
                    }

                    else return false;
                }
            }


            //ulong slidingAttackers = SlidingAttackersTo(startSquareIndex, Friendly, Opponent,
            //    AllOccupiedSquares & ~(pieceType == Pawn && move.TargetSquare == EnPassantSquare ? EnPassantTarget : 0) /* In case the move is en passant, remove the en passant target from the occupied squares map to calculate pins correctly. */);

            //while (slidingAttackers != 0)
            //{
            //    int attackerIndex = FirstSquareIndex(slidingAttackers);
            //    int attackerType = GetRank(attackerIndex) == GetRank(startSquareIndex) || GetFile(attackerIndex) == GetFile(startSquareIndex) ? Rook : Bishop;


            //    ulong attackRayToKing = PrecomputedMoveData.XRay[attackerType][attackerIndex, kingPosition];

            //    if (attackRayToKing != 0 &&
            //        (move.StartSquare & attackRayToKing) != 0 /* The moving piece is in the ray of attack. */ &&
            //        (move.TargetSquare & attackRayToKing) == 0 /* The target is not on the ray. If the piece is pinned this move is illegal. */)
            //    {
            //        if (PieceCount(AllOccupiedSquares & attackRayToKing) == 3)
            //        {
            //            return false;
            //        }

            //        if (GetRank(attackerIndex) == GetRank(startSquareIndex) &&
            //            pieceType == Pawn && move.TargetSquare == EnPassantSquare &&
            //            PieceCount(AllOccupiedSquares & attackRayToKing) == 4)
            //        {
            //            return false;
            //        }
            //    }

            //    slidingAttackers &= slidingAttackers - 1;
            //}
        }

        return true;
    }


    /// <summary>
    /// Update the list representation of the board
    /// </summary>
    public void UpdateSquares()
    {
        Squares = new int[64];

        foreach (var bitboard in Pieces)
        {
            ulong bit = 1;
            int index = 0;

            // After the final bit shift, the "1" will exit the boundaries of the ulong and the remaining value will be 0m
            while (bit != 0)
            {
                for (int color = 0; color < 2; color++)
                {
                    // If the bit is occupied, fill its slot in the Squares array with the piece's info.
                    if ((bitboard.Value[color] & bit) != 0)
                    {
                        Squares[index] = bitboard.Key | (color == 0 ? White : Black);
                    }
                }

                bit <<= 1;
                index++;
            }
        }
    }


    // Any square that is attacked by a piece could always attack that piece back
    // if there was a piece of the same type on the square. For pawns, this
    // assumption only holds true if the color of the attacker is inverted.
    public ulong AttackersTo(int squareIndex, int colorIndex, int opponentColorIndex, ulong occupiedSquares)
    {
        return
            (PrecomputedMoveData.Moves[Pawn][squareIndex, colorIndex + 4]     & Pawns[opponentColorIndex]) |
            (PrecomputedMoveData.Moves[Knight][squareIndex, 0]                & Knights[opponentColorIndex]) |
            (PrecomputedMoveData.Moves[King][squareIndex, 0]                  & Kings[opponentColorIndex]) |
            (MagicBitboard.GetAttacks(Rook, squareIndex, occupiedSquares)     & (Rooks[opponentColorIndex] | Queens[opponentColorIndex])) |
            (MagicBitboard.GetAttacks(Bishop, squareIndex, occupiedSquares)   & (Bishops[opponentColorIndex] | Queens[opponentColorIndex]));
    }

    public ulong PawnAttackersTo(int squareIndex, int colorIndex, int opponentColorIndex)
    {
        return PrecomputedMoveData.Moves[Pawn][squareIndex, colorIndex + 4] & Pawns[opponentColorIndex];
    }

    public ulong SlidingAttackersTo(int squareIndex, int colorIndex, int opponentColorIndex, ulong occupiedSquares)
    {
        return
            (MagicBitboard.GetAttacks(Rook, squareIndex, occupiedSquares)   & (Rooks[opponentColorIndex] | Queens[opponentColorIndex])) |
            (MagicBitboard.GetAttacks(Bishop, squareIndex, occupiedSquares) & (Bishops[opponentColorIndex] | Queens[opponentColorIndex]));
    }

    /// <summary>Bitboard of all the squares attacked by the specified piece, including enemy and friendly captures.</summary>
    public ulong AttacksFrom(int squareIndex, int pieceType, ulong occupiedSquares)
    {
        return
            pieceType == Knight ? PrecomputedMoveData.Moves[Knight][squareIndex, 0] :
            pieceType == Bishop ? MagicBitboard.GetAttacks(Bishop, squareIndex, occupiedSquares) :
            pieceType == Rook ? MagicBitboard.GetAttacks(Rook, squareIndex, occupiedSquares) :
            pieceType == Queen ? MagicBitboard.GetAttacks(Rook, squareIndex, occupiedSquares) | MagicBitboard.GetAttacks(Bishop, squareIndex, occupiedSquares) : 0;
    }

    /// <summary>
    /// Update bitboards with checking pieces and pin rays, and update <see cref="IsKingInCheck"/>.
    /// </summary>
    public void UpdateCheckData(bool excludePins = false)
    {
        if (IsCheckDataOutdated)
        {
            CheckingPieces[0] = AttackersTo(KingPosition[0], 0, 1, AllOccupiedSquares);
            CheckingPieces[1] = AttackersTo(KingPosition[1], 1, 0, AllOccupiedSquares);

            IsKingInCheck[0] = CheckingPieces[0] != 0;
            IsKingInCheck[1] = CheckingPieces[1] != 0;

            IsCheckDataOutdated = false;
        }

        if (!excludePins)
        {
            Pins[0] = FindPinRays(0, 1);
            Pins[1] = FindPinRays(1, 0);
        }

        ulong FindPinRays(int friendlyColorIndex, int opponentColorIndex)
        {
            int kingSquareIndex = KingPosition[friendlyColorIndex];

            // First find the enemy sliding pieces closest to the king in each direction, by finding pieces that would check the king if there were no friendly blockers.
            ulong potentialAttackingPieces = SlidingAttackersTo(kingSquareIndex, friendlyColorIndex, opponentColorIndex, 1UL << kingSquareIndex);
            
            ulong pinRays = 0;
            while (potentialAttackingPieces != 0)
            {
                int attackingPieceSquareIndex = FirstSquareIndex(potentialAttackingPieces);
                int attackingPieceType = PieceType(attackingPieceSquareIndex);

                if (attackingPieceType == 0)
                {
                    ulong test = SlidingAttackersTo(kingSquareIndex, friendlyColorIndex, opponentColorIndex, 1UL << kingSquareIndex);
                }

                // The ray will be empty if the piece cannot target the king (eg. rook cannot target diagonally).
                ulong rayToKing = PrecomputedMoveData.XRay[attackingPieceType][attackingPieceSquareIndex, kingSquareIndex];

                // There should be three pieces on the ray: the attacker, the king and the pinned piece.
                if (rayToKing != 0 && PieceCount(rayToKing & AllOccupiedSquares) == 3) pinRays |= rayToKing;

                // A special case: a pawn that could permorm en passant, which would remove two pieces at once from the rank discovering an attack on the king.
                else if (
                    rayToKing != 0 && PieceCount(rayToKing & AllOccupiedSquares) == 4 &&
                    (EnPassantTarget & rayToKing) != 0 &&
                    GetRank(attackingPieceSquareIndex) == GetRank(kingSquareIndex)) 
                    pinRays |= rayToKing;

                potentialAttackingPieces &= potentialAttackingPieces - 1;
            }

            return pinRays;
        }
    }

    /// <remarks>
    /// Should only be called on board initialization
    /// </remarks>
    public void UpdateKingPositions()
    {
        KingPosition[0] = FirstSquareIndex(Kings[0]);
        KingPosition[1] = FirstSquareIndex(Kings[1]);
    }

    public bool IsInCheck(int colorIndex)
    {
        if (IsCheckDataOutdated) UpdateCheckData(true);

        return IsKingInCheck[colorIndex];
    }

    public void UpdatePawnAttackedSquares()
    {
        PawnAttackedSquares[0] = ((Pawns[0] & ~Files[0]) << 7) | ((Pawns[0] & ~Files[7]) << 9);
        PawnAttackedSquares[1] = ((Pawns[1] & ~Files[0]) >> 9) | ((Pawns[1] & ~Files[7]) >> 7);
    }


    public static int GetFile(int squareIndex)
    {
        return squareIndex & 0b111;
    }

    public static int GetRank(int squareIndex)
    {
        return squareIndex >> 3;
    }

    public static bool CheckBoundaries(int squareIndex)
    {
        return squareIndex >= 0 && squareIndex < 64;
    }

    public int PieceType(int squareIndex) => Squares[squareIndex].PieceType();

    public int PieceColor(int squareIndex)
    {
        return Squares[squareIndex].PieceColor();
    }

    public bool IsSlidingPiece(ulong square) => (AllSlidingPieces & square) != 0;

    public static List<int> GetIndexes(ulong value)
    {
        var indexes = new List<int>();
        for (int i = 0; value != 0; i++)
        {
            if ((value & 1) == 1)
            {
                indexes.Add(i);
            }
            value >>= 1;
        }
        return indexes;
    }


    public override bool Equals(object? obj)
    {
        Board? other = obj as Board;
        if (other == null) return false;

        bool arePiecesEqual = true;
        for (int pieceType = Pawn; pieceType <= King; pieceType++)
        {
            arePiecesEqual &= Pieces[pieceType][0] == other.Pieces[pieceType][0];
            arePiecesEqual &= Pieces[pieceType][1] == other.Pieces[pieceType][1];
        }

        return
            Enumerable.SequenceEqual(Squares, other.Squares) &&
            arePiecesEqual &&
            Friendly == other.Friendly &&
            Opponent == other.Opponent &&
            Enumerable.SequenceEqual(OccupiedSquares, other.OccupiedSquares) &&
            AllOccupiedSquares == other.AllOccupiedSquares &&
            Enumerable.SequenceEqual(SlidingPieces, other.SlidingPieces) &&
            AllSlidingPieces == other.AllSlidingPieces &&
            Enumerable.SequenceEqual(KingPosition, other.KingPosition) &&
            (CheckingPieces == other.CheckingPieces || (IsCheckDataOutdated || other.IsCheckDataOutdated)) &&
            (Enumerable.SequenceEqual(IsKingInCheck, other.IsKingInCheck) || (IsCheckDataOutdated || other.IsCheckDataOutdated)) &&
            /* IsCheckDataOutdated == other.IsCheckDataOutdated && */
            (Enumerable.SequenceEqual(Pins, other.Pins) || (IsCheckDataOutdated || other.IsCheckDataOutdated)) &&
            Enumerable.SequenceEqual(PawnAttackedSquares, other.PawnAttackedSquares) &&
            EnPassantSquare == other.EnPassantSquare &&
            EnPassantTarget == other.EnPassantTarget &&
            /* EnPassantSquareBackup == other.EnPassantSquareBackup && */
            /* EnPassantTargetBackup == other.EnPassantTargetBackup && */
            CastlingRights == other.CastlingRights &&
            ZobristKey == other.ZobristKey &&
            Enumerable.SequenceEqual(GameStateHistory, other.GameStateHistory) &&
            Enumerable.SequenceEqual(PositionKeyHistory, other.PositionKeyHistory) &&
            Enumerable.SequenceEqual(PsqtScore, other.PsqtScore) &&
            Enumerable.SequenceEqual(MaterialScore, other.MaterialScore) &&
            Fen.GetCurrentFen(this) == Fen.GetCurrentFen(other);
    }

    public Board Clone()
    {
        return new()
        {
            Squares = Squares.Select(e => e).ToArray(),
            Pieces = Pieces.ToDictionary(entry => entry.Key, entry => entry.Value.Select(e => e).ToArray()),
            Friendly = Friendly,
            Opponent = Opponent,
            OccupiedSquares = OccupiedSquares.Select(e => e).ToArray(),
            AllOccupiedSquares = AllOccupiedSquares,
            SlidingPieces = SlidingPieces.Select(e => e).ToArray(),
            AllSlidingPieces = AllSlidingPieces,
            KingPosition = KingPosition.Select(e => e).ToArray(),
            CheckingPieces = CheckingPieces,
            IsKingInCheck = IsKingInCheck.Select(e => e).ToArray(),
            IsCheckDataOutdated = IsCheckDataOutdated,
            Pins = Pins.Select(e => e).ToArray(),
            PawnAttackedSquares = PawnAttackedSquares.Select(e => e).ToArray(),
            EnPassantSquare = EnPassantSquare,
            EnPassantTarget = EnPassantTarget,
            EnPassantSquareBackup = EnPassantSquareBackup,
            EnPassantTargetBackup = EnPassantTargetBackup,
            CastlingRights = CastlingRights,
            ZobristKey = ZobristKey,
            /* The stack is constructed twice because the order would be reversed. */
            GameStateHistory = new(new Stack<GameState>(GameStateHistory)),
            PositionKeyHistory = new(new Stack<ulong>(PositionKeyHistory)),
            PsqtScore = PsqtScore.Select(e => e).ToArray(),
            MaterialScore = MaterialScore.Select(e => e).ToArray()
        };
    }

    public List<string> FindDifferences(Board other)
    {
        List<string> differences = new();

        bool arePiecesEqual = true;
        for (int pieceType = Pawn; pieceType <= King; pieceType++)
        {
            arePiecesEqual &= Pieces[pieceType][0] == other.Pieces[pieceType][0];
            arePiecesEqual &= Pieces[pieceType][1] == other.Pieces[pieceType][1];
        }

        if (!Enumerable.SequenceEqual(Squares, other.Squares)) differences.Add("Squares");
        if (!arePiecesEqual) differences.Add("Pieces");
        if (!(Friendly == other.Friendly)) differences.Add("Friendly");
        if (!(Opponent == other.Opponent)) differences.Add("Opponent");
        if (!Enumerable.SequenceEqual(OccupiedSquares, other.OccupiedSquares)) differences.Add("Occupied Squares");
        if (!(AllOccupiedSquares == other.AllOccupiedSquares)) differences.Add("All Occupied Squares");
        if (!Enumerable.SequenceEqual(SlidingPieces, other.SlidingPieces)) differences.Add("Sliding Pieces");
        if (!(AllSlidingPieces == other.AllSlidingPieces)) differences.Add("All Sliding Pieces");
        if (!Enumerable.SequenceEqual(KingPosition, other.KingPosition)) differences.Add("King Position");
        if (!(CheckingPieces == other.CheckingPieces)) differences.Add("Checking Pieces");
        if (!Enumerable.SequenceEqual(IsKingInCheck, other.IsKingInCheck)) differences.Add("Is King In Check");
        if (!Enumerable.SequenceEqual(Pins, other.Pins)) differences.Add("Pins");
        if (!Enumerable.SequenceEqual(PawnAttackedSquares, other.PawnAttackedSquares)) differences.Add("Pawn Attacked Squares");
        if (!(EnPassantSquare == other.EnPassantSquare)) differences.Add("En Passant Square");
        if (!(EnPassantTarget == other.EnPassantTarget)) differences.Add("En Passant Target");
        if (!(CastlingRights == other.CastlingRights)) differences.Add("Castling Rights");
        if (!(ZobristKey == other.ZobristKey)) differences.Add("Zobrist Key");
        if (!Enumerable.SequenceEqual(GameStateHistory, other.GameStateHistory)) differences.Add("Game State History");
        if (!Enumerable.SequenceEqual(PositionKeyHistory, other.PositionKeyHistory)) differences.Add("Position Key History");
        if (!Enumerable.SequenceEqual(PsqtScore, other.PsqtScore)) differences.Add("Psqt Score");
        if (!Enumerable.SequenceEqual(MaterialScore, other.MaterialScore)) differences.Add("Material Score");
        if (!(Fen.GetCurrentFen(this) == Fen.GetCurrentFen(other))) differences.Add("Fen String");

        return differences;
    }
}


public struct Mask
{
    public const ulong LeftCastlingPath = 0x800000000000008;
    public const ulong RightCastlingPath = 0x2000000000000020;
    public const ulong WhiteQueensideCastling = 0x4UL;
    public const ulong WhiteKingsideCastling = 0x40UL;
    public const ulong BlackQueensideCastling = 0x4UL << 56;
    public const ulong BlackKingsideCastling = 0x40UL << 56;
    public const ulong RookEdge = ~0x7eUL;
    public const ulong WhitePromotionRank = 0xffUL << 56;
    public const ulong BlackPromotionRank = 0xffUL;
    public const ulong PromotionRanks = WhitePromotionRank | BlackPromotionRank;
    public const ulong PawnsRank = 0x10000010000;
    public const ulong WhitePawnsRank = 0xff0000;
    public const ulong BlackPawnsRank = 0xff0000000000;
    public const ulong DoublePawnsRank = 0x101000000;
    public const ulong WhiteDoublePawnsRank = 0xff000000;
    public const ulong BlackDoublePawnsRank = 0xff00000000;
    public const ulong WhiteRank = 0xff;
    public const ulong BlackRank = 0xff00000000000000;
    public const ulong WhiteCastledKingPosition = 0xe7;
    public const ulong BlackCastledKingPosition = 0xe700000000000000;
    public const ulong SecondRank = 0xff00000000ff00;
    public const ulong FourthRank = 0xffff000000;

    /// <summary>White's and black's seventh ranks.</summary>
    public const ulong SeventhRanks = 0xff00000000ff00;
    public const ulong LightSquares = 0x55aa55aa55aa55aa;
    public const ulong DarkSquares = 0xaa55aa55aa55aa55;
    public const ulong BlackHalf = 0xffffffff18000000;
    public const ulong WhiteHalf = 0x18ffffffff;
    public const ulong WhiteOutpostRanks = 0xffffff000000;
    public const ulong BlackOutpostRanks = 0xffffff0000;
}

public enum Square
{
    a1, b1, c1, d1, e1, f1, g1, h1,
    a2, b2, c2, d2, e2, f2, g2, h2,
    a3, b3, c3, d3, e3, f3, g3, h3,
    a4, b4, c4, d4, e4, f4, g4, h4,
    a5, b5, c5, d5, e5, f5, g5, h5,
    a6, b6, c6, d6, e6, f6, g6, h6,
    a7, b7, c7, d7, e7, f7, g7, h7,
    a8, b8, c8, d8, e8, f8, g8, h8
}
