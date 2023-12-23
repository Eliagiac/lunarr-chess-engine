using static Utilities.Bitboard;

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
            Board.PositionHistory = new();

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

                        Board.Pieces[pieceType][pieceColor == Piece.White ? 0 : 1] |= 1UL << (rank * 8 + file);

                        file++;

                    }
                }
            }

            Board.Friendly = sections[1] == "w" ? 0 : 1;
            Board.Opponent = Board.Friendly ^ 1;

            string castlingRights = (sections.Length > 2) ? sections[2] : "KQkq";
            Board.CastlingRights &= 0;
            Board.CastlingRights |= Mask.WhiteKingsideCastling & (castlingRights.Contains("K") ? ulong.MaxValue : 0);
            Board.CastlingRights |= Mask.WhiteQueensideCastling & (castlingRights.Contains("Q") ? ulong.MaxValue : 0);
            Board.CastlingRights |= Mask.BlackKingsideCastling & (castlingRights.Contains("k") ? ulong.MaxValue : 0);
            Board.CastlingRights |= Mask.BlackQueensideCastling & (castlingRights.Contains("q") ? ulong.MaxValue : 0);

            if (sections.Length > 3)
            {
                string enPassantFileName = sections[3][0].ToString();
                if ("abcdefgh".Contains(enPassantFileName))
                {
                    Board.EnPassantSquare = (Mask.PawnsRank << "abcdefgh".IndexOf(enPassantFileName)) & (Board.Friendly == 1 ? Mask.WhitePawnsRank : Mask.BlackPawnsRank);
                    Board.EnPassantTarget = (Mask.DoublePawnsRank << "abcdefgh".IndexOf(enPassantFileName)) & (Board.Friendly == 1 ? Mask.WhiteDoublePawnsRank : Mask.BlackDoublePawnsRank);
                }
            }

            Board.UpdateSquares();

            Board.UpdateAllOccupiedSquares();
            Board.UpdateKingPositions();
            Board.UpdateCheckData();
            //Board.GenerateAttackedSquares();
            Board.ZobristKey = Zobrist.CalculateZobristKey();

            Board.PsqtScore[0] = PieceSquareTables.EvaluateAllPsqt(0);
            Board.PsqtScore[1] = PieceSquareTables.EvaluateAllPsqt(1);

            Board.MaterialScore[0] = Evaluation.ComputeMaterial(0);
            Board.MaterialScore[1] = Evaluation.ComputeMaterial(1);
        }

        public static string GetCurrentFen()
        {
            string currentString = "";
            string fen = "";
            int emptySquaresCount = 0;

            for (int i = 63; i >= 0; i--)
            {
                string pieceLetter = Board.Squares[i].PieceLetter();
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
            fen += Board.Friendly == 0 ? "w" : "b";

            fen += " ";
            string castlingRights = "";
            if ((Board.CastlingRights & Mask.WhiteKingsideCastling) != 0) castlingRights += "K";
            if ((Board.CastlingRights & Mask.WhiteQueensideCastling) != 0) castlingRights += "Q";
            if ((Board.CastlingRights & Mask.BlackKingsideCastling) != 0) castlingRights += "k";
            if ((Board.CastlingRights & Mask.BlackQueensideCastling) != 0) castlingRights += "q";
            if (castlingRights == "") castlingRights = "-";
            fen += castlingRights;

            fen += " ";
            if (Board.EnPassantSquare != 0)
            {
                fen += Enum.GetName((Square)FirstSquareIndex(Board.EnPassantSquare));
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
