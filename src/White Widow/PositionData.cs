using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// A snapshot of the current position containg its zobrist key, 
/// castling rights and possible en passant target in any.
/// </summary>
public struct PositionData
{
    public readonly ulong ZobristKey;
    public readonly ulong CastlingRights;
    public readonly ulong EnPassantSquare;
    public readonly ulong EnPassantTarget;

    public PositionData(ulong zobristKey, ulong castlingRights, ulong enPassantSquare, ulong enPassantTarget)
    {
        ZobristKey = zobristKey;
        CastlingRights = castlingRights;
        EnPassantSquare = enPassantSquare;
        EnPassantTarget = enPassantTarget;
    }
}
