using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// A snapshot of the current position containg its zobrist key, 
/// castling rights and possible en passant target if any.
/// Also stores the type of the piece captured on the last turn, 
/// since this is not stored in the move itself.
/// </summary>
public struct PositionData
{
    public readonly ulong ZobristKey;
    public readonly int CapturedPieceType;
    public readonly ulong CastlingRights;
    public readonly ulong EnPassantSquare;
    public readonly ulong EnPassantTarget;

    public PositionData(ulong zobristKey, int capturedPieceType, ulong castlingRights, ulong enPassantSquare, ulong enPassantTarget)
    {
        ZobristKey = zobristKey;
        CapturedPieceType = capturedPieceType;
        CastlingRights = castlingRights;
        EnPassantSquare = enPassantSquare;
        EnPassantTarget = enPassantTarget;
    }
}
