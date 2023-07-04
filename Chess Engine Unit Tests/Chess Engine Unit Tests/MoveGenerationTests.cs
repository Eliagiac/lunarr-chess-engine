using System.Diagnostics;
using static Engine;
using static Utilities.Fen;

namespace Chess_Engine_Unit_Tests
{
    [TestClass]
    public class MoveGenerationTests
    {
        private const string Position1 = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        private const string Position2 = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq -";
        private const string Position3 = "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - -";
        private const string Position4 = "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1";
        private const string Position5 = "r2q1rk1/pP1p2pp/Q4n2/bbp1p3/Np6/1B3NBn/pPPP1PPP/R3K2R b KQ - 0 1";
        private const string Position6 = "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8";
        private const string Position7 = "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10";
        private const string Position8 = "r2k3r/nbNqpp1p/1p1p2nB/4P3/3P2p1/p1Q2N2/PPP1PPPP/R2K3R b KQkq -";

        private const string StockfishPath = @"C:\Users\eliag\OneDrive\Documenti\GitHub\Stockfish-VisualStudio\bin\Debug\x64\Stockfish.exe";

        static MoveGenerationTests()
        {
            MoveData.ComputeMoveData();
            MoveData.GenerateDirectionalMasks();
            MoveData.ComputeMagicBitboards();

            UseMoveOrdering = true;
            UseOpeningBook = false;
            LateMoveReductionMinimumTreshold = 1;
            LateMoveReductionPercentage = 1.0;
            UseTT = true;
            ResetTTOnEachSearch = false;
            ShallowDepthThreshold = 8;
            UseOpeningBook = false;
            InternalIterativeDeepeningDepthReduction = 5;
            ProbCutDepthReduction = 4;
            VerificationSearchMinimumDepth = 6;
            MultiPvCount = 1;

            Init();
        }


        [TestMethod]
        public void PerftResults_Position1_Depth1() =>
            TestPerftResults(Position1, 1);

        [TestMethod]
        public void PerftResults_Position1_Depth2() =>
            TestPerftResults(Position1, 2);

        [TestMethod]
        public void PerftResults_Position1_Depth3() =>
            TestPerftResults(Position1, 3);

        [TestMethod]
        public void PerftResults_Position1_Depth4() =>
            TestPerftResults(Position1, 4);

        [TestMethod]
        public void PerftResults_Position2_Depth1() =>
            TestPerftResults(Position2, 1);

        [TestMethod]
        public void PerftResults_Position2_Depth2() =>
            TestPerftResults(Position2, 2);

        [TestMethod]
        public void PerftResults_Position2_Depth3() =>
            TestPerftResults(Position2, 3);

        [TestMethod]
        public void PerftResults_Position2_Depth4() =>
            TestPerftResults(Position2, 4);

        [TestMethod]
        public void PerftResults_Position3_Depth1() =>
            TestPerftResults(Position3, 1);

        [TestMethod]
        public void PerftResults_Position3_Depth2() =>
            TestPerftResults(Position3, 2);

        [TestMethod]
        public void PerftResults_Position3_Depth3() =>
            TestPerftResults(Position3, 3);

        [TestMethod]
        public void PerftResults_Position3_Depth4() =>
            TestPerftResults(Position3, 4);

        [TestMethod]
        public void PerftResults_Position4_Depth1() =>
            TestPerftResults(Position4, 1);

        [TestMethod]
        public void PerftResults_Position4_Depth2() =>
            TestPerftResults(Position4, 2);

        [TestMethod]
        public void PerftResults_Position4_Depth3() =>
            TestPerftResults(Position4, 3);

        [TestMethod]
        public void PerftResults_Position4_Depth4() =>
            TestPerftResults(Position4, 4);

        [TestMethod]
        public void PerftResults_Position5_Depth1() =>
            TestPerftResults(Position5, 1);

        [TestMethod]
        public void PerftResults_Position5_Depth2() =>
            TestPerftResults(Position5, 2);

        [TestMethod]
        public void PerftResults_Position5_Depth3() =>
            TestPerftResults(Position5, 3);

        [TestMethod]
        public void PerftResults_Position5_Depth4() =>
            TestPerftResults(Position5, 4);

        [TestMethod]
        public void PerftResults_Position6_Depth1() =>
            TestPerftResults(Position6, 1);

        [TestMethod]
        public void PerftResults_Position6_Depth2() =>
            TestPerftResults(Position6, 2);

        [TestMethod]
        public void PerftResults_Position6_Depth3() =>
            TestPerftResults(Position6, 3);

        [TestMethod]
        public void PerftResults_Position6_Depth4() =>
            TestPerftResults(Position6, 4);

        [TestMethod]
        public void PerftResults_Position7_Depth1() =>
            TestPerftResults(Position7, 1);

        [TestMethod]
        public void PerftResults_Position7_Depth2() =>
            TestPerftResults(Position7, 2);

        [TestMethod]
        public void PerftResults_Position7_Depth3() =>
            TestPerftResults(Position7, 3);

        [TestMethod]
        public void PerftResults_Position7_Depth4() =>
            TestPerftResults(Position7, 4);

        [TestMethod]
        public void PerftResults_Position8_Depth1() =>
            TestPerftResults(Position8, 1);

        [TestMethod]
        public void PerftResults_Position8_Depth2() =>
            TestPerftResults(Position8, 2);

        [TestMethod]
        public void PerftResults_Position8_Depth3() =>
            TestPerftResults(Position8, 3);

        [TestMethod]
        public void PerftResults_Position8_Depth4() =>
            TestPerftResults(Position8, 4);


        private void TestPerftResults(string fen, int depth)
        {
            string moves = "";
            ConvertFromFen(fen);

            Perft(depth, depth);

            string correctResults = "";

            Process stockfish = new Process();
            stockfish.StartInfo.FileName = StockfishPath;
            stockfish.StartInfo.RedirectStandardInput = true;
            stockfish.StartInfo.RedirectStandardOutput = true;
            stockfish.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            stockfish.StartInfo.CreateNoWindow = true;
            stockfish.Start();
            Thread.Sleep(50);
            stockfish.BeginOutputReadLine();
            stockfish.OutputDataReceived += new DataReceivedEventHandler((s, e) => correctResults += e.Data + "\n");
            Thread.Sleep(50);

            StoreCorrectResults();

            Assert.IsTrue(DoResultsMatch(LastPerftResults, correctResults));
            stockfish.Dispose();
            

            void StoreCorrectResults()
            {
                correctResults = "";
                stockfish.StandardInput.WriteLine($"position fen {fen} moves {moves}");
                stockfish.StandardInput.WriteLine($"go perft {depth}");

                while (correctResults == "" || correctResults.Split('\n').Length < 4 || correctResults.Split('\n')[^3].Split(' ')[0] != "Nodes") 
                    Thread.Sleep(20);

                correctResults = string.Join('\n', correctResults.Split('\n')[..^4]);
            }

            bool DoResultsMatch(string results, string correctResults)
            {
                // Check if an illegal move was marked legal or if move counts don't match.
                foreach (string line in results.Split(new[] { '\n' }))
                {
                    string move = line.Split(' ')[0][..^1];
                    int count = int.Parse(line.Split(' ')[1]);

                    bool matching = false;
                    foreach (string stockfishLine in correctResults.Split(new[] { '\n' }))
                    {
                        string stockfishMove = "";
                        try
                        {
                            stockfishMove = stockfishLine.Split(' ')[0][..^1];
                        }
                        catch
                        {
                            int a = 0;
                        }
                        int stockfishCount = int.Parse(stockfishLine.Split(' ')[1]);

                        if (stockfishMove == move)
                        {
                            if (stockfishCount == count) matching = true;
                            break;
                        }
                    }

                    if (!matching)
                    {
                        InvestigateMove(ref depth, move);
                        return false;
                    }
                }

                // Check if a legal move is missing from our results.
                return correctResults.Split(new[] { '\n' }).All(line =>
                {
                    string move = line.Split(' ')[0][..^1];
                    bool found = results.Split(new[] { '\n' }).Select(l => l.Split(' ')[0][..^1]).Contains(move);

                    if (!found)
                        Console.WriteLine($"\nLegal move {move} missing in position {fen} moves {moves}!");

                    return found;
                });
            }

            void InvestigateMove(ref int depth, string move)
            {
                if (depth == 1)
                {
                    Console.WriteLine($"\nIllegal move {move} found in position {fen} moves {moves}!");
                    return;
                }

                moves += move + " ";

                int startIndex = (int)Enum.Parse(typeof(Square), move.Substring(0, 2));
                int targetIndex = (int)Enum.Parse(typeof(Square), move.Substring(2, 2));
                int promotionType = move.Length == 5 ? Array.IndexOf(new[] { "", "p", "n", "b", "r", "q", "k" }, move.Substring(4, 1)) : 0;
                Board.MakeMove(new(Board.PieceType(startIndex), 1UL << startIndex, 1UL << targetIndex, Board.PieceType(targetIndex), promotionType));

                depth--;
                Perft(depth, depth);

                StoreCorrectResults();

                // The DoResultsMatch function will check the new perft results and recursively call InvestigateMove until the root of the problem is found.
                DoResultsMatch(LastPerftResults, correctResults);
            }
        }
    }
}