using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PGNLoader {

	public static Move[] MovesFromPGN (string pgn, int maxPlyCount = int.MaxValue) {
		List<string> algebraicMoves = new List<string> ();

		string[] entries = pgn.Replace ("\n", " ").Split (' ');
		for (int i = 0; i < entries.Length; i++) {
			// Reached move limit, so exit.
			// (This is used for example when creating book, where only interested in first n moves of game)
			if (algebraicMoves.Count == maxPlyCount) {
				break;
			}

			string entry = entries[i].Trim ();

			if (entry.Contains (".") || entry == "1/2-1/2" || entry == "1-0" || entry == "0-1") {
				continue;
			}

			if (!string.IsNullOrEmpty (entry)) {
				algebraicMoves.Add (entry);
			}
		}

		return MovesFromAlgebraic (algebraicMoves.ToArray ());
	}

	static Move[] MovesFromAlgebraic (string[] algebraicMoves) {
		Board board = new Board();
        Fen.ConvertFromFen(Fen.StartingFen);
        var moves = new List<Move>();

		for (int i = 0; i < algebraicMoves.Length; i++) {
			Move move = MoveFromAlgebraic (board,algebraicMoves[i].Trim ());
			if (move == null) { // move is illegal; discard and return moves up to this point
				UnityEngine.Debug.Log ("illegal move in supplied pgn: " + algebraicMoves[i] + " move index: " + i);
				string pgn = "";
				foreach (string s in algebraicMoves) {
					pgn += s + " ";
				}
				Debug.Log ("problematic pgn: " + pgn);
				moves.ToArray ();
			} else {
				moves.Add (move);
			}
			Board.MakeMove(move);
		}
		return moves.ToArray ();
	}

	static Move MoveFromAlgebraic (Board board, string algebraicMove) {
		// Remove unrequired info from move string
		algebraicMove = algebraicMove.Replace ("+", "").Replace ("#", "").Replace ("x", "").Replace ("-", "");
		var allMoves = AI.GenerateAllLegalMoves(promotionMode : 0);

		Move move = null;
		foreach (Move moveToTest in allMoves) {
			move = moveToTest;

			int moveFromIndex = BitboardUtility.FirstSquareIndex(move.StartSquare);
			int moveToIndex = BitboardUtility.FirstSquareIndex(move.TargetSquare);
			int movePieceType = Board.PieceType(moveFromIndex);
			if (algebraicMove == "OO") { // castle kingside
				if (movePieceType == Piece.King && moveToIndex - moveFromIndex == 2) {
					return move;
				}
			} else if (algebraicMove == "OOO") { // castle queenside
				if (movePieceType == Piece.King && moveToIndex - moveFromIndex == -2) {
					return move;
				}
			}
			// Is pawn move if starts with any file indicator (e.g. 'e'4. Note that uppercase B is used for bishops) 
			else if ("abcdefgh".Contains (algebraicMove[0].ToString ())) {
				if (movePieceType != Piece.Pawn) {
					continue;
				}
				if ("abcdefgh".IndexOf (algebraicMove[0]) == Board.GetFile(moveFromIndex)) { // correct starting file
					if (algebraicMove.Contains ("=")) { // is promotion
						if (Board.GetRank(moveToIndex) == 0 || Board.GetRank(moveToIndex) == 7) {

							if (algebraicMove.Length == 5) // pawn is capturing to promote
							{
								char targetFile = algebraicMove[1];
								if ("abcdefgh".IndexOf (targetFile) != Board.GetFile(moveToIndex)) {
									// Skip if not moving to correct file
									continue;
								}
							}
							char promotionChar = algebraicMove[algebraicMove.Length - 1];

							if (move.PromotionPiece != GetPieceTypeFromSymbol (promotionChar)) {
								continue; // skip this move, incorrect promotion type
							}

							return move;
						}
					} else {

						char targetFile = algebraicMove[algebraicMove.Length - 2];
						char targetRank = algebraicMove[algebraicMove.Length - 1];

						if ("abcdefgh".IndexOf (targetFile) == Board.GetFile(moveToIndex)) { // correct ending file
							if (targetRank.ToString () == (Board.GetRank(moveToIndex) + 1).ToString ()) { // correct ending rank
								break;
							}
						}
					}
				}
			} else { // regular piece move

				char movePieceChar = algebraicMove[0];
				if (GetPieceTypeFromSymbol (movePieceChar) != movePieceType) {
					continue; // skip this move, incorrect move piece type
				}

				char targetFile = algebraicMove[algebraicMove.Length - 2];
				char targetRank = algebraicMove[algebraicMove.Length - 1];
				if ("abcdefgh".IndexOf (targetFile) == Board.GetFile(moveToIndex)) { // correct ending file
					if (targetRank.ToString () == (Board.GetRank(moveToIndex) + 1).ToString ()) { // correct ending rank

						if (algebraicMove.Length == 4) { // addition char present for disambiguation (e.g. Nbd7 or R7e2)
							char disambiguationChar = algebraicMove[1];

							if ("abcdefgh".Contains (disambiguationChar.ToString ())) { // is file disambiguation
								if ("abcdefgh".IndexOf (disambiguationChar) != Board.GetFile(moveFromIndex)) { // incorrect starting file
									continue;
								}
							} else { // is rank disambiguation
								if (disambiguationChar.ToString () != (Board.GetRank(moveFromIndex) + 1).ToString ()) { // incorrect starting rank
									continue;
								}

							}
						}
						break;
					}
				}
			}
		}
		return move;
	}

	static int GetPieceTypeFromSymbol (char symbol) {
		switch (symbol) {
			case 'R':
				return Piece.Rook;
			case 'N':
				return Piece.Knight;
			case 'B':
				return Piece.Bishop;
			case 'Q':
				return Piece.Queen;
			case 'K':
				return Piece.King;
			default:
				return Piece.None;
		}
	}
}
