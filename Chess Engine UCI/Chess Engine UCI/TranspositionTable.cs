using System.Runtime.InteropServices;
using static Utilities.Bitboard;
using static Engine;

/// <summary>The <see cref="TT"/> (Transposition Table) class stores info on positions that have already been reached by the search function. 
/// Information about these entries such as their evaluation can quickly be looked up to avoid calculating it again.</summary>
public class TT
{
    /// <summary>A table containing information about specific positions, <br />
    /// indexed based on the <see cref="Board.ZobristKey"/> of the position.</summary>
    public static TTEntry[] Entries = new TTEntry[_tableSize];

    /// <summary>The index of the current position in the <see cref="Entries"/> array based on the <see cref="Board.ZobristKey"/>.</summary>
    /// <remarks>Must be updated any time the position changes using <see cref="CalculateCurrentEntryIndex"/>.</remarks>
    public static ulong CurrentEntryIndex;


    /// <summary>If the transposition table is disabled, 
    /// <see cref="StoreEvaluation"/> will return early.</summary>
    public static bool IsEnabled = true;


    private static readonly ulong EntriesInOneMegabyte = (1024 * 1024) / (ulong)Marshal.SizeOf<TTEntry>();

    /// <summary>The size of the <see cref="Entries"/> array.</summary>
    /// <remarks>The default size is 8MB.</remarks>
    private static ulong _tableSize = 8 * EntriesInOneMegabyte;


    /// <summary>Generate a new transposition table of the specified size (in megabytes).</summary>
    /// <param name="sizeInMegabytes">The size of the <see cref="Entries"/> array in megabytes.</param>
    public static void Resize(int sizeInMegabytes)
    {
        _tableSize = (ulong)sizeInMegabytes * EntriesInOneMegabyte;

        Entries = new TTEntry[_tableSize];
    }

    /// <summary>Reset the <see cref="Entries"/> array.</summary>
    public static void Clear() => 
        Entries = new TTEntry[_tableSize];

    /// <summary>Update the <see cref="CurrentEntryIndex"/> based on the position on the board.</summary>
    public static void CalculateCurrentEntryIndex() => 
        CurrentEntryIndex = MultiplyHigh64Bits(Board.ZobristKey, _tableSize);


    /// <summary>The entry at the <see cref="CurrentEntryIndex"/>.</summary>
    private static TTEntry CurrentEntry
    {
        get => Entries[CurrentEntryIndex];
        set => Entries[CurrentEntryIndex] = value;
    }

    public static Move GetStoredMove() => CurrentEntry.Line?.Move;

    public static Line GetStoredLine() => CurrentEntry.Line;

	public static TTEntry GetStoredEntry(out bool ttHit)
	{
		if (CurrentEntry.Key == Board.ZobristKey) ttHit = true;
		else ttHit = false;

		return CurrentEntry;
    }

    /// <summary>Get the stored evaluation of the current position.</summary>
    /// <remarks>This function ensures returned values are either exact or in line with the current boundaries. <br />
    /// The returned values may be outside of the bounds (see https://www.chessprogramming.org/Fail-Soft).</remarks>
    public static int GetStoredEvaluation(int ply, int alpha, int beta)
    {
        int score = CurrentEntry.Evaluation;

        // Stored checkmate scores are always the max value. Correct them to account for the distance from mate.
        if (IsMateWinScore(score)) score = MateIn(ply);
        else if (IsMateLossScore(score)) score = MatedIn(ply);


        if (CurrentEntry.EvaluationType == EvaluationType.Exact)
            return score;

        if (CurrentEntry.EvaluationType == EvaluationType.LowerBound && score >= beta)
            return score;

        if (CurrentEntry.EvaluationType == EvaluationType.UpperBound && score <= alpha)
            return score;


        // Lookup failed.
        return Null;
    }

    public static void ClearCurrentEntry() =>
        CurrentEntry = new();

	public static void StoreEvaluation(int depth, int ply, int evaluation, EvaluationType evaluationType, Line line, int staticEvaluation) 
	{
		// Don't store incorrect values.
		if (!IsEnabled || WasSearchAborted || evaluation == Null) return;

		if (depth >= CurrentEntry.Depth) 
            CurrentEntry = new TTEntry(Board.ZobristKey, CorrectMateScoreForStorage(evaluation, ply), depth, staticEvaluation, evaluationType, line);
	}


	public static int CorrectMateScoreForStorage(int score, int ply) 
	{
        if (IsMateWinScore(score)) return Checkmate;
        if (IsMateLossScore(score)) return -Checkmate;

        return score;
	}
}

public struct TTEntry
{
    /// <summary>The zobrist key of the position of this entry, used for indexing.</summary>
    public readonly ulong Key;

    /// <summary>The score previously returned by a search on this position.</summary>
    /// <remarks>It's called evaluation and not score as it might become outdated.</remarks>
    public readonly int Evaluation;

    /// <summary>The depth of the search that generated this entry.</summary>
    public readonly int Depth;

    /// <summary>The static evaluation of this position.</summary>
    /// <remarks>Stored to avoid recomputing it if the position is encountered again.</remarks>
    public readonly int StaticEvaluation;

    /// <summary>Whether this is the exact evaluation of the position or an upper or lower bound.</summary>
    public readonly EvaluationType EvaluationType;

    /// <summary>The best move previously found in this position, and the best play sequence that follows it.</summary>
    public readonly Line Line;

    /// <summary>Create a new <see cref="TTEntry"/> for the given position.</summary>
    public TTEntry(ulong key, int evaluation, int depth, int staticEvaluation, EvaluationType evaluationType, Line line)
    {
        Key = key;
        Evaluation = evaluation;
        Depth = depth;
        StaticEvaluation = staticEvaluation;
        EvaluationType = evaluationType;
        Line = line;
    }
}

/// <summary>
/// The score returned by the search may not be the exact evaluation of the position, 
/// because of <see href="https://www.chessprogramming.org/Alpha-Beta">Alpha-Beta pruning</see>.
/// </summary>
public enum EvaluationType
{
    /// <summary>The node is a PV-Node.</summary>
    /// <remarks>The score is the exact evaluation of the position.</remarks>
    Exact = 3,

    /// <summary>The node is a Cut-Node.</summary>
    /// <remarks>
    /// A beta-cutoff caused some moves to be skipped,
    /// so the score is a lower bound of the evaluation
    /// (the exact evaluation may be higher).
    /// </remarks>
    LowerBound = 2,

    /// <summary>The node is an All-Node.</summary>
    /// <remarks>
    /// No moves exceeded alpha, so the score 
    /// is an upper bound of the evaluation
    /// (the exact evaluation may be lower).
    /// </remarks>
    UpperBound = 1
}
