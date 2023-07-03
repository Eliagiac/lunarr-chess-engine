using static Utilities.Bitboard;

namespace Utilities
{
	/// <summary>
	/// A collection of constant keys used to update the board's Zobrist key.
	/// </summary>
	public static class Zobrist
	{
		// Note: using a known seed allows storing of data regarding specific
		// positions throughout different excecutions (for example, for an opening book).
		private const int Seed = 2361912;


		// Indexed by [pieceType, pieceColor, squareIndex]
		public static readonly ulong[,,] PieceKeys = new ulong[8, 2, 64];

		// Indexed by [4BitCastlingRights]
		public static readonly ulong[] CastlingRightsKeys = new ulong[16];

        // Indexed by [enPassantFile]
        // Note: if en passant is unavailable, EnPassantFileKeys[0] should be used.
        public static readonly ulong[] EnPassantFileKeys = new ulong[9]; // no need for rank info as side to move is included in key

		// Note: this key is used on every turn change (it's effectively either added or removed).
		public static readonly ulong BlackToMoveKey;


		private static Random _randomNumberGenerator = new(Seed);


        static string randomNumbersPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RandomNumbers.txt");


        private static void WriteRandomNumbers()
        {
            int randomNumbersAmount = 
				(PieceKeys.GetLength(0) * PieceKeys.GetLength(1) * PieceKeys.GetLength(2)) +
                CastlingRightsKeys.GetLength(0) +
                EnPassantFileKeys.GetLength(0) +
                1;

			ulong[] randomNumbers = new ulong[randomNumbersAmount];

            for (int i = 0; i < randomNumbers.Length; i++)
                randomNumbers[i] = NextRandomNumber();

            StreamWriter writer = new(randomNumbersPath);
			writer.Write(string.Join(',', randomNumbers));
			writer.Close();
		}

        private static Queue<ulong> ReadRandomNumbers()
		{
			if (!File.Exists(randomNumbersPath)) WriteRandomNumbers();

            Queue<ulong> randomNumbers = new();

            StreamReader reader = new(randomNumbersPath);
            string randomNumbersString = reader.ReadToEnd();
			reader.Close();

			foreach (var number in randomNumbersString.Split(','))
				randomNumbers.Enqueue(ulong.Parse(number));

			return randomNumbers;
		}

		static Zobrist()
		{

			var randomNumbers = ReadRandomNumbers();

			for (int squareIndex = 0; squareIndex < 64; squareIndex++)
			{
				for (int pieceIndex = 0; pieceIndex < 8; pieceIndex++)
				{
                    PieceKeys[pieceIndex, 0, squareIndex] = randomNumbers.Dequeue();
                    PieceKeys[pieceIndex, 1, squareIndex] = randomNumbers.Dequeue();
				}
			}

			for (int i = 0; i < 16; i++)
			{
                CastlingRightsKeys[i] = randomNumbers.Dequeue();
			}

			for (int i = 0; i < EnPassantFileKeys.Length; i++)
			{
                EnPassantFileKeys[i] = randomNumbers.Dequeue();
			}

            BlackToMoveKey = randomNumbers.Dequeue();
		}

		/// <summary>
		/// Generate a unique key based on the current board state. <br />
		/// Should only be used upon board initialization.
		/// </summary>
		public static ulong CalculateZobristKey()
		{
			ulong key = 0;

			for (int squareIndex = 0; squareIndex < 64; squareIndex++)
			{
				int pieceType = Board.PieceType(squareIndex);
				int pieceColorIndex = Board.PieceColor(squareIndex) == Piece.White ? 0 : 1;

				if (pieceType != Piece.None) key ^= PieceKeys[pieceType, pieceColorIndex, squareIndex];
			}

			if (Board.CurrentTurn == 1) key ^= BlackToMoveKey;

            key ^= CastlingRightsKeys[Board.FourBitCastlingRights()];

			int enPassantFileIndex = Board.EnPassantSquare != 0 ? 
				Board.GetFile(FirstSquareIndex(Board.EnPassantSquare)) + 1 : 
				0;

            key ^= EnPassantFileKeys[enPassantFileIndex];

			return key;
		}

        static ulong NextRandomNumber()
		{
			byte[] buffer = new byte[8];
			_randomNumberGenerator.NextBytes(buffer);
			return BitConverter.ToUInt64(buffer, 0);
		}
	}
}
