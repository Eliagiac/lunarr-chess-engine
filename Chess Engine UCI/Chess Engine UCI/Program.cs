using System.Diagnostics;

using static Utilities.Fen;
using static Engine;


Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;

MoveData.ComputeMoveData();
MoveData.GenerateDirectionalMasks();
MoveData.ComputeMagicBitboards();

UseMoveOrdering = true;
UseOpeningBook = false;
LateMoveReductionMinimumTreshold = 1;
LateMoveReductionPercentage = 1.0;
UseTranspositionTable = true;
ResetTranspositionTableOnEachSearch = false;
ShallowDepthThreshold = 8;
UseOpeningBook = false;
InternalIterativeDeepeningDepthReduction = 5;
ProbCutDepthReduction = 4;
VerificationSearchMinimumDepth = 6;
MultiPvCount = 1;

Init();


ConvertFromFen(StartingFen);


bool wait = false;
while (true)
{
    //if (BestMoveFound)
    //{
    //    Console.WriteLine($"bestmove {MainLine.Move}");
    //    BestMoveFound = false;
    //    wait = false;
    //}

    if (wait) continue;

    string[] commands = Console.ReadLine().Split(' ');

    switch (commands[0])
    {
        case "position":

            switch (commands[1])
            {
                // Load the starting position.
                case "startpos":
                    ConvertFromFen(StartingFen);
                    break;

                // Load an opening position.
                case "opening":
                    ConvertFromFen("r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10 ");
                    break;

                // Load a middlegame position.
                case "midgame":
                    ConvertFromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - ");
                    break;

                // Load an endgame position.
                case "endgame":
                    ConvertFromFen("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - ");
                    break;

                default:
                    string fenString = commands.Skip(1).TakeWhile(command => command != "moves").Aggregate((a, b) => $"{a} {b}");

                    List<string> list = commands.ToList();
                    list.RemoveRange(1, list.Contains("moves") ? (list.IndexOf("moves") - 1) : list.Count - 1);
                    list.Insert(1, fenString);
                    commands = list.ToArray();

                    try
                    {
                        ConvertFromFen(commands[1]);
                    }
                    catch (Exception ex)
                    {
                        if (ex is KeyNotFoundException || ex is IndexOutOfRangeException)
                        {
                            Console.WriteLine("Invalid fen!");
                            Console.WriteLine(ex);
                        }

                        else
                        {
                            Console.WriteLine(ex);
                            throw;
                        }
                    }

                    break;
            }

            if (commands.Length > 2)
            {
                switch (commands[2])
                {
                    case "moves":

                        for (int i = 3; i < commands.Length; i++)
                        {
                            try
                            {
                                int startIndex = (int)Enum.Parse(typeof(Square), commands[i].Substring(0, 2));
                                int targetIndex = (int)Enum.Parse(typeof(Square), commands[i].Substring(2, 2));
                                int promotionType = commands[i].Length == 5 ? Array.IndexOf(new[] { "", "p", "n", "b", "r", "q", "k" }, commands[i].Substring(4, 1)) : 0;
                                Board.MakeMove(new(Board.PieceType(startIndex), 1UL << startIndex, 1UL << targetIndex, Board.PieceType(targetIndex), promotionType));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("An error occured while parsing the moves!");
                                Console.WriteLine(ex);
                            }
                        }

                        break;
                }
            }

            break;

        case "go":

            if (commands.Contains("multipv")) MultiPvCount = int.Parse(commands[Array.IndexOf(commands, "multipv") + 1]);

            switch (commands[1])
            {
                case "movetime":
                    UseTimeLimit = true;
                    UseTimeManagement = false;
                    try
                    {
                        TimeLimit = float.Parse(commands[2]) / 1000;
                    }
                    catch (FormatException ex)
                    {
                        Console.WriteLine("Invalid time limit!");
                        Console.WriteLine(ex);
                    }

                    FindBestMove();

                    break;

                case "infinite":
                    UseTimeLimit = false;
                    UseTimeManagement = false;
                    TimeLimit = 64; // Max depth.

                    FindBestMove();

                    break;

                case "depth":
                    UseTimeLimit = false;
                    UseTimeManagement = false;

                    TimeLimit = float.Parse(commands[2]); // Max depth.

                    FindBestMove();

                    break;

                default:
                    UseTimeLimit = true;
                    UseTimeManagement = true;

                    int movesToGo = 0;
                    int totTime = 0;
                    int increment = 0;

                    for (int i = 1; i < commands.Length; i++)
                    {
                        switch (commands[i])
                        {
                            case "wtime":
                                if (Board.CurrentTurn == 0) totTime = int.Parse(commands[i + 1]);
                                break;

                            case "btime":
                                if (Board.CurrentTurn == 1) totTime = int.Parse(commands[i + 1]);
                                break;

                            case "winc":
                                if (Board.CurrentTurn == 0) increment = int.Parse(commands[i + 1]);
                                break;

                            case "binc":
                                if (Board.CurrentTurn == 1) increment = int.Parse(commands[i + 1]);
                                break;

                            case "movestogo":
                                movesToGo = int.Parse(commands[i + 1]);
                                break;
                        }
                    }

                    // From Stockfish.
                    OptimumTime = (Math.Max(totTime, 10));
                    MaximumTime = (Math.Max(totTime, 10));

                    int hypMyTime = 0;

                    // We calculate optimum time usage for different hypothetical "moves to go" values
                    // and choose the minimum of calculated search time values. Usually the greatest
                    // hypMTG gives the minimum values.
                    for (int hypMTG = 1; hypMTG <= Math.Min(movesToGo > 0 ? movesToGo : 50, 50) ; hypMTG++)
                    {
                        // Calculate thinking time for hypothetical "moves to go"-value
                        hypMyTime = totTime + increment * (hypMTG - 1);

                        hypMyTime = Math.Max(hypMyTime, 0);

                        int t1 = 10 + Remaining(hypMyTime, hypMTG, Ply, 0);
                        int t2 = 10 + Remaining(hypMyTime, hypMTG, Ply, 1);

                        OptimumTime = Math.Min(t1, OptimumTime);
                        MaximumTime = Math.Min(t2, MaximumTime);
                    }

                    TimeLimit = (MaximumTime - 10) / 1000f;

                    FindBestMove();

                    break;
            }

            break;

        case "uci":
            Console.WriteLine("id name Engine");
            Console.WriteLine("id author Elia Giaccardi");
            break;

        case "isready":
            Console.WriteLine("readyok");
            break;

        case "ucinewgame":
            break;

        case "stop":
            AbortSearch();
            wait = true;
            break;

        case "quit":
            return;
    }
}


// From Stockfish.
int Remaining(int myTime, int movesToGo, int ply, int T)
{
    double TMaxRatio = (T == 0 ? 1.0 : 7.3);
    double TStealRatio = (T == 0 ? 0.0 : 0.34);

    double moveImportance = MoveImportance(ply);
    double otherMovesImportance = 0.0;

    for (int i = 1; i < movesToGo; ++i)
        otherMovesImportance += MoveImportance(ply + 2 * i);

    double ratio1 = (TMaxRatio * moveImportance) / (TMaxRatio * moveImportance + otherMovesImportance);
    double ratio2 = (moveImportance + TStealRatio * otherMovesImportance) / (moveImportance + otherMovesImportance);

    return (int)(myTime * Math.Min(ratio1, ratio2)); // Halve the time (for lack of better implementation).
}

// MoveImportance() is a skew-logistic function based on naive statistical
// analysis of "how many games are still undecided after n half-moves". Game
// is considered "undecided" as long as neither side has >275cp advantage.
// Data was extracted from the CCRL game database with some simple filtering criteria.
double MoveImportance(int ply)
{
    const double XScale = 6.85;
    const double XShift = 64.5;
    const double Skew = 0.171;

    return Math.Pow((1 + Math.Exp((ply - XShift) / XScale)), -Skew) + 0.000000000001; // Ensure non-zero
}
