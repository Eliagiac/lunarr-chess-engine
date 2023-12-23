﻿using System.Diagnostics;
using static Utilities.Fen;
using static Engine;


Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

PrecomputedMoveData.ComputeMoveData();
PrecomputedMoveData.GenerateDirectionalMasks();
PrecomputedMoveData.ComputeMagicBitboards();

TT.Resize(64);

ConvertFromFen(StartingFen);


while (true)
{
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

            if (commands.Contains("multipv")) SetMultiPVCount(int.Parse(commands[Array.IndexOf(commands, "multipv") + 1]));

            switch (commands[1])
            {
                case "movetime":
                    try
                    {
                        SetSearchLimit(SearchLimit.Time, int.Parse(commands[2]));
                    }
                    catch (FormatException ex)
                    {
                        Console.WriteLine("Invalid time limit!");
                        Console.WriteLine(ex);
                    }

                    FindBestMove();

                    break;

                case "infinite":
                    SetSearchLimit(SearchLimit.None, 0);

                    FindBestMove();

                    break;

                case "depth":
                    SetSearchLimit(SearchLimit.Depth, int.Parse(commands[2]));

                    FindBestMove();

                    break;

                case "perft":
                    PerftResults(int.Parse(commands[2]), true);
                    break;

                default:
                    // The optimum time will be set to roughly half the max time with a value of 2.
                    const float optimumTimeFactor = 1.8f;

                    int movesToGo = 40;
                    int totTime = 0;
                    int increment = 0;

                    for (int i = 1; i < commands.Length; i++)
                    {
                        switch (commands[i])
                        {
                            case "wtime":
                                if (Board.Friendly == 0) totTime = int.Parse(commands[i + 1]);
                                break;

                            case "btime":
                                if (Board.Friendly == 1) totTime = int.Parse(commands[i + 1]);
                                break;

                            case "winc":
                                if (Board.Friendly == 0) increment = int.Parse(commands[i + 1]);
                                break;

                            case "binc":
                                if (Board.Friendly == 1) increment = int.Parse(commands[i + 1]);
                                break;

                            case "movestogo":
                                movesToGo = int.Parse(commands[i + 1]);
                                break;
                        }
                    }

                    // The time limit must be accurate and take into consideration the estimated move overhead.
                    int totTimeWithIncrement = 10 + totTime + increment * (movesToGo - 1) - GetMoveOverhead() * (movesToGo + 2);

                    // The optimum time is meant to allow the search to end early if enough time has passed.
                    int optimumTotTimeWithIncrement = 10 + totTime + (int)(increment * movesToGo * optimumTimeFactor);

                    SetSearchLimit(SearchLimit.TimeManagement, totTimeWithIncrement / movesToGo);
                    SetOptimumTime(optimumTotTimeWithIncrement / (int)(movesToGo * optimumTimeFactor));

                    FindBestMove();

                    break;
            }

            break;

        case "setoption":
            
            if (commands[1] == "name")
            {
                switch (commands[2])
                {
                    case "Hash":
                        if (commands[3] == "value") 
                            TT.Resize(int.Parse(commands[4]));

                        break;

                    case "Move Overhead":
                        if (commands[3] == "value")
                            SetMoveOverhead(int.Parse(commands[4]));

                        break;

                    case "Threads":
                        // Multithreading is not currently supported.
                        break;
                }
            }

            break;

        case "uci":
            Console.WriteLine("id name White Widow");
            Console.WriteLine("id author Elia Giaccardi");
            Console.WriteLine("option name Hash type spin default 64 min 1 max 33554432");
            Console.WriteLine("option name Move Overhead type spin default 10 min 0 max 5000");
            Console.WriteLine("option name Threads type spin default 1 min 1 max 1");
            Console.WriteLine("uciok");
            break;

        case "isready":
            Console.WriteLine("readyok");
            break;

        case "ucinewgame":
            AbortSearch();
            TT.Clear();
            break;

        case "stop":
            AbortSearch();
            break;

        case "quit":
            return;

        case "print":
            //Console.WriteLine($"Generate: {Board.tempGenerateStopwatch.ElapsedMilliseconds}");
            //Console.WriteLine($"Is legal: {Board.tempLegalStopwatch.ElapsedMilliseconds}");
            //Console.WriteLine($"Create moves (except is legal): {Board.tempCreateMoveStopwatch.ElapsedMilliseconds - Board.tempLegalStopwatch.ElapsedMilliseconds}");
            //Console.WriteLine($"Pins: {Board.tempPinStopwatch.ElapsedMilliseconds}");
            //Console.WriteLine($"Find pins: {Board.tempFindPinsStopwatch.ElapsedMilliseconds}");
            //Console.WriteLine($"Make move: {Board.tempMakeMoveStopwatch.ElapsedMilliseconds}");
            //Console.WriteLine($"Make move 1: {Board.tempMakeMove1Stopwatch.ElapsedMilliseconds}");
            //Console.WriteLine($"Make move 2: {Board.tempMakeMove2Stopwatch.ElapsedMilliseconds}");
            //Console.WriteLine($"Make move 3: {Board.tempMakeMove3Stopwatch.ElapsedMilliseconds}");
            //Console.WriteLine($"Make move 4: {Board.tempMakeMove4Stopwatch.ElapsedMilliseconds}");
            //Console.WriteLine($"Make move 5: {Board.tempMakeMove5Stopwatch.ElapsedMilliseconds}");
            //Console.WriteLine($"Make move 6: {Board.tempMakeMove6Stopwatch.ElapsedMilliseconds}");
            //Console.WriteLine($"Unmake move: {Board.tempUnmakeMoveStopwatch.ElapsedMilliseconds}");
            //Console.WriteLine($"Add piece: {Board.tempAddPieceStopwatch.ElapsedMilliseconds}");
            //Console.WriteLine($"Remove piece: {Board.tempRemovePieceStopwatch.ElapsedMilliseconds}");

            Console.WriteLine($"All: {Board.AllOccupiedSquares}");
            Console.WriteLine($"White: {Board.OccupiedSquares[0]}");
            Console.WriteLine($"Black: {Board.OccupiedSquares[1]}");
            break;
    }
}