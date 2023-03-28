public struct Piece
{
    public const int None = 0;
    public const int Pawn = 1;
    public const int Knight = 2;
    public const int Bishop = 3;
    public const int Rook = 4;
    public const int Queen = 5;
    public const int King = 6;

    public const int White = 0b0000;
    public const int Black = 0b1000;
}


public static class IntExtensions
{
    public static int PieceType(this int piece) => piece & 0b111;

    public static int PieceColor(this int piece) => piece & 0b1000;
}

public static class PieceExtensions
{
    public static bool IsSlidingPiece(this int piece) => piece == Piece.Bishop || piece == Piece.Rook || piece == Piece.Queen;
}
