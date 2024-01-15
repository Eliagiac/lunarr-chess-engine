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

    public override bool Equals(object? obj)
    {
        RepetitionTable other = obj as RepetitionTable;
        if (other == null) return false;

        return
            _currentPositionIndex == other._currentPositionIndex &&
            Enumerable.SequenceEqual(PositionKeyHistory, other.PositionKeyHistory);
    }
}
