using Utilities;
using static Utilities.Bitboard;

public class RepetitionTable
{
    private const int MaxPositionCount = 256;

    public ulong[] PositionKeyHistory;
    private int _currentPositionIndex;

    public RepetitionTable()
    {
        PositionKeyHistory = new ulong[MaxPositionCount];
    }

    public RepetitionTable(RepetitionTable other)
    {
        PositionKeyHistory = (ulong[])other.PositionKeyHistory.Clone();
        _currentPositionIndex = other._currentPositionIndex;
    }

    public void PopulateWithCurrentHistory(Board board)
    {
        _currentPositionIndex = board.GameStateHistory.Count - 1;

        ulong[] gameStateHistoryArray = board.GameStateHistory
            .Select(p => p.ZobristKey).Reverse().ToArray();

        for (int i = 0; i <= _currentPositionIndex; i++)
            PositionKeyHistory[i] = gameStateHistoryArray[i];
    }

    public void Push(ulong key)
    {
        _currentPositionIndex++;
        PositionKeyHistory[_currentPositionIndex] = key;
    }

    public void Pop()
    {
        _currentPositionIndex--;
    }

    public bool Contains(Board board)
    {
        if (board.FiftyMovePlyCount < 4) return false;

        for (int i = _currentPositionIndex - 4; i >= _currentPositionIndex - board.FiftyMovePlyCount; i -= 2)
        {
            if (PositionKeyHistory[i] == board.ZobristKey) return true;
        }

        return false;
    }

    /// <summary>
    /// Test whether the current position has a move that would cause a draw by repetition.
    /// </summary>
    /// <remarks>
    /// This is done by checking if there is a legal move in any past position 
    /// (up to the last irreversible move) that leads to the current position. <br />
    /// For more details, see this paper by Marcel van Kervinck: http://web.archive.org/web/20201107002606/https://marcelk.net/2013-04-06/paper/upcoming-rep-v2.pdf.  <br />
    /// The functions returns true even if the position would only have occured twice if the move was played.
    /// </remarks>
    public bool HasUpcomingRepetition(Board board)
    {
        if (board.FiftyMovePlyCount < 3) return false;

        ulong currentPositionKey = PositionKeyHistory[_currentPositionIndex];

        for (int ply = 3; ply <= board.FiftyMovePlyCount; ply++)
        {
            ulong previousPositionKey = PositionKeyHistory[_currentPositionIndex - ply];

            // The Zobrist key of a move from the previous position to the current.
            ulong moveDifferenceKey = currentPositionKey ^ previousPositionKey;

            int i = Cuckoo.Hash1(moveDifferenceKey);

            if (Cuckoo.Moves[i].Key != moveDifferenceKey)
                i = Cuckoo.Hash2(moveDifferenceKey);

            if (Cuckoo.Moves[i].Key == moveDifferenceKey)
            {
                Move move = Cuckoo.Moves[i].Move;

                int startSquareIndex = move.StartSquareIndex;
                int targetSquareIndex = move.TargetSquareIndex;

                // For the move to be legal there must not be pieces between the start and
                // target square, other than the moving piece and any captured piece.
                if (PieceCount(PrecomputedMoveData.Line[startSquareIndex, targetSquareIndex] & board.AllOccupiedSquares) <= 2)
                    return true;
            }
        }

        return false;
    }

    public override bool Equals(object? obj)
    {
        RepetitionTable other = obj as RepetitionTable;
        if (other == null) return false;

        return
            _currentPositionIndex == other._currentPositionIndex &&
            Enumerable.SequenceEqual(PositionKeyHistory, other.PositionKeyHistory);
    }
}
