/// <summary>
/// A snapshot of the current position containg its zobrist key, 
/// castling rights and possible en passant target if any.
/// Also stores the type of the piece captured on the last turn, 
/// since this is not stored in the move itself.
/// </summary>
public struct GameState
{
    public readonly ulong ZobristKey;
    public readonly int CapturedPieceType;
    public readonly ulong CastlingRights;
    public readonly ulong EnPassantSquare;
    public readonly ulong EnPassantTarget;
    public readonly int PlyCountReversible;
    public readonly int FiftyMovePlyCount;

    public GameState(ulong zobristKey, int capturedPieceType, ulong castlingRights, ulong enPassantSquare, ulong enPassantTarget, int plyCountReversible, int fiftyMovePlyCount)
    {
        ZobristKey = zobristKey;
        CapturedPieceType = capturedPieceType;
        CastlingRights = castlingRights;
        EnPassantSquare = enPassantSquare;
        EnPassantTarget = enPassantTarget;
        PlyCountReversible = plyCountReversible;
        FiftyMovePlyCount = fiftyMovePlyCount;
    }
}
