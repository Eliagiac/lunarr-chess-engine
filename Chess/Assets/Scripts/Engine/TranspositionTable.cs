using System;
using static Engine;
using static System.Math;

public class TranspositionTable {

	public const int LookupFailed = 32003;

	// The value for this position is the exact evaluation.
	public const int Exact = 3;

	// A move was found during the search that was too good, meaning the opponent will play a different move earlier on,
	// not allowing the position where this move was available to be reached. Because the search cuts off at
	// this point (beta cut-off), an even better move may exist. This means that the evaluation for the
	// position could be even higher, making the stored value the lower bound of the actual value.
	public const int LowerBound = 2;

	// No move during the search resulted in a position that was better than the current player could get from playing a
	// different move in an earlier position (i.e eval was <= alpha for all moves in the position).
	// Due to the way alpha-beta search works, the value we get here won't be the exact evaluation of the position,
	// but rather the upper bound of the evaluation. This means that the evaluation is, at most, equal to this value.
	public const int UpperBound = 1;

	public Entry[] Entries;

	public readonly ulong Size;
	public bool Enabled = true;

	public TranspositionTable(int size) 
	{
		Size = (ulong)size;

		Entries = new Entry[size];
	}

	public void Clear () 
	{
		for (int i = 0; i < Entries.Length; i++) 
		{
			Entries[i] = new();
		}
	}

	// Use the first 28 bits of the key to get the index of the entry.
	// This number was tweaked for the best performance.
	// It may need to be updated after changing the size of the transposition table.
	// Note: the method on the right isn't used for worse performance on size 100000.
	public ulong Index => (Board.ZobristKey >> 36) % Size;//(((UInt32)Board.ZobristKey * (UInt64)Size) >> 32);


    public Move GetStoredMove() => Entries[Index].Line?.Move;

    public Line GetStoredLine() => Entries[Index].Line;

    public Entry GetStoredEntry(out bool ttHit)
    {
        Entry entry = Entries[Index];

        if (entry.Key == Board.ZobristKey) ttHit = true;
        else ttHit = false;

        return entry;
    }

    public int LookupEvaluation(int depth, int plyFromRoot, int alpha, int beta) 
	{
		if (!Enabled) return LookupFailed;

        Entry entry = Entries[Index];

		if (entry.Key == Board.ZobristKey) 
		{
			// Only use stored evaluation if it has been searched to at least the same depth as would be searched now
			if (entry.Depth >= depth && depth != Null && entry.Evaluation != LookupFailed) 
			{
				int correctedScore = CorrectRetrievedMateScore (entry.Evaluation, plyFromRoot);

				// We have stored the exact evaluation for this position, so return it
				if (entry.Bound == Exact) 
				{
					return correctedScore;
				}

				// We have stored the upper bound of the eval for this position. If it's less than alpha then we don't need to
				// search the moves in this position as they won't interest us; otherwise we will have to search to find the exact value

				if (entry.Bound == UpperBound && correctedScore <= alpha) 
				{
					return correctedScore;
				}

				// We have stored the lower bound of the eval for this position. Only return if it causes a beta cut-off.
				if (entry.Bound == LowerBound && correctedScore >= beta) 
				{
					return correctedScore;
				}
			}
		}

		return LookupFailed;
	}

	public void StoreEvaluation(int depth, int numPlySearched, int eval, int evalType, Line line, int staticEvaluation) 
	{
		if (!Enabled) return;

        ulong index = Index;
		if (depth >= Entries[index].Depth) 
		{
			Entry entry = new Entry(Board.ZobristKey, CorrectMateScoreForStorage(eval, numPlySearched), (byte)depth, (byte)evalType, line, staticEvaluation);
			Entries[index] = entry;
		}
	}

	public void ClearEntry()
	{
		Entries[Index] = new();
    }


	public int CorrectMateScoreForStorage(int score, int numPlySearched) 
	{
		if (IsMateScore(score)) 
		{
			int sign = Sign(score);
			return (score * sign + numPlySearched) * sign;
		}

		return score;
	}

    public int CorrectRetrievedMateScore(int score, int numPlySearched) 
	{
		if (IsMateScore(score)) 
		{
			int sign = Sign(score);
			return (score * sign - numPlySearched) * sign;
		}

		return score;
	}


	public struct Entry 
	{

		public readonly ulong Key;
		public readonly int Evaluation;
		public readonly Line Line;
		public readonly byte Depth;
		public readonly byte Bound;
        public readonly int StaticEvaluation;

        public Entry(ulong key, int evaluation, byte depth, byte bound, Line line, int staticEvaluation) 
		{
			Key = key;
            Evaluation = evaluation;
			Depth = depth;
			Bound = bound;
			Line = line;
			StaticEvaluation = staticEvaluation;
        }

		public static int GetSize() 
		{
			return System.Runtime.InteropServices.Marshal.SizeOf<Entry> ();
		}
	}
}
