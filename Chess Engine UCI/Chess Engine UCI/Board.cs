using static Utilities.Bitboard;
using static Utilities.Zobrist;
using static Piece;
using static Move;

public class Board
{
    // List representation of the board.
    public static int[] Squares { get; set; }

    // Bitboard representations of the board.
    public static Dictionary<int, ulong[]> Pieces { get; set; }


    public static ulong[] Pawns => Pieces[Pawn];
    public static ulong[] Knights => Pieces[Knight];
    public static ulong[] Bishops => Pieces[Bishop];
    public static ulong[] Rooks => Pieces[Rook];
    public static ulong[] Queens => Pieces[Queen];
    public static ulong[] Kings => Pieces[King];


    // 0 = white to move.
    // 1 = black to move.
    public static int CurrentTurn { get; set; }
    public static int OpponentTurn { get; set; }


    // Board information storage for quick lookup.
    public static ulong[] OccupiedSquares = new ulong[2];
    public static ulong AllOccupiedSquares;

    public static ulong[] SlidingPieces = new ulong[2];
    public static ulong AllSlidingPieces;

    public static int[] KingPosition = new int[2];

    public static bool[] IsKingInCheck = new bool[2];


    // All the squares currently attacked by each piece.
    // Updated every time a move is made/unmade.
    //public static ulong[,] Attacks = new ulong[2, 64];

    // All the squares currently attacked by a color.
    //public static ulong[] AttackedSquares = new ulong[2];

    /// <summary>Bitboards of the squares currently attacked by pawns of the given color.</summary>
    public static ulong[] PawnAttackedSquares = new ulong[2];


    // En passant is the only move that allows the capture of
    // a piece on a different square from the move's target,
    // so it needs some extra variables.

    // Square where en passant can be performed (3rd or 6th rank).
    public static ulong EnPassantSquare;
    // Square with the en passant target (4th or 5th rank).
    public static ulong EnPassantTarget;

    public static ulong EnPassantSquareBackup;
    public static ulong EnPassantTargetBackup;


    // Castling is a move that allows the king to move 2 squares
    // towards one of the initial rooks.
    // To be able to castle, the following rules apply:
    // 1. The king must have never moved from it's starting square.
    // 2. The rook must have never moved from it's starting square.
    // 3. The rook must not have been captured.
    // 4. The king must not currently be in check.
    // 5. The king must not travel through, or land on a square that is attacked by an enemy piece.

    // The positive bits represent the squares the king can castle to.
    public static ulong CastlingRights;


    // 64-bit key that is unique to (almost) every position.
    // Multiple positions may share a key because of the limited size
    // compared to the amount of possible chess positions, but the tradeoff
    // is necessary since Zobrist Hashing is (to my knowledge) the fastest and most efficient
    // method of retrieving a unique key used to look up the position in a transposition table.
    public static ulong ZobristKey;


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


    // Contains the history of zobrist keys of the game.
    // Used to detect draws by repetition.
    public static Stack<ulong> PositionHistory;


    public static uint[] PsqtScore;

    public static uint[] MaterialScore;


    public static void Init()
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
        PositionHistory = new();
        PsqtScore = new uint[2];
        MaterialScore = new uint[2];


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


    public static void UpdateBoardInformation(ulong startSquare = 0, ulong targetSquare = 0, bool enPassant = false, ulong enPassantTarget = 0, bool castling = false, ulong castledRookSquare = 0, ulong castledRookTarget = 0)
    {
        KingPosition[0] = FirstSquareIndex(Kings[0]);
        KingPosition[1] = FirstSquareIndex(Kings[1]);

        //if (startSquare != 0) UpdateAttackedSquares(startSquare, targetSquare, enPassant, enPassantTarget, castling, castledRookSquare, castledRookTarget);

        //UpdateAttacks();

        //UpdatePawnAttacks();

        // The king is in check if the opponent's attacked
        // squares map intersects with the king position map.
        //IsKingInCheck[0] = (Kings[0] & AttackedSquares[1]) != 0;
        //IsKingInCheck[1] = (Kings[1] & AttackedSquares[0]) != 0;

        IsKingInCheck[0] = AttackersTo(KingPosition[0], 0, 1, AllOccupiedSquares) != 0;
        IsKingInCheck[1] = AttackersTo(KingPosition[1], 1, 0, AllOccupiedSquares) != 0;


        //void UpdateAttacks()
        //{
        //    for (int i = 0; i < 64; i++)
        //    {
        //        if (AttackersTo(i, 1, 0, AllOccupiedSquares) != 0) AttackedSquares[0] |= 1UL << i;
        //        if (AttackersTo(i, 0, 1, AllOccupiedSquares) != 0) AttackedSquares[1] |= 1UL << i;
        //    }
        //}

        PawnAttackedSquares[0] = ((Pawns[0] & ~Files[0]) << 7) | ((Pawns[0] & ~Files[7]) << 9);
        PawnAttackedSquares[1] = ((Pawns[1] & ~Files[0]) >> 9) | ((Pawns[1] & ~Files[7]) >> 7);
    }

    public static void UpdateAllOccupiedSquares()
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


    public static void MakeMove(Move move)
    {
        int pieceTypeOrPromotion = move.PromotionPiece == None ? move.PieceType : move.PromotionPiece;

        // Empty the move's start square.
        RemovePiece(move.StartSquare, move.PieceType, CurrentTurn);

        // Remove any captured piece.
        if (move.CapturedPieceType != None) RemovePiece(move.TargetSquare, move.CapturedPieceType, OpponentTurn);

        // Place moved piece on the target square (unless promoting).
        AddPiece(move.TargetSquare, pieceTypeOrPromotion, CurrentTurn);


        // Remove old en passant square.
        ZobristKey ^= EnPassantFileKeys[EnPassantSquare != 0 ? GetFile(FirstSquareIndex(EnPassantSquare)) + 1 : 0];

        bool enPassant = false;
        ulong enPassantTargetBackup = 0;
        // Special pawn moves.
        if (move.PieceType == Pawn) MakePawnMove();

        // Remove previous en passant target.
        else
        {
            EnPassantTarget &= 0;
            EnPassantSquare &= 0;
        }

        // Update en passant square.
        ZobristKey ^= EnPassantFileKeys[EnPassantSquare != 0 ? GetFile(FirstSquareIndex(EnPassantSquare)) + 1 : 0];


        // Save the current castling rights
        // as 4 bits for Zobrist hashing.
        uint oldCastlingRights = FourBitCastlingRights();

        // Remove castling rights if a rook is captured.
        if (move.CapturedPieceType == Rook) RemoveRookCastlingRights(move.TargetSquare, OpponentTurn == 0 ? Mask.WhiteRank : Mask.BlackRank);

        // Remove castling rights if the king moves.
        if (move.PieceType == King) CastlingRights &= ~(CurrentTurn == 0 ? Mask.WhiteRank : Mask.BlackRank);

        // Remove castling rights if the rook moves.
        if (move.PieceType == Rook) RemoveRookCastlingRights(move.StartSquare, CurrentTurn == 0 ? Mask.WhiteRank : Mask.BlackRank);


        ulong castledRookSquare = 0;
        ulong castledRookTarget = 0;
        // If the king is castling, the rook should follow it.
        if (move.PieceType == King)
        {
            // Queenside castling
            if (move.StartSquare >> 2 == move.TargetSquare)
            {
                // Add rook on the target square.
                castledRookTarget = move.TargetSquare << 1;
                AddPiece(castledRookTarget, Rook, CurrentTurn);

                // Remove it from its start square.
                castledRookSquare = move.StartSquare >> 4;
                RemovePiece(castledRookSquare, Rook, CurrentTurn);
            }

            // Kingside castling
            else if (move.StartSquare << 2 == move.TargetSquare)
            {
                // Add rook on the target square.
                castledRookTarget = move.TargetSquare >> 1;
                AddPiece(castledRookTarget, Rook, CurrentTurn);

                // Remove it from its start square.
                castledRookSquare = move.StartSquare << 3;
                RemovePiece(castledRookSquare, Rook, CurrentTurn);
            }
        }


        uint newCastlingRights = FourBitCastlingRights();

        if (newCastlingRights != oldCastlingRights)
        {
            ZobristKey ^= CastlingRightsKeys[oldCastlingRights];
            ZobristKey ^= CastlingRightsKeys[newCastlingRights];
        }

        UpdateBoardInformation(move.StartSquare, move.TargetSquare, enPassant, enPassantTargetBackup, castledRookSquare != 0, castledRookSquare, castledRookTarget);

        CurrentTurn ^= 1;
        OpponentTurn ^= 1;
        ZobristKey ^= BlackToMoveKey;

        TT.CalculateCurrentEntryIndex();

        PositionHistory.Push(ZobristKey);


        void MakePawnMove()
        {
            // En passant.
            if (move.TargetSquare == EnPassantSquare)
            {
                enPassant = true;
                enPassantTargetBackup = EnPassantTarget;

                // Remove en passant target.
                RemovePiece(EnPassantTarget, Pawn, OpponentTurn);

                EnPassantTarget &= 0;
                EnPassantSquare &= 0;
            }

            // White double pawn push.
            else if (move.StartSquare << 16 == move.TargetSquare)
            {
                EnPassantTarget = move.TargetSquare;
                EnPassantSquare = move.StartSquare << 8;
            }

            // Black double pawn push.
            else if (move.StartSquare >> 16 == move.TargetSquare)
            {
                EnPassantTarget = move.TargetSquare;
                EnPassantSquare = move.StartSquare >> 8;
            }

            else
            {
                EnPassantTarget &= 0;
                EnPassantSquare &= 0;
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

    public static void UnmakeMove(Move move)
    {
        int pieceTypeOrPromotedPawn = move.PromotionPiece == None ? move.PieceType : Pawn;
        int pieceTypeOrPromotion = move.PromotionPiece == None ? move.PieceType : move.PromotionPiece;

        // Restore current turn before anything else.
        CurrentTurn ^= 1;
        OpponentTurn ^= 1;
        ZobristKey ^= BlackToMoveKey;

        // Reset castling rights.
        ulong oldCastlingRights = FourBitCastlingRights();

        CastlingRights = move.CastlingRightsBackup;

        ulong newCastlingRights = FourBitCastlingRights();

        if (newCastlingRights != oldCastlingRights)
        {
            ZobristKey ^= CastlingRightsKeys[oldCastlingRights];
            ZobristKey ^= CastlingRightsKeys[newCastlingRights];
        }


        // Place moved piece back on the start square.
        AddPiece(move.StartSquare, pieceTypeOrPromotedPawn, CurrentTurn);

        // Empty the move's target square.
        RemovePiece(move.TargetSquare, pieceTypeOrPromotion, CurrentTurn);

        // Restore any captured piece.
        if (move.CapturedPieceType != None) AddPiece(move.TargetSquare, move.CapturedPieceType, OpponentTurn);


        // Remove old en passant square.
        ZobristKey ^= EnPassantFileKeys[EnPassantSquare != 0 ? GetFile(FirstSquareIndex(EnPassantSquare)) + 1 : 0];

        // Reset en passant data.
        EnPassantSquare = move.EnPassantSquareBackup;
        EnPassantTarget = move.EnPassantTargetBackup;

        // Add new en passant square.
        ZobristKey ^= EnPassantFileKeys[EnPassantSquare != 0 ? GetFile(FirstSquareIndex(EnPassantSquare)) + 1 : 0];


        bool enPassant = false;
        if (move.PieceType == Pawn) UnmakePawnMove();

        ulong castledRookSquare = 0;
        ulong castledRookTarget = 0;
        // If the king is castling, the rook should follow it.
        if (move.PieceType == King)
        {
            // Queenside castling
            if (move.StartSquare >> 2 == move.TargetSquare)
            {
                // Remove rook from the target square.
                castledRookTarget = move.TargetSquare << 1;
                RemovePiece(castledRookTarget, Rook, CurrentTurn);

                // Add it to its start square.
                castledRookSquare = move.StartSquare >> 4;
                AddPiece(castledRookSquare, Rook, CurrentTurn);
            }

            // Kingside castling
            else if (move.StartSquare << 2 == move.TargetSquare)
            {
                // Remove rook from the target square.
                castledRookTarget = move.TargetSquare >> 1;
                RemovePiece(castledRookTarget, Rook, CurrentTurn);

                // Add it to its start square.
                castledRookSquare = move.StartSquare << 3;
                AddPiece(castledRookSquare, Rook, CurrentTurn);
            }
        }

        UpdateBoardInformation(move.TargetSquare, move.StartSquare, enPassant, EnPassantTarget, castledRookSquare != 0, castledRookTarget, castledRookSquare);

        TT.CalculateCurrentEntryIndex();

        PositionHistory.Pop();


        void UnmakePawnMove()
        {
            if (move.TargetSquare == EnPassantSquare)
            {
                AddPiece(EnPassantTarget, Pawn, OpponentTurn);
                enPassant = true;
            }
        }
    }

    private static void AddPiece(ulong square, int pieceType, int colorIndex)
    {
        int squareIndex = FirstSquareIndex(square);

        Squares[squareIndex] = (colorIndex << 3) | pieceType;

        // Add the piece to the specified bitboard.
        Pieces[pieceType][colorIndex] |= square;

        // Update occupied squares.
        OccupiedSquares[colorIndex] |= square;
        AllOccupiedSquares |= square;

        if (pieceType.IsSlidingPiece())
        {
            SlidingPieces[colorIndex] |= square;
            AllSlidingPieces |= square;
        }

        // Update the Zobrist key.
        ZobristKey ^= PieceKeys[pieceType, colorIndex, squareIndex];

        // Update material score.
        MaterialScore[colorIndex] += Evaluation.PieceValues[pieceType];

        // Update piece square tables score.
        PsqtScore[colorIndex] += PieceSquareTables.ReadScore(pieceType, squareIndex, colorIndex == 0);
    }

    private static void RemovePiece(ulong square, int pieceType, int colorIndex)
    {
        int squareIndex = FirstSquareIndex(square);

        Squares[squareIndex] = None;

        // Add the piece to the specified bitboard.
        Pieces[pieceType][colorIndex] &= ~square;

        // Update occupied squares.
        OccupiedSquares[colorIndex] &= ~square;
        AllOccupiedSquares &= ~square;

        if (pieceType.IsSlidingPiece())
        {
            SlidingPieces[colorIndex] &= ~square;
            AllSlidingPieces &= ~square;
        }

        // Update the Zobrist key.
        ZobristKey ^= PieceKeys[pieceType, colorIndex, squareIndex];

        // Update material score.
        MaterialScore[colorIndex] -= Evaluation.PieceValues[pieceType];

        // Update piece square tables score.
        PsqtScore[colorIndex] -= PieceSquareTables.ReadScore(pieceType, squareIndex, colorIndex == 0);
    }

    public static uint FourBitCastlingRights()
    {
        return (uint)
        ((CastlingRights >> 6) | /* White kingside castling. */
        (CastlingRights >> 1) | /* White queenside castling. */
        (CastlingRights >> 60) | /* Black kingside castling. */
        (CastlingRights >> 55)) /* Black queenside castling. */
        & 0b1111;
    }


    public static void MakeNullMove(NullMove move)
    {
        move.EnPassantSquareBackup = EnPassantSquare;
        move.EnPassantTargetBackup = EnPassantTarget;

        // Remove old en passant square.
        ZobristKey ^= EnPassantFileKeys[EnPassantSquare != 0 ? GetFile(FirstSquareIndex(EnPassantSquare)) + 1 : 0];

        EnPassantSquare = 0;
        EnPassantTarget = 0;

        // Add new en passant square.
        ZobristKey ^= EnPassantFileKeys[0];

        ZobristKey ^= BlackToMoveKey;

        CurrentTurn ^= 1;
        OpponentTurn ^= 1;

        TT.CalculateCurrentEntryIndex();
    }

    public static void UnmakeNullMove(NullMove move)
    {
        // Remove old en passant square.
        ZobristKey ^= EnPassantFileKeys[0];

        EnPassantSquare = move.EnPassantSquareBackup;
        EnPassantTarget = move.EnPassantTargetBackup;

        // Add new en passant square.
        ZobristKey ^= EnPassantFileKeys[EnPassantSquare != 0 ? GetFile(FirstSquareIndex(EnPassantSquare)) + 1 : 0];

        ZobristKey ^= BlackToMoveKey;

        CurrentTurn ^= 1;
        OpponentTurn ^= 1;

        TT.CalculateCurrentEntryIndex();
    }


    public static List<Move> GenerateAllLegalMoves(bool capturesOnly = false)
    {
        List<Move> movesList = new();
        movesList.AddRange(GenerateLegalMoves(Pawn,   MoveType.Pawn,      capturesOnly, false));
        movesList.AddRange(GenerateLegalMoves(Knight, MoveType.Normal,    capturesOnly, false));
        movesList.AddRange(GenerateLegalMoves(Bishop, MoveType.Sliding,   capturesOnly, false));
        movesList.AddRange(GenerateLegalMoves(Rook,   MoveType.Sliding,   capturesOnly, false));
        movesList.AddRange(GenerateLegalMoves(Queen,  MoveType.Sliding,   capturesOnly, false));
        movesList.AddRange(GenerateLegalMoves(King,   MoveType.Normal,    capturesOnly, false));
        return movesList;
    }

    private static List<Move> GenerateLegalMoves(int pieceType, MoveType moveType, bool capturesOnly, bool friendlyCaptures)
    {
        List<Move> movesList = new();

        ulong pieces = Pieces[pieceType][CurrentTurn];
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
                    moves = MoveData.Moves[pieceType][squareIndex, 0];
                    break;

                case MoveType.Sliding:
                    moves = MagicBitboard.GetAttacks(pieceType, squareIndex, AllOccupiedSquares);
                    break;

                case MoveType.Pawn:

                    ulong opponentBlockers = OccupiedSquares[OpponentTurn];
                    // Pawns can only move forward if there is not a blocking piece.
                    moves |= MoveData.Moves[pieceType][squareIndex, CurrentTurn] & ~AllOccupiedSquares;
                    // If the pawn was able to move forward, it may be eligible for a double push.
                    if (moves != 0) moves |= MoveData.Moves[pieceType][squareIndex, CurrentTurn + 2] & ~opponentBlockers;
                    // Pawns can only capture if there is an enemy piece.
                    moves |= MoveData.Moves[pieceType][squareIndex, CurrentTurn + 4] & (capturesOnly ? ulong.MaxValue : (opponentBlockers | EnPassantSquare));

                    break;
            }

            if (capturesOnly) moves &= AllOccupiedSquares;

            // Remove friendly blockers (all captures were included, but only enemy pieces can actually be captured).
            if (!friendlyCaptures) moves &= ~OccupiedSquares[CurrentTurn];

            while (moves != 0)
            {
                int targetSquareIndex = FirstSquareIndex(moves);
                moves &= moves - 1;

                ulong targetSquare = 1UL << targetSquareIndex;

                if (pieceType == Pawn && (targetSquare & Mask.PromotionRanks) != 0)
                {
                    Move move = new(pieceType, square, targetSquare, PieceType(targetSquareIndex), Queen);

                    // If one promotion is legal, all of them are.
                    if (IsLegal(move))
                    {
                        movesList.Add(move);
                        movesList.Add(new(pieceType, square, targetSquare, PieceType(targetSquareIndex), Knight));
                        movesList.Add(new(pieceType, square, targetSquare, PieceType(targetSquareIndex), Bishop));
                        movesList.Add(new(pieceType, square, targetSquare, PieceType(targetSquareIndex), Rook));
                    }
                }

                else
                {
                    Move move = new(pieceType, square, targetSquare, PieceType(targetSquareIndex));
                    if (IsLegal(move)) movesList.Add(move);
                }
            }
        }

        return movesList;
    }

    public static bool IsLegal(Move move)
    {
        int startSquareIndex = FirstSquareIndex(move.StartSquare);
        int targetSquareIndex = FirstSquareIndex(move.TargetSquare);

        int pieceType = PieceType(startSquareIndex);

        bool inCheck = IsKingInCheck[CurrentTurn];
        bool isKing = pieceType == King;

        if (isKing)
        {
            // The king cannot move to an attacked square.
            if (AttackersTo(targetSquareIndex, CurrentTurn, OpponentTurn, AllOccupiedSquares) != 0) return false;

            // Queenside castling
            if (move.StartSquare >> 2 == move.TargetSquare)
            {
                // Castling is not allowed while in check.
                if (inCheck) return false;

                // The king cannot castle through an attack.
                if (AttackersTo(startSquareIndex - 1, CurrentTurn, OpponentTurn, AllOccupiedSquares) != 0) return false;
            }

            // Kingside castling
            if (move.StartSquare << 2 == move.TargetSquare)
            {
                // Castling is not allowed while in check.
                if (inCheck) return false;

                // The king cannot castle through an attack.
                if (AttackersTo(startSquareIndex + 1, CurrentTurn, OpponentTurn, AllOccupiedSquares) != 0) return false;
            }

            if (inCheck)
            {
                int attackerIndex = -1;
                int secondAttackerIndex = -1;

                ulong attacks = 0;
                if ((attacks = AttackersTo(KingPosition[CurrentTurn], CurrentTurn, OpponentTurn, AllOccupiedSquares)) != 0)
                {
                    attackerIndex = FirstSquareIndex(attacks);
                    secondAttackerIndex = LastSquareIndex(attacks);

                    if (secondAttackerIndex == attackerIndex) secondAttackerIndex = -1;
                }

                if (attackerIndex != -1)
                {
                    int attackerType = GetRank(attackerIndex) == GetRank(startSquareIndex) || GetFile(attackerIndex) == GetFile(startSquareIndex) ? Rook : Bishop;
                    if (IsSlidingPiece(1UL << attackerIndex) && attackerType != None)
                    {
                        if (MoveData.SpecificMasks[attackerType][attackerIndex, targetSquareIndex] != 0) return false;
                    }

                    if (secondAttackerIndex != -1)
                    {
                        int secondAttackerType = GetRank(secondAttackerIndex) == GetRank(startSquareIndex) || GetFile(secondAttackerIndex) == GetFile(startSquareIndex) ? Rook : Bishop;
                        if (IsSlidingPiece(1UL << secondAttackerIndex) && secondAttackerType != None)
                        {
                            if (MoveData.SpecificMasks[secondAttackerType][secondAttackerIndex, targetSquareIndex] != 0) return false;
                        }
                    }
                }
            }
        }

        else
        {
            if (inCheck)
            {
                int attackerIndex = -1;
                int secondAttackerIndex = -1;

                ulong attacks = AttackersTo(KingPosition[CurrentTurn], CurrentTurn, OpponentTurn, AllOccupiedSquares);
                if (attacks != 0)
                {
                    attackerIndex = FirstSquareIndex(attacks);
                    secondAttackerIndex = LastSquareIndex(attacks);

                    if (secondAttackerIndex == attackerIndex) secondAttackerIndex = -1;
                }

                // In case of a double attack on the king, the only option is to move the king to safety.
                if (secondAttackerIndex != -1) return false;

                int attackerType = PieceType(attackerIndex, slidingPiecesOnly: true);
                if (attackerType != None)
                {
                    // A check must be stoppped by blocking the attack.
                    if ((move.TargetSquare & MoveData.Masks[attackerIndex, KingPosition[CurrentTurn]]) == 0) return false;
                }

                // In case of a non-sliding attack, the attacker must be captured.
                else
                {
                    // The move is illegal if we are not capturing the attacker (which may also be captured using en passant).
                    if (targetSquareIndex != attackerIndex &&
                        !(pieceType == Pawn && move.TargetSquare == EnPassantSquare && (1UL << attackerIndex) == EnPassantTarget)) return false;
                }
            }

            ulong slidingAttackers = SlidingAttackersTo(startSquareIndex, CurrentTurn, OpponentTurn,
                AllOccupiedSquares & ~(pieceType == Pawn && move.TargetSquare == EnPassantSquare ? EnPassantTarget : 0) /* In case the move is en passant, remove the en passant target from the occupied squares map to calculate pins correctly. */);

            while (slidingAttackers != 0)
            {
                int attackerIndex = FirstSquareIndex(slidingAttackers);
                int attackerType = GetRank(attackerIndex) == GetRank(startSquareIndex) || GetFile(attackerIndex) == GetFile(startSquareIndex) ? Rook : Bishop;


                ulong attackRayToKing = MoveData.SpecificMasks[attackerType][attackerIndex, KingPosition[CurrentTurn]];

                if (attackRayToKing != 0 &&
                    (move.StartSquare & attackRayToKing) != 0 /* The moving piece is in the ray of attack. */ &&
                    (move.TargetSquare & attackRayToKing) == 0 /* The target is not on the ray. If the piece is pinned this move is illegal. */)
                {
                    if (PieceCount(AllOccupiedSquares & attackRayToKing) == 3) return false;

                    if (GetRank(attackerIndex) == GetRank(startSquareIndex) &&
                        pieceType == Pawn && move.TargetSquare == EnPassantSquare &&
                        PieceCount(AllOccupiedSquares & attackRayToKing) == 4) return false;
                }

                slidingAttackers &= slidingAttackers - 1;
            }
        }

        return true;
    }


    /// <summary>
    /// Update the list representation of the board
    /// </summary>
    public static void UpdateSquares()
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
    public static ulong AttackersTo(int squareIndex, int colorIndex, int opponentColorIndex, ulong occupiedSquares)
    {
        return
            (MoveData.Moves[Pawn][squareIndex, colorIndex + 4]                & Pawns[opponentColorIndex]) |
            (MoveData.Moves[Knight][squareIndex, 0]                           & Knights[opponentColorIndex]) |
            (MoveData.Moves[King][squareIndex, 0]                             & Kings[opponentColorIndex]) |
            (MagicBitboard.GetAttacks(Rook, squareIndex, occupiedSquares)     & (Rooks[opponentColorIndex] | Queens[opponentColorIndex])) |
            (MagicBitboard.GetAttacks(Bishop, squareIndex, occupiedSquares)   & (Bishops[opponentColorIndex] | Queens[opponentColorIndex]));
    }

    public static ulong PawnAttackersTo(int squareIndex, int colorIndex, int opponentColorIndex)
    {
        return MoveData.Moves[Pawn][squareIndex, colorIndex + 4] & Pawns[opponentColorIndex];
    }

    public static ulong SlidingAttackersTo(int squareIndex, int colorIndex, int opponentColorIndex, ulong occupiedSquares)
    {
        return
            (MagicBitboard.GetAttacks(Rook, squareIndex, occupiedSquares) & (Rooks[opponentColorIndex] | Queens[opponentColorIndex])) |
            (MagicBitboard.GetAttacks(Bishop, squareIndex, occupiedSquares) & (Bishops[opponentColorIndex] | Queens[opponentColorIndex]));
    }

    /// <summary>Bitboard of all the squares attacked by the specified piece, including enemy and friendly captures.</summary>
    public static ulong AttacksFrom(int squareIndex, int pieceType, ulong occupiedSquares)
    {
        return
            pieceType == Knight ? MoveData.Moves[Knight][squareIndex, 0] :
            pieceType == Bishop ? MagicBitboard.GetAttacks(Bishop, squareIndex, occupiedSquares) :
            pieceType == Rook ? MagicBitboard.GetAttacks(Rook, squareIndex, occupiedSquares) :
            pieceType == Queen ? MagicBitboard.GetAttacks(Rook, squareIndex, occupiedSquares) | MagicBitboard.GetAttacks(Bishop, squareIndex, occupiedSquares) : 0;
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

    public static int PieceType(int squareIndex, bool slidingPiecesOnly = false)
    {
        int pieceType = Squares[squareIndex].PieceType();
        return
            !slidingPiecesOnly ? pieceType :
            (pieceType.IsSlidingPiece() ? pieceType : None);
    }

    public static int PieceColor(int squareIndex)
    {
        return Squares[squareIndex].PieceColor();
    }

    public static int PieceColorIndex(int squareIndex)
    {
        return Squares[squareIndex].PieceColor() >> 3;
    }

    public static bool IsSlidingPiece(ulong square) => (AllSlidingPieces & square) != 0;

    public static bool RemovePins(ulong square, ulong mask, ref ulong moves)
    {
        if (PieceCount(AllOccupiedSquares & mask) == 3)
        {
            moves &= mask;

            return true;
        }

        if ((Pawns[CurrentTurn] & square) != 0 && (moves & EnPassantSquare) != 0)
        {
            if (PieceCount(AllOccupiedSquares & mask) == 4)
            {
                moves &= ~EnPassantSquare;

                return true;
            }
        }

        return false;
    }

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
}


public struct Mask
{
    public const ulong InitialCastlingRights = 0x4400000000000044;
    public const ulong WhiteInitialCastlingRights = 0x44;
    public const ulong BlackInitialCastlingRights = 0x4400000000000000;
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
    public const ulong SeventhRank = 0xff00000000ff00;
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
