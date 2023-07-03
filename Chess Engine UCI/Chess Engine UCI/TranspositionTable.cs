using System.Runtime.InteropServices;

using static Utilities.Bitboard;
using static Engine;

public class TranspositionTable 
{
	/// <summary>The position is not in the transposition table.</summary>
	public const int LookupFailed = 32003;


	/// <summary>
	/// A table containing information about specific positions, <br />
	/// indexed based on the <see cref="Board.ZobristKey"/> of the position.
	/// </summary>
	public static Entry[] Entries;

	/// <summary>
	/// The index of the current position in the <see cref="Entries"/> array based on the <see cref="Board.ZobristKey"/>.
	/// </summary>
	public static ulong CurrentEntryIndex;


    /// <summary>
    /// If the transposition table is disabled, <see cref="LookupEvaluation"/> 
	/// and <see cref="StoreEvaluation"/> will return early.
    /// </summary>
    public static bool IsEnabled = true;


    /// <summary>
    /// The score returned by the search may not be the exact evaluation 
    /// of the position, because of <see href="https://www.chessprogramming.org/Alpha-Beta">Alpha-Beta pruning</see>.
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


	/// <summary>The entry at the <see cref="CurrentEntryIndex"/>.</summary>
	private static Entry CurrentEntry
	{
        get => Entries[CurrentEntryIndex];
		set => Entries[CurrentEntryIndex] = value;
    }

	/// <summary>The size of the <see cref="Entries"/> array.</summary>
    public static ulong Size { get; private set; }


    /// <summary>Generate a new transposition table of a specific size (in megabytes).</summary>
    /// <param name="size">The size of the <see cref="Entries"/> array in megabytes.</param>
    public static void ResizeTranspositionTable(ulong size)
    {
        const int megabyte = 1024 * 1024;

        Size = (size * megabyte) / (ulong)Entry.GetSize();

        Entries = new Entry[Size];
    }

    /// <summary>
    /// Reset the <see cref="Entries"/> array.
    /// </summary>
    public static void Clear() 
	{
		for (int i = 0; i < Entries.Length; i++) 
		{
			Entries[i] = new();
		}
	}

	public static void CalculateCurrentEntryIndex() => CurrentEntryIndex = MultiplyHigh64Bits(Board.ZobristKey, Size);


	public static Move GetStoredMove() => CurrentEntry.Line?.Move;

    public static Line GetStoredLine() => CurrentEntry.Line;

	public static Entry GetStoredEntry(out bool ttHit)
	{
		if (CurrentEntry.Key == Board.ZobristKey) ttHit = true;
		else ttHit = false;

		return CurrentEntry;
	}

    public static void ClearCurrentEntry() =>
        CurrentEntry = new();


    public static int LookupEvaluation(int depth, int ply, int alpha, int beta) 
	{
		if (!IsEnabled) return LookupFailed;

		if (CurrentEntry.Key == Board.ZobristKey) 
		{
			if (depth != Null && CurrentEntry.Score != LookupFailed) 
			{
				int correctedScore = CorrectRetrievedMateScore (CurrentEntry.Score, ply);

				// We have stored the exact evaluation for this position, so return it
				if (CurrentEntry.EvaluationType == EvaluationType.Exact) 
				{
					return correctedScore;
				}

				// We have stored the upper bound of the eval for this position. If it's less than alpha then we don't need to
				// search the moves in this position as they won't interest us; otherwise we will have to search to find the exact value

				if (CurrentEntry.EvaluationType == EvaluationType.UpperBound && correctedScore <= alpha) 
				{
					return correctedScore;
				}

				// We have stored the lower bound of the eval for this position. Only return if it causes a beta cut-off.
				if (CurrentEntry.EvaluationType == EvaluationType.LowerBound && correctedScore >= beta) 
				{
					return correctedScore;
				}
			}
		}

        return LookupFailed;
    }

	public static void StoreEvaluation(int depth, int numPlySearched, int score, EvaluationType evaluationType, Line line, int staticEvaluation) 
	{
		// Don't store incorrect values.
		if (!IsEnabled || WasSearchAborted || score == Null) return;

		if (depth >= CurrentEntry.Depth) 
            CurrentEntry = new Entry(Board.ZobristKey, CorrectMateScoreForStorage(score, numPlySearched), depth, staticEvaluation, evaluationType, line);
	}


	public static int CorrectMateScoreForStorage(int score, int numPlySearched) 
	{
		if (IsMateScore(score)) 
		{
			int sign = Math.Sign(score);
			return (score * sign + numPlySearched) * sign;
		}

		return score;
	}

    public static int CorrectRetrievedMateScore(int score, int numPlySearched) 
	{
		if (IsMateScore(score)) 
		{
			int sign = Math.Sign(score);
			return (score * sign - numPlySearched) * sign;
		}

		return score;
	}


	public struct Entry 
	{

		public readonly ulong Key;
		public readonly int Score;
		public readonly int Depth;
        public readonly int StaticEvaluation;
        public readonly EvaluationType EvaluationType;
        public readonly Line Line;

        public Entry(ulong key, int score, int depth, int staticEvaluation, EvaluationType evaluationType, Line line) 
		{
			Key = key;
            Score = score;
			Depth = depth;
			StaticEvaluation = staticEvaluation;
            EvaluationType = evaluationType;
            Line = line;
        }


		public static int GetSize() => Marshal.SizeOf<Entry>();
    }
}
