using Utilities;
using static Piece;

public class Cuckoo
{
    public static (ulong Key, Move Move)[] Moves;

    public static void Init()
    {
        Moves = new (ulong Key, Move Move)[8192];

        for (int pieceColor = 0; pieceColor <= 1; pieceColor++) 
        {
            // Note: pawns can be skipped as their moves are always irreversible.
            for (int pieceType = Pawn; pieceType <= King; pieceType++)
            {
                for (int a = 0; a < 64; a++)
                {
                    for (int b = a + 1; b < 64; b++)
                    {
                        if (Move.IsValidAndReversible(pieceType, a, b))
                        {
                            Move move = new Move(a, b);
                            ulong moveKey = 
                                Zobrist.PieceKeys[pieceType, pieceColor, a] ^
                                Zobrist.PieceKeys[pieceType, pieceColor, b] ^
                                Zobrist.BlackToMoveKey;

                            int i = Hash1(moveKey);
                            while (true)
                            {
                                // Swap the cuckoo move at the current index with the current move.
                                (Moves[i].Move, move) = (move, Moves[i].Move);
                                (Moves[i].Key, moveKey) = (moveKey, Moves[i].Key);

                                // Stop when an empty slot is reached.
                                if (move.IsNullMove()) break;

                                // Push victim to its alternative slot.
                                i = (i == Hash1(moveKey)) ? Hash2(moveKey) : Hash1(moveKey);
                            }
                        }
                    }
                }
            } 
        }
    }

    public static int Hash1(ulong key) => (int)key & 0x1fff;

    public static int Hash2(ulong key) => (int)(key >> 16) & 0x1fff;
}