using static Utilities.Bitboard;
using static Piece;

public struct Move
{
    /// <summary>
    /// The 16-bit value containing the move's start square, target square and flag.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>The first 4 bits are used to store a flag for special moves such as castling, promotions or en passant. </item>
    /// <item>The next 6 bits are the move's target square index (0-63). </item>
    /// <item>The last 6 bits are the move's start square index (0-63). </item>
    /// </list>
    /// A move value of 0 represent a null/invalid move. This is because 
    /// no real move can have the same start square and target square.
    /// </remarks>
    public readonly ushort MoveValue;


    public const ushort StartSquareMask = 0b0000000000111111;
    public const ushort TargetSquareMask = 0b0000111111000000;
    public const ushort FlagMask = 0b1111000000000000;
    
    public const ushort EnPassantCaptureFlag = 1;
    public const ushort CastlingFlag = 2;
    public const ushort PawnDoublePushFlag = 3;
    public const ushort PromotionToQueenFlag = 4;
    public const ushort PromotionToKnightFlag = 5;
    public const ushort PromotionToBishopFlag = 6;
    public const ushort PromotionToRookFlag = 7;


    public int StartSquareIndex => MoveValue & StartSquareMask;
    public int TargetSquareIndex => (MoveValue & TargetSquareMask) >> 6;

    public ulong StartSquare => 1UL << StartSquareIndex;
    public ulong TargetSquare => 1UL << TargetSquareIndex;

    // Note: the intersection is unnecessary.
    public int Flag => (MoveValue & FlagMask) >> 12;

    public int PromotionPieceType {
        get 
        { 
            switch (Flag)
            {
                case PromotionToQueenFlag:
                    return Queen;
                case PromotionToKnightFlag:
                    return Knight;
                case PromotionToBishopFlag:
                    return Bishop;
                case PromotionToRookFlag:
                    return Rook;
                default:
                    return None;
            }
        }
    }


    public Move(int startSquareIndex, int targetSquareIndex)
    {
        MoveValue = (ushort)(targetSquareIndex << 6 | startSquareIndex);
    }

    public Move(int startSquareIndex, int targetSquareIndex, int flag)
    {
        MoveValue = (ushort)(flag << 12 | startSquareIndex | targetSquareIndex << 6);
    }

    public Move(Move other) => MoveValue = other.MoveValue;

    // Used to load the opening book
    public Move(ushort moveValue) => MoveValue = moveValue;


    // Note: might be more readable to use (Move)move!
    // Note: could consider using an extension method instead.
    public static Move NotNull(Move? move) => move.Value;


    public bool Equals(Move? other) => MoveValue == (other?.MoveValue ?? 0);

    public override string ToString() =>
        $"{(Square)StartSquareIndex}{(Square)TargetSquareIndex}{new[] { "", "p", "n", "b", "r", "q", "k" }[PromotionPieceType]}";

    public bool IsNullMove() => MoveValue == 0;

    public bool IsCapture()
    {
        if (IsNullMove()) return false;

        bool isEnPassant = Flag == EnPassantCaptureFlag;

        int capturedPieceType = isEnPassant ? Pawn : Board.PieceType(TargetSquareIndex);

        return capturedPieceType != None || isEnPassant;
    }
}

public class NullMove
{
    public ulong EnPassantSquareBackup;
    public ulong EnPassantTargetBackup;
}

// Used for move generation.
public enum MoveType
{
    None,
    Normal,
    Sliding,
    Pawn
}
