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
        private const string Position8 = "r3k2r/p2p1pb1/bn1qpnp1/2pPN3/1p2P3/2N2Q1p/PPPBBPPP/2RK3R w kq c6 0 3";
        private const string Position9 = "1k6/1b6/8/8/7R/8/8/4K2R b K - 0 1";
        private const string Position10 = "3k4/3p4/8/K1P4r/8/8/8/8 b - - 0 1";
        private const string Position11 = "8/8/4k3/8/2p5/8/B2P2K1/8 w - - 0 1";
        private const string Position12 = "8/8/1k6/2b5/2pP4/8/5K2/8 b - d3 0 1";
        private const string Position13 = "5k2/8/8/8/8/8/8/4K2R w K - 0 1";
        private const string Position14 = "3k4/8/8/8/8/8/8/R3K3 w Q - 0 1";
        private const string Position15 = "r3k2r/1b4bq/8/8/8/8/7B/R3K2R w KQkq - 0 1";
        private const string Position16 = "r3k2r/8/3Q4/8/8/5q2/8/R3K2R b KQkq - 0 1";
        private const string Position17 = "2K2r2/4P3/8/8/8/8/8/3k4 w - - 0 1";
        private const string Position18 = "8/8/1P2K3/8/2n5/1q6/8/5k2 b - - 0 1";
        private const string Position19 = "4k3/1P6/8/8/8/8/K7/8 w - - 0 1";
        private const string Position20 = "8/P1k5/K7/8/8/8/8/8 w - - 0 1";
        private const string Position21 = "K1k5/8/P7/8/8/8/8/8 w - - 0 1";
        private const string Position22 = "8/k1P5/8/1K6/8/8/8/8 w - - 0 1";
        private const string Position23 = "8/8/2k5/5q2/5n2/8/5K2/8 b - - 0 1";
        private const string Position24 = "rnbqkbnr/pppppppp/8/8/3P4/8/PPPP1PPP/RNBQKBNR w KQkq - 0 1";

        private const string StockfishPath = @"C:\Users\eliag\OneDrive\Documenti\GitHub\Stockfish-VisualStudio\bin\Debug\x64\Stockfish.exe";

        static MoveGenerationTests()
        {
            PrecomputedMoveData.ComputeMoveData();
            PrecomputedMoveData.GenerateDirectionalMasks();
            PrecomputedMoveData.ComputeMagicBitboards();

            TT.Resize(8);
        }


        [TestMethod]
        public void PerftResults_Position1_Depth4() =>
            TestPerftResults(Position1, 4);

        [TestMethod]
        public void PerftResults_Position2_Depth4() =>
            TestPerftResults(Position2, 4);

        [TestMethod]
        public void PerftResults_Position3_Depth6() =>
            TestPerftResults(Position3, 6);

        [TestMethod]
        public void PerftResults_Position4_Depth5() =>
            TestPerftResults(Position4, 5);

        [TestMethod]
        public void PerftResults_Position5_Depth5() =>
            TestPerftResults(Position5, 5);

        [TestMethod]
        public void PerftResults_Position6_Depth5() =>
            TestPerftResults(Position6, 5);

        [TestMethod]
        public void PerftResults_Position7_Depth4() =>
            TestPerftResults(Position7, 4);

        [TestMethod]
        public void PerftResults_Position8_Depth4() =>
            TestPerftResults(Position8, 4);

        [TestMethod]
        public void PerftResults_Position9_Depth6() =>
            TestPerftResults(Position9, 6);

        [TestMethod]
        public void PerftResults_Position10_Depth7() =>
            TestPerftResults(Position10, 7);

        [TestMethod]
        public void PerftResults_Position11_Depth7() =>
            TestPerftResults(Position11, 7);

        [TestMethod]
        public void PerftResults_Position12_Depth7() =>
            TestPerftResults(Position12, 7);

        [TestMethod]
        public void PerftResults_Position13_Depth7() =>
            TestPerftResults(Position13, 7);

        [TestMethod]
        public void PerftResults_Position14_Depth7() =>
            TestPerftResults(Position14, 7);

        [TestMethod]
        public void PerftResults_Position15_Depth5() =>
            TestPerftResults(Position15, 5);

        [TestMethod]
        public void PerftResults_Position16_Depth5() =>
            TestPerftResults(Position16, 5);

        [TestMethod]
        public void PerftResults_Position17_Depth7() =>
            TestPerftResults(Position17, 7);

        [TestMethod]
        public void PerftResults_Position18_Depth6() =>
            TestPerftResults(Position18, 6);

        [TestMethod]
        public void PerftResults_Position19_Depth8() =>
            TestPerftResults(Position19, 8);

        [TestMethod]
        public void PerftResults_Position20_Depth8() =>
            TestPerftResults(Position20, 8);

        [TestMethod]
        public void PerftResults_Position21_Depth10() =>
            TestPerftResults(Position21, 10);

        [TestMethod]
        public void PerftResults_Position22_Depth9() =>
            TestPerftResults(Position22, 9);

        [TestMethod]
        public void PerftResults_Position23_Depth6() =>
            TestPerftResults(Position23, 6);

        [TestMethod]
        public void PerftResults_Position24_Depth4() =>
            TestPerftResults(Position24, 4);


        private void TestPerftResults(string fen, int depth)
        {
            string moves = "";
            ConvertFromFen(fen);

            string results = PerftResults(depth);

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

            Assert.IsTrue(DoResultsMatch(results, correctResults));
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
                        Console.WriteLine($"\nLegal move {move} missing in position {GetCurrentFen()} (moves {moves}).");

                    return found;
                });
            }

            void InvestigateMove(ref int depth, string move)
            {
                if (depth == 1)
                {
                    Console.WriteLine($"\nIllegal move {move} found in position {GetCurrentFen()} (moves {moves}).");
                    return;
                }

                moves += move + " ";

                int startIndex = (int)Enum.Parse(typeof(Square), move.Substring(0, 2));
                int targetIndex = (int)Enum.Parse(typeof(Square), move.Substring(2, 2));
                int promotionType = move.Length == 5 ? Array.IndexOf(new[] { "", "p", "n", "b", "r", "q", "k" }, move.Substring(4, 1)) : 0;
                Board.MakeMove(new(startIndex, targetIndex, promotionType), out int _, out int _);

                string results = PerftResults(--depth);

                StoreCorrectResults();

                // The DoResultsMatch function will check the new perft results and recursively call InvestigateMove until the root of the problem is found.
                DoResultsMatch(results, correctResults);
            }
        }
    }

    [TestClass]
    public class DrawByInsufficientMaterialTests
    {
        private const string Position1 = "8/2k5/8/8/5K2/8/8/8 w - - 0 1";
        private const string Position2 = "8/5K2/8/8/8/1k6/8/8 b - - 0 1";
        private const string Position3 = "8/5K2/8/7N/8/1k6/8/8 w - - 0 1";
        private const string Position4 = "8/5K2/8/2N5/8/1k6/8/8 b - - 0 1";
        private const string Position5 = "8/5K2/8/8/5n2/1k6/8/8 b - - 0 1";
        private const string Position6 = "8/5K2/3n4/8/8/1k6/8/8 w - - 0 1";
        private const string Position7 = "8/5K2/8/8/4B3/1k6/8/8 w - - 0 1";
        private const string Position8 = "8/2B2K2/8/8/8/1k6/8/8 b - - 0 1";
        private const string Position9 = "8/5K2/8/8/8/1k4b1/8/8 w - - 0 1";
        private const string Position10 = "8/5K2/8/8/6b1/1k6/8/8 b - - 0 1";
        private const string Position11 = "8/3B1K2/8/8/4b3/1k6/8/8 w - - 0 1";
        private const string Position12 = "8/5K2/8/8/6B1/1k3b2/8/8 b - - 0 1";
        private const string Position13 = "4B3/5K2/8/8/6B1/1k3b2/8/8 w - - 0 1";
        private const string Position14 = "4N3/5K2/8/8/8/1k3b2/8/8 w - - 0 1";
        private const string Position15 = "4N3/1n3K2/8/8/8/1k6/8/8 w - - 0 1";
        private const string Position16 = "8/8/5Bb1/2k5/8/6K1/8/8 w - - 0 1";
        private const string Position17 = "8/8/5B2/2k5/8/1b4K1/8/8 b - - 0 1";
        private const string Position18 = "8/8/3p1B2/2k5/8/6K1/8/8 b - - 0 1";
        private const string Position19 = "8/3P4/8/2k3p1/8/6K1/8/8 w - - 0 1";

        static DrawByInsufficientMaterialTests()
        {
            PrecomputedMoveData.ComputeMoveData();
            PrecomputedMoveData.GenerateDirectionalMasks();
            PrecomputedMoveData.ComputeMagicBitboards();

            TT.Resize(8);
        }


        [TestMethod]
        public void DrawByInsufficientMaterial_Position1_True() =>
            TestDrawByInsufficientMaterial(Position1, true);

        [TestMethod]
        public void DrawByInsufficientMaterial_Position2_True() =>
            TestDrawByInsufficientMaterial(Position2, true);

        [TestMethod]
        public void DrawByInsufficientMaterial_Position3_True() =>
            TestDrawByInsufficientMaterial(Position3, true);

        [TestMethod]
        public void DrawByInsufficientMaterial_Position4_True() =>
            TestDrawByInsufficientMaterial(Position4, true);

        [TestMethod]
        public void DrawByInsufficientMaterial_Position5_True() =>
            TestDrawByInsufficientMaterial(Position5, true);

        [TestMethod]
        public void DrawByInsufficientMaterial_Position6_True() =>
            TestDrawByInsufficientMaterial(Position6, true);

        [TestMethod]
        public void DrawByInsufficientMaterial_Position7_True() =>
            TestDrawByInsufficientMaterial(Position7, true);

        [TestMethod]
        public void DrawByInsufficientMaterial_Position8_True() =>
            TestDrawByInsufficientMaterial(Position8, true);

        [TestMethod]
        public void DrawByInsufficientMaterial_Position9_True() =>
            TestDrawByInsufficientMaterial(Position9, true);

        [TestMethod]
        public void DrawByInsufficientMaterial_Position10_True() =>
            TestDrawByInsufficientMaterial(Position10, true);

        [TestMethod]
        public void DrawByInsufficientMaterial_Position11_True() =>
            TestDrawByInsufficientMaterial(Position11, true);

        [TestMethod]
        public void DrawByInsufficientMaterial_Position12_True() =>
            TestDrawByInsufficientMaterial(Position12, true);

        [TestMethod]
        public void DrawByInsufficientMaterial_Position13_False() =>
            TestDrawByInsufficientMaterial(Position13, false);

        [TestMethod]
        public void DrawByInsufficientMaterial_Position14_False() =>
            TestDrawByInsufficientMaterial(Position14, false);

        [TestMethod]
        public void DrawByInsufficientMaterial_Position15_False() =>
            TestDrawByInsufficientMaterial(Position15, false);

        [TestMethod]
        public void DrawByInsufficientMaterial_Position16_False() =>
            TestDrawByInsufficientMaterial(Position16, false);

        [TestMethod]
        public void DrawByInsufficientMaterial_Position17_False() =>
            TestDrawByInsufficientMaterial(Position17, false);

        [TestMethod]
        public void DrawByInsufficientMaterial_Position18_False() =>
            TestDrawByInsufficientMaterial(Position18, false);

        [TestMethod]
        public void DrawByInsufficientMaterial_Position19_False() =>
            TestDrawByInsufficientMaterial(Position19, false);


        private void TestDrawByInsufficientMaterial(string fen, bool isTrue)
        {
            ConvertFromFen(fen);

            if (isTrue) 
                Assert.IsTrue(IsDrawByInsufficientMaterial());

            else
                Assert.IsFalse(IsDrawByInsufficientMaterial());
        }
    }
}