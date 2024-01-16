using static Utilities.Bitboard;
using static Engine;

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

        public static Board ConvertFromFen(Board board, string fen)
        {
            board.Init();

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
                        int pieceColor = char.IsUpper(symbol) ? Piece.White : Piece.Black;
                        int pieceType = pieceTypeFromSymbol[char.ToLower(symbol)];

                        board.Pieces[pieceType][pieceColor == Piece.White ? 0 : 1] |= 1UL << (rank * 8 + file);

                        file++;

                    }
                }
            }

            board.Friendly = sections[1] == "w" ? 0 : 1;
            board.Opponent = board.Friendly ^ 1;

            string castlingRights = (sections.Length > 2) ? sections[2] : "KQkq";
            board.CastlingRights &= 0;
            board.CastlingRights |= Mask.WhiteKingsideCastling & (castlingRights.Contains("K") ? ulong.MaxValue : 0);
            board.CastlingRights |= Mask.WhiteQueensideCastling & (castlingRights.Contains("Q") ? ulong.MaxValue : 0);
            board.CastlingRights |= Mask.BlackKingsideCastling & (castlingRights.Contains("k") ? ulong.MaxValue : 0);
            board.CastlingRights |= Mask.BlackQueensideCastling & (castlingRights.Contains("q") ? ulong.MaxValue : 0);

            if (sections.Length > 3)
            {
                string enPassantFileName = sections[3];
                if ("abcdefgh".Contains(enPassantFileName))
                {
                    board.EnPassantSquare = (Mask.PawnsRank << "abcdefgh".IndexOf(enPassantFileName)) & (board.Friendly == 1 ? Mask.WhitePawnsRank : Mask.BlackPawnsRank);
                    board.EnPassantTarget = (Mask.DoublePawnsRank << "abcdefgh".IndexOf(enPassantFileName)) & (board.Friendly == 1 ? Mask.WhiteDoublePawnsRank : Mask.BlackDoublePawnsRank);
                }
            }

            if (sections.Length > 4)
            {
                int.TryParse(sections[4], out board.PlyCountReversible);
                int.TryParse(sections[4], out board.FiftyMovePlyCount);
            }

            if (sections.Length > 5)
            {
                int moveCount;
                int.TryParse(sections[5], out moveCount);
                board.PlyCount = ((moveCount - 1) * 2) + (board.Friendly == 0 ? 0 : 1);
            }

            board.UpdateSquares();

            board.UpdateAllOccupiedSquares();
            board.UpdateKingPositions();
            board.UpdateCheckData();
            board.UpdatePawnAttackedSquares();
            board.ZobristKey = Zobrist.CalculateZobristKey(board);

            board.PsqtScore[0] = PieceSquareTables.EvaluateAllPsqt(board, 0);
            board.PsqtScore[1] = PieceSquareTables.EvaluateAllPsqt(board, 1);

            board.MaterialScore[0] = Evaluation.ComputeMaterial(board, 0);
            board.MaterialScore[1] = Evaluation.ComputeMaterial(board, 1);

            board.GameStateHistory.Push(board.CurrentGameState());
            board.RepetitionTable.PopulateWithCurrentHistory(board);

            return board;
        }

        public static string GetCurrentFen(Board board)
        {
            string currentString = "";
            string fen = "";
            int emptySquaresCount = 0;

            for (int i = 63; i >= 0; i--)
            {
                string pieceLetter = board.Squares[i].PieceLetter();
                if (pieceLetter == "-") emptySquaresCount++;
                else
                {
                    if (emptySquaresCount != 0)
                    {
                        currentString += emptySquaresCount;
                        emptySquaresCount = 0;
                    }
                    currentString += pieceLetter;
                }

                if (i % 8 == 0)
                {
                    if (emptySquaresCount != 0)
                    {
                        currentString += emptySquaresCount;
                        emptySquaresCount = 0;
                    }
                    
                    // Letters are added in reverse order compared to fen notation.
                    fen += Reverse(currentString);
                    if (i != 0) fen += "/";

                    currentString = "";
                }
            }

            fen += " ";
            fen += board.Friendly == 0 ? "w" : "b";

            fen += " ";
            string castlingRights = "";
            if ((board.CastlingRights & Mask.WhiteKingsideCastling) != 0) castlingRights += "K";
            if ((board.CastlingRights & Mask.WhiteQueensideCastling) != 0) castlingRights += "Q";
            if ((board.CastlingRights & Mask.BlackKingsideCastling) != 0) castlingRights += "k";
            if ((board.CastlingRights & Mask.BlackQueensideCastling) != 0) castlingRights += "q";
            if (castlingRights == "") castlingRights = "-";
            fen += castlingRights;

            fen += " ";
            if (board.EnPassantSquare != 0)
            {
                fen += Enum.GetName((Square)FirstSquareIndex(board.EnPassantSquare));
            }
            else fen += "-";

            return fen;


            string Reverse(string s)
            {
                char[] charArray = s.ToCharArray();
                Array.Reverse(charArray);
                return new string(charArray);
            }
        }
    }
}
