using static Utilities.Bitboard;

public class Move
{
    public int PieceType;

    public ulong StartSquare;
    public ulong TargetSquare;

    public int StartSquareIndex;
    public int TargetSquareIndex;

    public int CapturedPieceType;

    public int PromotionPiece;

    public ulong CastlingRightsBackup;

    public ulong EnPassantSquareBackup;
    public ulong EnPassantTargetBackup;

    public Move(Board board, int pieceType, ulong startSquare, ulong targetSquare, int startSquareIndex, int targetSquareIndex, int capturedPieceType = Piece.None, int promotionPiece = Piece.None, ulong? currentCastlingRights = null, ulong? currentEnPassantSquare = null, ulong? currentEnPassantTarget = null)
    {
        PieceType = pieceType;
        StartSquare = startSquare;
        TargetSquare = targetSquare;
        StartSquareIndex = startSquareIndex;
        TargetSquareIndex = targetSquareIndex;
        CapturedPieceType = capturedPieceType;
        PromotionPiece = promotionPiece;
        CastlingRightsBackup = currentCastlingRights ?? board.CastlingRights;
        EnPassantSquareBackup = currentEnPassantSquare ?? board.EnPassantSquare;
        EnPassantTargetBackup = currentEnPassantTarget ?? board.EnPassantTarget;
    }

    // Used for move generation.
    public enum MoveType
    {
        None,
        Normal,
        Sliding,
        Pawn
    }

    public bool Equals(Move other)
    {
        return
            other != null &&
            PieceType == other.PieceType &&
            StartSquare == other.StartSquare &&
            TargetSquare == other.TargetSquare &&
            PromotionPiece == other.PromotionPiece;
    }

    public override string ToString()
    {
        return $"{(Square)FirstSquareIndex(StartSquare)}{(Square)FirstSquareIndex(TargetSquare)}{new[] { "", "p", "n", "b", "r", "q", "k" }[PromotionPiece]}";
    }

    public Move(Move other)
    {
        PieceType = other.PieceType;
        StartSquare = other.StartSquare;
        TargetSquare = other.TargetSquare;
        CapturedPieceType = other.CapturedPieceType;
        PromotionPiece = other.PromotionPiece;
        CastlingRightsBackup = other.CastlingRightsBackup;
        EnPassantSquareBackup = other.EnPassantSquareBackup;
        EnPassantTargetBackup = other.EnPassantTargetBackup;
    }
}

public class NullMove
{
    public ulong EnPassantSquareBackup;
    public ulong EnPassantTargetBackup;
}
