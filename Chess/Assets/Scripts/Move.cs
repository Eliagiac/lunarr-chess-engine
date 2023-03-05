using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Move
{
    public int PieceType;

    public ulong StartSquare;
    public ulong TargetSquare;

    public int CapturedPieceType;

    public int PromotionPiece;

    public ulong CastlingRightsBackup;

    public ulong EnPassantSquareBackup;
    public ulong EnPassantTargetBackup;

    public Move(int pieceType, ulong startSquare, ulong targetSquare, int capturedPieceType = Piece.None, int promotionPiece = Piece.None, ulong? currentCastlingRights = null, ulong? currentEnPassantSquare = null, ulong? currentEnPassantTarget = null)
    {
        PieceType = pieceType;
        StartSquare = startSquare;
        TargetSquare = targetSquare;
        CapturedPieceType = capturedPieceType;
        PromotionPiece = promotionPiece;
        CastlingRightsBackup = currentCastlingRights ?? Board.CastlingRights;
        EnPassantSquareBackup = currentEnPassantSquare ?? Board.EnPassantSquare;
        EnPassantTargetBackup = currentEnPassantTarget ?? Board.EnPassantTarget;
    }


    public bool Equals(Move move)
    {
        return
            move != null &&
            StartSquare == move.StartSquare &&
            TargetSquare == move.TargetSquare &&
            PromotionPiece == move.PromotionPiece;
    }

    public override string ToString()
    {
        return $"{(Square)BitboardUtility.FirstSquareIndex(StartSquare)}{(Square)BitboardUtility.FirstSquareIndex(TargetSquare)}";
    }

    private string PieceTypeString()
    {
        switch (PieceType)
        {
            case Piece.King: return "King";
            case Piece.Pawn: return "Pawn";
            case Piece.Knight: return "Knight";
            case Piece.Bishop: return "Bishop";
            case Piece.Rook: return "Rook";
            case Piece.Queen: return "Queen";
            default: return "";
        }
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


    // Used to load the opening book
    public Move(ushort moveValue)
    {
        CastlingRightsBackup = Board.CastlingRights;
        EnPassantSquareBackup = Board.EnPassantSquare;
        EnPassantTargetBackup = Board.EnPassantTarget;

        StartSquare = 1UL << (moveValue & 0b0000000000111111);
        TargetSquare = 1UL << ((moveValue & 0b0000111111000000) >> 6);

        PieceType = Board.PieceType(BitboardUtility.FirstSquareIndex(StartSquare));

        if ((moveValue >> 12) == 3) PromotionPiece = Piece.Queen;
        if ((moveValue >> 12) == 4) PromotionPiece = Piece.Knight;
        if ((moveValue >> 12) == 5) PromotionPiece = Piece.Rook;
        if ((moveValue >> 12) == 6) PromotionPiece = Piece.Bishop;
    }

    public ushort Value 
    {
        get
        {
            return
                (ushort)(BitboardUtility.FirstSquareIndex(StartSquare) |
                BitboardUtility.FirstSquareIndex(TargetSquare) << 6 |
                (PromotionPiece == Piece.Queen ? 3 :
                PromotionPiece == Piece.Knight ? 4 :
                PromotionPiece == Piece.Rook ? 5 :
                PromotionPiece == Piece.Bishop ? 6 : 0) << 12);
        }
    }

    //public Move Copy()
    //{
    //    return new(PieceType, StartSquare, TargetSquare, CapturedPieceType, PromotionPiece, CastledRookSquare, CastledRookTarget, CastlingRightsBackup, EnPassantSquareBackup, EnPassantTargetBackup);
    //}
}

public class NullMove
{
    public ulong EnPassantSquareBackup;
    public ulong EnPassantTargetBackup;
}
