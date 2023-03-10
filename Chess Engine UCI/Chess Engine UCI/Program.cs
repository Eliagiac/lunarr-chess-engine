MoveData.ComputeMoveData();
MoveData.GenerateDirectionalMasks();
MoveData.ComputeMagicBitboards();
AIPlayer.Init();

AIPlayer.UseMoveOrdering = true;
AIPlayer.UseOpeningBook = false;
AIPlayer.LateMoveReductionMinimumTreshold = 3;
AIPlayer.LateMoveReductionPercentage = 98;
AIPlayer.ResetTranspositionTableOnEachSearch = false;
AIPlayer.ShallowDepthThreshold = 6;
AIPlayer.UseOpeningBook = false;


bool start = true;
while (true)
{
    if (AIPlayer.MoveFound || start)
    {
        if (AIPlayer.MoveFound)
        {
            Console.WriteLine($"bestmove {AIPlayer.MainLine.Move}");
            AIPlayer.MoveFound = false;
            start = true;
        }

        string[] commands = Console.ReadLine().Split(' ');

        switch (commands[0])
        {
            case "position":

                switch (commands[1])
                {
                    case "startpos":
                        Fen.ConvertFromFen(Fen.StartingFen);
                        start = true;
                        break;

                    default:

                        try
                        {
                            Fen.ConvertFromFen(commands[1]);
                            start = true;
                        }
                        catch (Exception ex)
                        {
                            if (ex is KeyNotFoundException || ex is IndexOutOfRangeException)
                            {
                                Console.WriteLine("Invalid fen!");
                                return;
                            }

                            throw;
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
                                    int promotionType = commands.Length == 5 ? Array.IndexOf(new[] { "", "p", "n", "b", "r", "q", "k" }, commands[4]) : 0;
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
                        AIPlayer.UseTimeLimit = true;
                        try
                        {
                            AIPlayer.TimeLimit = float.Parse(commands[2]) / 1000;
                        }
                        catch (FormatException)
                        {
                            Console.WriteLine("Invalid time limit!");
                        }

                        AIPlayer.PlayBestMove();
                        start = false;

                        break;

                    default:
                        AIPlayer.UseTimeLimit = true;

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
                        AIPlayer.OptimumTime = (Math.Max(totTime, 10));
                        AIPlayer.MaximumTime = (Math.Max(totTime, 10));

                        int hypMyTime = 0;

                        // We calculate optimum time usage for different hypothetical "moves to go" values
                        // and choose the minimum of calculated search time values. Usually the greatest
                        // hypMTG gives the minimum values.
                        for (int hypMTG = 1; hypMTG <= Math.Min(movesToGo > 0 ? movesToGo : 50, 50) ; hypMTG++)
                        {
                            // Calculate thinking time for hypothetical "moves to go"-value
                            hypMyTime = totTime + increment * (hypMTG - 1);

                            hypMyTime = Math.Max(hypMyTime, 0);

                            int t1 = 10 + Remaining(hypMyTime, hypMTG, AIPlayer.Ply, 0);
                            int t2 = 10 + Remaining(hypMyTime, hypMTG, AIPlayer.Ply, 1);

                            AIPlayer.OptimumTime = Math.Min(t1, AIPlayer.OptimumTime);
                            AIPlayer.MaximumTime = Math.Min(t2, AIPlayer.MaximumTime);
                        }

                        AIPlayer.TimeLimit = (AIPlayer.MaximumTime - 10) / 1000f;

                        AIPlayer.PlayBestMove();
                        start = false;

                        break;
                }

                break;

            case "uci":
                Console.WriteLine("id name Engine");
                Console.WriteLine("id author Elia Giaccardi");
                start = true;
                break;

            case "isready":
                Console.WriteLine("readyok");
                start = true;
                break;

            case "ucinewgame":
                start = true;
                break;

            case "stop":
                AIPlayer.StopSearch();
                start = true;
                break;
        }
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
