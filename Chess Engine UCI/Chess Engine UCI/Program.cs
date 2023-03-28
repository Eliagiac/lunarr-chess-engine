using System.Diagnostics;

MoveData.ComputeMoveData();
MoveData.GenerateDirectionalMasks();
MoveData.ComputeMagicBitboards();

AI.UseMoveOrdering = true;
AI.UseOpeningBook = false;
AI.LateMoveReductionMinimumTreshold = 1;
AI.LateMoveReductionPercentage = 100;
AI.UseTranspositionTable = true;
AI.ResetTranspositionTableOnEachSearch = false;
AI.ShallowDepthThreshold = 8;
AI.UseOpeningBook = false;

AI.Init();

Fen.ConvertFromFen(Fen.StartingFen);


Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;


bool wait = false;
while (true)
{
    if (AI.MoveFound)
    {
        Console.WriteLine($"bestmove {AI.MainLine.Move}");
        AI.MoveFound = false;
        wait = false;
    }

    if (wait) continue;

    string[] commands = Console.ReadLine().Split(' ');

    switch (commands[0])
    {
        case "position":

            switch (commands[1])
            {
                // Load the starting position.
                case "startpos":
                    Fen.ConvertFromFen(Fen.StartingFen);
                    break;

                // Load an opening position.
                case "opening":
                    Fen.ConvertFromFen("r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10 ");
                    break;

                // Load a middlegame position.
                case "midgame":
                    Fen.ConvertFromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - ");
                    break;

                // Load an endgame position.
                case "endgame":
                    Fen.ConvertFromFen("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - ");
                    break;

                default:
                    string fenString = commands.Skip(1).TakeWhile(command => command != "moves").Aggregate((a, b) => $"{a} {b}");

                    List<string> list = commands.ToList();
                    list.RemoveRange(1, list.Contains("moves") ? (list.IndexOf("moves") - 1) : list.Count - 1);
                    list.Insert(1, fenString);
                    commands = list.ToArray();

                    try
                    {
                        Fen.ConvertFromFen(commands[1]);
                    }
                    catch (Exception ex)
                    {
                        if (ex is KeyNotFoundException || ex is IndexOutOfRangeException)
                        {
                            Console.WriteLine("Invalid fen!");
                        }

                        else throw;
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
                                int promotionType = commands[i].Length == 5 ? Array.IndexOf(new[] { "", "p", "n", "b", "r", "q", "k" }, commands[i]) : 0;
                                Board.MakeMove(new(Board.PieceType(startIndex), 1UL << startIndex, 1UL << targetIndex, Board.PieceType(targetIndex), promotionType));
                            }
                            catch
                            {
                                Console.WriteLine("An error occured while parsing the moves!");
                            }
                        }

                        break;
                }
            }

            break;

        case "go":

            switch (commands[1])
            {
                case "movetime":
                    AI.UseTimeLimit = true;
                    AI.UseTimeManagement = false;
                    try
                    {
                        AI.TimeLimit = float.Parse(commands[2]) / 1000;
                    }
                    catch (FormatException)
                    {
                        Console.WriteLine("Invalid time limit!");
                    }

                    AI.PlayBestMove();

                    break;

                case "infinite":
                    AI.UseTimeLimit = false;
                    AI.UseTimeManagement = false;
                    AI.TimeLimit = 64; // Max depth.

                    AI.PlayBestMove();

                    break;

                default:
                    AI.UseTimeLimit = true;
                    AI.UseTimeManagement = true;

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
                    AI.OptimumTime = (Math.Max(totTime, 10));
                    AI.MaximumTime = (Math.Max(totTime, 10));

                    int hypMyTime = 0;

                    // We calculate optimum time usage for different hypothetical "moves to go" values
                    // and choose the minimum of calculated search time values. Usually the greatest
                    // hypMTG gives the minimum values.
                    for (int hypMTG = 1; hypMTG <= Math.Min(movesToGo > 0 ? movesToGo : 50, 50) ; hypMTG++)
                    {
                        // Calculate thinking time for hypothetical "moves to go"-value
                        hypMyTime = totTime + increment * (hypMTG - 1);

                        hypMyTime = Math.Max(hypMyTime, 0);

                        int t1 = 10 + Remaining(hypMyTime, hypMTG, AI.Ply, 0);
                        int t2 = 10 + Remaining(hypMyTime, hypMTG, AI.Ply, 1);

                        AI.OptimumTime = Math.Min(t1, AI.OptimumTime);
                        AI.MaximumTime = Math.Min(t2, AI.MaximumTime);
                    }

                    AI.TimeLimit = (AI.MaximumTime - 10) / 1000f;

                    AI.PlayBestMove();

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
            AI.StopSearch();
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

    return (int)(myTime * Math.Min(ratio1, ratio2));
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
