using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Utilities
{
    public class Fen
    {
        public const string StartingFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        static Dictionary<char, int> pieceTypeFromSymbol = new()
        {
            ['k'] = Piece.King,
            ['p'] = Piece.Pawn,
            ['n'] = Piece.Knight,
            ['b'] = Piece.Bishop,
            ['r'] = Piece.Rook,
            ['q'] = Piece.Queen
        };

        public static void ConvertFromFen(string fen)
        {
            Board.Init();

            string[] sections = fen.Split(' ');

            int file = 0;
            int rank = 7;

            foreach (char symbol in sections[0])
            {
                if (symbol == '/')
                {
                    file = 0;
                    rank--;
                }
                else
                {
                    if (char.IsDigit(symbol))
                    {
                        file += (int)char.GetNumericValue(symbol);
                    }
                    else
                    {
                        int pieceColour = (char.IsUpper(symbol)) ? Piece.White : Piece.Black;
                        int pieceType = pieceTypeFromSymbol[char.ToLower(symbol)];

                        Board.Pieces[pieceType][pieceColour == Piece.White ? 0 : 1] |= 1UL << (rank * 8 + file);

                        file++;

                    }
                }
            }

            Board.CurrentTurn = sections[1] == "w" ? 0 : 1;
            Board.OpponentTurn = Board.CurrentTurn ^ 1;

            string castlingRights = (sections.Length > 2) ? sections[2] : "KQkq";
            Board.CastlingRights |= Mask.WhiteKingsideCastling & (castlingRights.Contains("K") ? ulong.MaxValue : 0);
            Board.CastlingRights |= Mask.WhiteQueensideCastling & (castlingRights.Contains("Q") ? ulong.MaxValue : 0);
            Board.CastlingRights |= Mask.BlackKingsideCastling & (castlingRights.Contains("k") ? ulong.MaxValue : 0);
            Board.CastlingRights |= Mask.BlackQueensideCastling & (castlingRights.Contains("q") ? ulong.MaxValue : 0);

            if (sections.Length > 3)
            {
                string enPassantFileName = sections[3][0].ToString();
                if ("abcdefgh".Contains(enPassantFileName))
                {
                    Board.EnPassantSquare = (Mask.PawnsRank << "abcdefgh".IndexOf(enPassantFileName)) & (Board.CurrentTurn == 1 ? Mask.WhitePawnsRank : Mask.BlackPawnsRank);
                    Board.EnPassantTarget = (Mask.DoublePawnsRank << "abcdefgh".IndexOf(enPassantFileName)) & (Board.CurrentTurn == 1 ? Mask.WhiteDoublePawnsRank : Mask.BlackDoublePawnsRank);
                }
            }

            Board.UpdateSquares();

            Board.UpdateAllOccupiedSquares();
            Board.UpdateBoardInformation();
            Board.UpdateBoardInformation();
        }
    }
}
