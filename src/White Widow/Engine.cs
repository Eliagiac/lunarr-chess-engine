using System.Diagnostics;
using static Utilities.Bitboard;
using static Utilities.Fen;
using static System.Math;
using static Piece;
using static Evaluation;
using static Move;

/// <summary>The <see cref="Engine"/> class contains the main features of the engine.</summary>
public class Engine
{
    /// <summary>Represents a value that is either not usable or incorrect.</summary>
    public const int Null = 32002;

    /// <summary>The maximum evaluation of any position.</summary>
    public const int Infinity = 32001;

    /// <summary>The score given to a position with checkmate on the board.</summary>
    /// <remarks>Positions with a forced mating sequence are given a score of
    /// <see cref="Checkmate"/> - <c>distance from mate</c>. For example, <c>mate in 5</c> = <c>31995</c>.</remarks>
    public const int Checkmate = 32000;

    /// <summary>The evaluation of a drawn position or stalemate.</summary>
    public const int Draw = 0;

    private const int MaxPly = 64;

    private const int LateMoveReductionsMinimumThreshold = 1;
    private const double LateMoveReductionsPercentage = 1.0;
    private const int ShallowDepthThreshold = 8;
    private const int InternalIterativeDeepeningDepthReduction = 5;
    private const int ProbCutDepthReduction = 4;

    private const bool ResetTTOnEachSearch = false;


    /// <summary>Minimum move index to use late move reductions, depending on total move count.</summary>
    private static readonly int[] s_lateMoveThresholds = 
        Enumerable.Range(0, 300).Select(totalMoveCount => (int)(
        (totalMoveCount + LateMoveReductionsMinimumThreshold) - (totalMoveCount * LateMoveReductionsPercentage))).ToArray();

    /// <summary>If the evaluation is too low compared to alpha (by at least <c><see cref="s_futilityMargin"/>[Improving 0/1][Depth]</c>) more moves will be skipped.</summary>
    /// <remarks>The margin is lower if the evaluation has improved since the player's last turn, 
    /// and is directly proportional to the depth. </remarks>
    private static readonly int[][] s_futilityMargin = new[] {
        Enumerable.Range(0, 64).Select(depth => 165 * depth).ToArray(),
        Enumerable.Range(0, 64).Select(depth => 165 * (depth - 1)).ToArray(),
    };

    /// <summary>Alpha and beta aspiration window increase factor.</summary>
    private static readonly int[] s_aspirationWindowsMultipliers = { 4, 4 };

    /// <summary>Depth reduction for late moves, indexed by [Improving 0/1][Depth][MoveNumber].</summary>
    /// <remarks>Later moves will receive greater depth reductions.</remarks>
    private static readonly int[][][] s_lateMoveDepthReductions =
        Enumerable.Range(0, 64).Select(depth =>
        Enumerable.Range(0, 64).Select(moveNumber =>
        {
            int[] reductions = new int[2];

            if (depth == 0 || moveNumber == 0) return reductions;

            // Reduction amounts are from a 2019 Stocfish version.
            double reduction = Log(depth) * Log(moveNumber) / 1.95f;

            // Set improving reduction.
            reductions[1] = Max((int)Round(reduction), 0);

            // Set non-improving reduction to one above improving reduction (or the same if reduction is low).
            reductions[0] = reductions[1] + (reduction > 1 ? 1 : 0);

            return reductions;
        }
        ).ToArray()).ToArray();

    
    /// <summary><see cref="Stopwatch"/> that keeps track of the total time taken by the current search.</summary>
    /// <remarks>Note that it is not reset on different iterations of iterative deepening.</remarks>
    private static readonly Stopwatch s_searchStopwatch = new();

    private static CancellationTokenSource? s_abortSearchTimer;

    private static ThreadInfo[] s_threads;


    private static int s_moveOverhead = 10;
    private static int s_depthLimit;
    private static int s_timeLimit;
    private static int s_optimumTime;

    private static bool s_useDepthLimit;
    private static bool s_useTimeLimit;
    private static bool s_useTimeManagement;

    private static int s_multiPvCount = 1;
    private static int s_threadCount = 1;

    private static List<Move> s_excludedRootMoves = new();


    /// <summary>Killer moves are quiet moves that caused a beta cutoff, indexed by <c>[KillerMoveIndex, Ply]</c>.<br />
    /// If the same move is found in another position at the same ply, it will be prioritized.</summary>
    /// <remarks>2 killer moves are stored at each ply. Storing more would increase the complexity of adding a new move.</remarks>
    [ThreadStatic] 
    private static Move[,] t_killerMoves = new Move[2, MaxPly];

    /// <summary>Bonus based on the success of a move in other positions.</summary>
    /// <remarks>Moves are identified using butterfly boards (https://www.chessprogramming.org/Butterfly_Boards) 
    /// with [ColorIndex][StartSquareIndex, TargetSquareIndex].</remarks>
    [ThreadStatic]
    private static int[][,] t_historyHeuristics = new[] { new int[64, 64], new int[64, 64] };


    [ThreadStatic]
    private static int t_maxDepthReached;
    [ThreadStatic]
    private static int t_rootDepth;

    [ThreadStatic]
    private static int t_totalSearchNodes;

    [ThreadStatic]
    private static Board t_board = new();

    /// <summary>The main search thread is the first thread that starts searching.</summary>
    [ThreadStatic]
    private static bool t_isMainSearchThread = false;


#if DEBUG
    private static bool s_writeLogs = true;
    private static string s_log = "";

    private static string LogPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");

    private static void EraseLog()
    {
        if (!s_writeLogs) return;

        StreamWriter writer = new(LogPath);
        writer.Close();
    }

    private static void WriteLog(string log)
    {
        if (!s_writeLogs) return;

        StreamWriter writer = new(LogPath, true);
        writer.WriteLine(log);
        writer.Close();
    }
#endif


    /// <summary>Type of limitation applied to the search.</summary>
    public enum SearchLimit
    {
        /// <summary>The search will go on to reach a depth of <see cref="MaxPly"/>.</summary>
        None,

        /// <summary>The search will go on until the specified <see cref="s_depthLimit"/> is reached.</summary>
        Depth,

        /// <summary>The search will go on for <see cref="s_timeLimit"/> milliseconds.</summary>
        Time,

        /// <summary>The search will stop automatically if <see cref="s_optimumTime"/> milliseconds have passed 
        /// after a search is complete (inside the iterative deepening loop), or will otherwise be aborted after <see cref="s_timeLimit"/> milliseconds.</summary>
        TimeManagement,
    }


    /// <summary>If the search was abruptly interrupted, the returned values will be unusable.</summary>
    public static bool WasSearchAborted { get; private set; }

    /// <summary>The best move found by the engine, and the best play sequence that follows it.</summary>
    public static Line? MainLine { get; private set; }


    /// <summary>Specify an estimate of the time taken to play a move (lag).</summary>
    public static void SetMoveOverhead(int moveOverhead) => s_moveOverhead = moveOverhead;

    /// <summary>Returns the current move overhead estimate.</summary>
    public static int GetMoveOverhead() => s_moveOverhead;

    /// <summary>Select which type of limit the next search will use.</summary>
    /// <remarks>Limits aren't automatically reset on each search.</remarks>
    /// <param name="searchLimit">What type of limit is applied to the next search.</param>
    /// <param name="limit">Ignored if searchLimit == SearchLimit.None.</param>
    public static void SetSearchLimit(SearchLimit searchLimit, int limit)
    {
        switch (searchLimit)
        {
            case SearchLimit.None:
                s_useDepthLimit = true;
                s_useTimeLimit = false;
                s_useTimeManagement = false;

                s_depthLimit = MaxPly;
                break;

            case SearchLimit.Depth:
                s_useDepthLimit = true;
                s_useTimeLimit = false;
                s_useTimeManagement = false;

                s_depthLimit = limit;
                break;

            case SearchLimit.Time:
                s_useDepthLimit = false;
                s_useTimeLimit = true;
                s_useTimeManagement = false;

                s_timeLimit = limit;
                break;

            case SearchLimit.TimeManagement:
                s_useDepthLimit = false;
                s_useTimeLimit = true;
                s_useTimeManagement = true;

                s_timeLimit = limit;
                break;
        }
    }

    /// <summary>If using time management, set the optimum time for the next search.</summary>'
    /// <remarks>Should only be used after the maximum time limit has been set and only when using time management.</remarks>
    public static void SetOptimumTime(int optimumTime) => s_optimumTime = optimumTime;

    /// <summary>Set the amount of root moves that will each receive a separate evaluation.</summary>
    public static void SetMultiPVCount(int multiPVCount) => s_multiPvCount = multiPVCount;
    
    /// <summary>Set the amount of threads that will be searching at the same time.</summary>
    public static void SetThreadCount(int threadCount) => s_threadCount = threadCount;

    /// <summary>Set the starting position for all threads.</summary>
    /// <remarks>This function should only be called from the main thread.</remarks>
    public static Board SetPosition(string fen) => ConvertFromFen(t_board, fen);


    /// <summary>The total number of legal moves after each move in the current position and down the game tree until the desired depth is reached.</summary>
    /// <returns>A string with each root move on a new line and the legal move count after it. For example, "a2a4: 20".</returns>
    public static string PerftResults(int depth, bool debug = false)
    {
        Stopwatch perftStopwatch = new();
        perftStopwatch.Start();

        List<string> results = new();

        var moves = t_board.GenerateAllLegalMoves();
        int totNumPositions = 0;
        foreach (var move in moves)
        {
            t_board.MakeMove(move, out int _, out int _);

            int numPositions = Perft(depth - 1);
            totNumPositions += numPositions;
            results.Add($"{move}: {numPositions}");
            Console.WriteLine($"{move}: {numPositions}");

            t_board.UnmakeMove(move);
        }

        if (debug)
        {
            Console.WriteLine();
            Console.WriteLine($"Speed: {totNumPositions / (perftStopwatch.ElapsedMilliseconds / 1000f)} nps");
            Console.WriteLine($"Time: {perftStopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Nodes: {totNumPositions}");
        }

        return string.Join('\n', results);


        int Perft(int depth)
        {
            if (depth == 0) return 1;

            var moves = t_board.GenerateAllLegalMoves();
            int numPositions = 0;
            foreach (var move in moves)
            {
                t_board.MakeMove(move, out int _, out int _);
                numPositions += Perft(depth - 1);
                t_board.UnmakeMove(move);
            }
            return numPositions;
        }
    }


    /// <summary>Start a new search using the last applied limits and store the best move in <see cref="MainLine"/>.</summary>
    /// <remarks>This function must only be called from the main thread.</remarks>
    public static void FindBestMove()
    {
        WasSearchAborted = false;
        MainLine = new();
        if (ResetTTOnEachSearch) TT.Clear();
        s_searchStopwatch.Restart();

        // Since this function is called on the main thread, t_board stores the initial board.
        Board initialBoard = t_board;

        s_threads = new ThreadInfo[s_threadCount];

        // Start searching on every thread
        for (int i = 0; i < s_threadCount; i++)
        {
            // Clone the initial board by creating a new board and applying its fen string to it.
            Task.Factory.StartNew(() => StartSearching(ConvertFromFen(new(), GetCurrentFen(initialBoard))));
        }

        s_abortSearchTimer = new();
        if (s_useTimeLimit) 
            Task.Delay(s_timeLimit, s_abortSearchTimer.Token).ContinueWith((t) => AbortSearch());
    }

    public static void AbortSearch() =>
        WasSearchAborted = true;

    private static void FinishSearch()
    {
        s_abortSearchTimer?.Cancel();
        WasSearchAborted = false;

        UCI.Bestmove(MainLine?.Move.ToString() ?? "");
    }

    /// <summary>Start searching on the current thread.</summary>
    /// <remarks>Other threads may be activated depending on the on <see cref="s_threadCount"/>.</remarks>
    /// <param name="board">The starting position given to all threads.</param>
    /// <param name="isMainSearchThread">UI search updates will only be sent if isMainThread is true.</param>
    private static void StartSearching(Board board)
    {
#if DEBUG
        EraseLog();
#endif

        ThreadInfo threadInfo = new();

        t_board = board;

        t_totalSearchNodes = 0;

        int evaluation = Null;

        // Start at depth 1 and increase it until the search is stopped.
        int depth = 1;
        do
        {
            t_rootDepth = depth;

            // Do a full depth search for the first s_multiPvCount root moves.
            // After a move is searched it will be added to s_excludedRootMoves.
            s_excludedRootMoves = new();
            for (int pvIndex = 0; pvIndex < s_multiPvCount; pvIndex++)
            {
                t_maxDepthReached = 0;

                // After the first search (depth 1), the next searches will shrinken the windows to be close to the returned score.
                // These are the initial window sizes.
                int alphaWindow = 25;
                int betaWindow = 25;

                int alpha = -Infinity;
                int beta = Infinity;

                if (depth >= 2)
                {
                    alpha = evaluation - alphaWindow;
                    beta = evaluation + betaWindow;
                }


                int failedHighCounter = 0;
                while (true)
                {
                    // Reset on each iteration because of better performance.
                    // This behaviour is not expected and further research is required.
                    ResetQuietMoveStats();


                    Node root = new();
                    ref int score = ref root.Score;

#if DEBUG
                    WriteLog($"depth {depth}");
                    WriteLog("");
#endif

                    score = Search(root, depth - failedHighCounter, alpha, beta, out threadInfo.MainLine);
                    bool isUpperbound = score <= alpha;

#if DEBUG
                    WriteLog("");
                    WriteLog($"score {score}, nodes {t_totalSearchNodes}");
                    WriteLog("");
                    WriteLog("");
#endif

                    // If the score is outside the bounds, the search failed and a new search with wider bounds will be performed.
                    bool searchFailed = false;
                    if (score <= alpha)
                    {
                        alphaWindow *= s_aspirationWindowsMultipliers[0];
                        searchFailed = true;
                    }

                    if (score >= beta)
                    {
                        betaWindow *= s_aspirationWindowsMultipliers[1];
                        searchFailed = true;
                        failedHighCounter++;
                    }

                    alpha = Max(score - alphaWindow, -Infinity);
                    beta = Min(score + betaWindow, Infinity);

                    evaluation = score;

                    if (!searchFailed || WasSearchAborted) break;

                    // Update the UI when the search fails.
                    else if (t_isMainSearchThread && searchFailed && s_searchStopwatch.ElapsedMilliseconds > 3000)
                        UCI.PV(
                            depth: depth,
                            seldepth: t_maxDepthReached + 1,
                            multipv: pvIndex + 1,
                            evaluation: evaluation,
                            evaluationType: isUpperbound ? "upperbound" : "lowerbound",
                            nodes: t_totalSearchNodes,
                            nps: (int)Round(t_totalSearchNodes / (s_searchStopwatch.ElapsedMilliseconds / 1000f)),
                            time: s_searchStopwatch.ElapsedMilliseconds,
                            pv: MainLine.ToString());
                }

                if (t_isMainSearchThread && !WasSearchAborted)
                {
                    MainLine = new(threadInfo.MainLine);

                    UCI.PV(
                        depth: depth,
                        seldepth: t_maxDepthReached + 1,
                        multipv: pvIndex + 1,
                        evaluation: evaluation,
                        evaluationType: "",
                        nodes: t_totalSearchNodes,
                        nps: (int)Round(t_totalSearchNodes / (double)s_searchStopwatch.ElapsedMilliseconds * 1000),
                        time: s_searchStopwatch.ElapsedMilliseconds,
                        pv: MainLine.ToString());

                        s_excludedRootMoves.Add(MainLine?.Move ?? NullMove());
                }

                else break;

                // If all legal moves have been searched, exit the multi pv loop immediately.
                if (s_excludedRootMoves.Count >= t_board.GenerateAllLegalMoves().Count) break;
            }

            depth++;
        }
        while
        (!WasSearchAborted && depth < MaxPly &&
        (!s_useTimeManagement || s_searchStopwatch.ElapsedMilliseconds <= s_optimumTime) &&
        (!s_useDepthLimit || depth <= s_depthLimit));

        s_searchStopwatch.Stop();
        FinishSearch();
    }


    /// <summary>
    /// The <see cref="Search"/> function goes through every legal move,
    /// then recursively calls itself on each of the opponent's responses
    /// until the depth reaches 0. Finally, the positions reached are evaluated. <br />
    /// The path that leads to the position with the best evaluation is then chosen. <br />
    /// The greater the depth, the further into the future the computer will be able to see,
    /// possibly finding more advanced tactics and better moves.
    /// </summary>
    /// <remarks>
    /// Terms such as evaluation, value and score all refer to the predicted quality of a position:
    /// 'evaluation' (similarly to 'value' or 'eval') is used to describe approximations of the score, 
    /// such as the values returned by the Evaluate() function, whereas 'score' is the value returned 
    /// by the search function, which is more accurate as future moves were taken into consideration as well.
    /// </remarks>
    /// <param name="depth">The remaining depth to search before evaluating the positions reached.</param>
    /// <param name="alpha">The lower bound of the evaluation. Inside the search, only values above 
    /// alpha will be considered. If alpha is returned because moves reached the minimum score, 
    /// the search will fail-low and the actual score may be lower.</param>
    /// <param name="beta">The upper bound of the evaluation. If a value greater or equal to beta 
    /// is found, the search will fail-high and the actual score may be higher.</param>
    private static int Search(Node node, int depth, int alpha, int beta, out Line pvLine, bool useNullMovePruning = true, ulong previousCapture = 0, bool useMultiCut = true)
    {
        int ply = node.Ply;

        bool rootNode = ply == 0;

        // pvLine == null -> branch was pruned.
        // pvLine == empty -> the node is an All-Node.
        pvLine = null;


        if (WasSearchAborted) return Null;

        // Check for a draw, but never return early at the root.
        if (!rootNode && IsDrawByRepetition()) return Draw;

        if (!rootNode && IsDrawByInsufficientMaterial()) return Draw;


        // Return the static evaluation immediately if the max ply was reached.
        if (ply >= MaxPly) return Evaluate(t_board, out int _);


        // Once the depth reaches 0, keep searching until no more captures
        // are available using the QuiescenceSearch function.
        if (depth <= 0)
            return QuiescenceSearch(node, alpha, beta, out pvLine);

        // Keep track of the highest depth reached this search.
        if (ply > t_maxDepthReached) t_maxDepthReached = ply;


        // Mate Distance Pruning:
        // If a forced checkmate was found at a lower ply, prune this branch.
        if (MateDistancePruning()) return alpha;


        #region Lookup Transposition Data
        // Was this position searched before?
        bool ttHit;

        // Lookup transposition table entry. Should be accessed only if ttHit is true.
        TTEntry ttEntry = TT.GetStoredEntry(t_board, out ttHit);

        // Lookup transposition evaluation.
        int ttEval = TT.GetStoredEvaluation(ply, alpha, beta);

        // Store the best move found when this position was previously searched.
        Line? ttLine = ttHit ? ttEntry.Line : null;
        Move ttMove = ttLine?.Move ?? NullMove();
        bool ttMoveIsCapture = ttMove.IsCapture(t_board);
        #endregion


        // Early Transposition Table Cutoff:
        // If the current position has been evaluated before at a depth
        // greater or equal the current depth, return the stored value.
        if (TTCutoff(out pvLine)) return ttEval;


        pvLine = new();


        #region Store Static Evaluation Data
        // Early pruning is disabled when in check.
        bool inCheck = t_board.IsInCheck(t_board.Friendly);

        // Evaluation of the current position.
        ref int staticEvaluation = ref node.StaticEvaluation;
        bool hasStaticEvaluationImproved;

        // Approximation of the actual evaluation.
        // Found using the transposition table in case of a ttHit, otherwise evaluation = staticEvaluation.
        // Note: based on results of lower depth searches. 
        ref int evaluation = ref node.Evaluation;

        StoreStaticEvaluation(ref staticEvaluation, ref evaluation);
        #endregion


        #region Early Pruning
        // Razoring:
        // If the evaluation is too much lower than beta, jump straight into the quiescence search.
        // Inspired by Strelka: https://www.chessprogramming.org/Razoring#Strelka.
        // As implemented in Wukong JS: https://github.com/maksimKorzh/wukongJS/blob/main/wukong.js#L1575-L1591.
        if (Razoring(ref evaluation, out int razoringScore)) return razoringScore;


        // Futility Pruning:
        // When close to the horizon, if it's unlikely that alpha will be raised, most moves are skipped.
        // For more details: https://www.chessprogramming.org/Futility_Pruning.
        bool useFutilityPruning = UseFutilityPruning(ref evaluation);
        

        // Null Move Pruning:
        // Explained here: https://www.chessprogramming.org/Null_Move_Pruning.
        if (NullMovePruning(evaluation, staticEvaluation, out int nullMovePruningScore)) return nullMovePruningScore;


        if (ProbCut(ref staticEvaluation, ref pvLine, out int probCutScore)) return probCutScore;
        #endregion

        // If the position is not in the transposition table insert it by
        // performing a reduced depth search, for better move ordering.
        InternalIterativeDeepening();


        // Generate a list of all the moves currently available.
        var moves = t_board.GenerateAllLegalMoves();

        // If no legal moves were found, it's either
        // checkmate for the current player or stalemate.
        if (moves.Count == 0)
        {
            // Checkmate.
            if (t_board.IsInCheck(t_board.Friendly)) return MatedIn(ply);

            // Stalemate.
            else return Draw;
        }

        // Rearrange the moves list to reach better moves earlier,
        // hoping for a beta-cutoff to occur as soon as possible.
        OrderMoves(moves, ply);

#if DEBUG
        WriteLog($"movelist at depth {depth}, ply {ply}, nodes {t_totalSearchNodes}, fen {GetCurrentFen(t_board)}: {string.Join(", ", moves)}");
#endif


        // The new depth may receive depth extensions, so by separating it from
        // the initial depth we can keep using that to make decisions on pruning.
        int newDepth = depth - 1;

        // Extend the search when in check.
        if (CanExtend() && depth >= 9 && inCheck) newDepth++;

        // Extend if only one legal move is available.
        // TODO: Further research needed. May need to limit one reply extensions to only when in check.
        //if (CanExtend() && moves.Count == 1) newDepth++;


        // The evaluation type shows how the score returned by the search compares to the actual score.
        EvaluationType evaluationType = EvaluationType.UpperBound;

        bool bestMoveIsCapture = false;

        // Moves Loop:
        // Iterate through all legal moves and perform a depth - 1 search on each one.
        for (int i = 0; i < moves.Count; i++)
        {
            Move move = moves[i];

            // Skip PV lines that have already been explored.
            if (rootNode && s_excludedRootMoves.Any(m => m.Equals(move))) continue;

            // Update the UI on the search progress.
            if (t_isMainSearchThread && rootNode && s_searchStopwatch.ElapsedMilliseconds > 3000)
                UCI.Currmove(
                    depth: depth,
                    currmove: move.ToString(),
                    currmovenumber: i + 1);


            // Make the move on the board.
            // The move must be unmade before moving on to the next one.
            t_board.MakeMove(move, out int pieceType, out int capturedPieceType);


            // Store information on the move for pruning purposes.
            bool isCapture = capturedPieceType != None;
            bool isPromotion = move.PromotionPieceType != None;
            bool isCaptureOrPromotion = isCapture || isPromotion;

            bool givesCheck = t_board.IsInCheck(t_board.Friendly);


            // Futility Pruning:
            // When close to the horizon, skip quiet moves that don't give check.
            // Note: never prune the first move.
            if (FutilityPruning()) continue;


            // Late Move Pruning:
            // Skip quiet late moves at shallow depths.
            // As implemented in Stockfish: https://github.com/official-stockfish/Stockfish/blob/master/src/search.cpp#L1005.
            bool useLateMovePruning;
            if (LateMovePruning()) continue;


            // Update node count after pruning.
            t_totalSearchNodes++;

            // If the branch wasn't pruned, create a new child node.
            Node newNode = node.AddNewChild();
            ref int score = ref newNode.Score;

            // Depth reduction.
            int depthReduction = 0;

            bool usedLmr;

            // Reduce the depth of the next search if it is unlikely to reveal anything interesting.
            ReduceDepth();

            // Extend the depth of the next search if it is likely to be interesting.
            ExtendDepth();


            int lmrDepth = newDepth - depthReduction;

            // Permorm a search on the new position with a depth reduced by R.
            // The bounds need to be inverted (alpha = -beta, beta = -alpha), because what was previously the ceiling is now the score to beat, and viceversa.
            // The score needs to be negated, because the evaluation in the point of view of the opponent is opposite to ours.
            score = -Search(newNode, lmrDepth, -beta, -alpha, out Line nextLine);

            // In case late move reductions were used and the score exceeded alpha,
            // a re-search at full depth is needed to verify the score.
            if (usedLmr && lmrDepth < depth && score > alpha)
                score = -Search(newNode, depth - 1, -beta, -alpha, out nextLine);


#if DEBUG
            WriteLog($"depth {depth}, ply {ply}, move {i}: alpha {alpha}, beta {beta}, nodes {t_totalSearchNodes}, score {score} (fen {GetCurrentFen(t_board)})");
#endif

            // Unmake the move on the board.
            // This must be done before moving onto the next move.
            t_board.UnmakeMove(move);


            // If the search was aborted, don't return incorrect values.
            if (WasSearchAborted) return Null;

            // A new best move was found!
            if (score > alpha)
            {
                evaluationType = EvaluationType.Exact;

                alpha = score;

                pvLine.Move = move;
                pvLine.Next = nextLine;

                bestMoveIsCapture = isCapture;

                // Fail-High:
                // If the score is higher than beta, it means the move is too good for the
                // opponent and this node will be avoided. There's no need to look at any
                // other moves since we already know this one is worse than we can afford.
                if (score >= beta)
                {
                    // The score was limited to beta, thus the actual score may be higher.
                    evaluationType = EvaluationType.LowerBound;

                    TT.StoreEvaluation(t_board, depth, ply, beta, evaluationType, pvLine, staticEvaluation);

                    // If a quiet move caused a beta-cutoff, update it's stats.
                    if (!isCapture) UpdateQuietMoveStats(move, depth, ply);

                    return beta;
                }

                // When a new best move is found, reduce the depth of other moves.
                else if (depth > 1 && depth < 6 && !IsMateWinScore(beta) && !IsMateLossScore(alpha))
                    depth--;
            }


            bool FutilityPruning()
            {
                if (useFutilityPruning && i > 0 &&
                !isCaptureOrPromotion && !givesCheck)
                {
                    // It's essential to unmake moves when pruning inside the moves loop.
                    t_board.UnmakeMove(move);
                    return true;
                }

                return false;
            }

            bool LateMovePruning()
            {
                useLateMovePruning = !rootNode &&
                    depth < ShallowDepthThreshold &&
                    i > LateMovePruningThreshold(depth, hasStaticEvaluationImproved);

                if (useLateMovePruning &&
                    !isCaptureOrPromotion && !givesCheck)
                {
                    t_board.UnmakeMove(move);
                    return true;
                }

                return false;
            }


            void ReduceDepth()
            {
                // Late Move Reductions:
                // Reduce quiet moves towards the end of the ordered moves list.
                usedLmr = false;
                // Only reduce if we aren't in check and the move doesn't give check to the opponent.
                if (depth >= 2 && i > s_lateMoveThresholds[moves.Count] && !inCheck && !givesCheck)
                {
                    // Don't reduce captures, promotions and killer moves,
                    // unless we are past the moveCountBasedPruningThreshold (very late moves).
                    if (useLateMovePruning ||
                        (!isCaptureOrPromotion &&
                        !move.Equals(t_killerMoves[0, ply]) &&
                        !move.Equals(t_killerMoves[1, ply])))
                    {
                        usedLmr = true;

                        depthReduction +=
                            s_lateMoveDepthReductions[Min(depth, 63)][Min(i, 63)][hasStaticEvaluationImproved ? 1 : 0];
                    }
                }
            }

            void ExtendDepth()
            {
                // Capture extension.
                //if (move.CapturedPieceType != Piece.None)
                //    depthReduction--;

                // Passed pawn extension (when a pawn is pushed to the seventh rank).
                // Note: unexpectedly, passed pawn extensions actually decrease the amount of nodes searched.
                if (CanExtend() && pieceType == Pawn &&
                    ((move.TargetSquare & Mask.SeventhRanks) != 0))
                    depthReduction--;

                // Promotion extension.
                //if (CanExtend() && move.PromotionPiece != Piece.None)
                //    depthReduction--;
            }
        }


        // Store killer move in case the best move found is quiet, even if it didn't cause a beta-cutoff.
        if (!bestMoveIsCapture && !pvLine.Move.IsNullMove()) UpdateQuietMoveStats(pvLine.Move, depth, ply);

        // Once all legal moves have been searched, save the best score found in the transposition table and return it.
        TT.StoreEvaluation(t_board, depth, ply, alpha, evaluationType, pvLine, staticEvaluation);

        return alpha;


        bool MateDistancePruning()
        {
            if (!rootNode)
            {
                // The worst possible outcome is that the player is currently in checkmate.
                alpha = Max(alpha, MatedIn(ply));

                // The best case scenario is that we deliver a mate on the next move.
                beta = Min(beta, MateIn(ply + 1));

                // Prune if a shorter mating sequence was found.
                if (alpha >= beta) return true;
            }

            return false;
        }

        bool TTCutoff(out Line pvLine)
        {
            pvLine = null;

            if (!rootNode && ttHit && ttEntry.Depth >= depth && ttEval != Null)
            {
                // Update quiet move stats.
                if (!ttMove.IsNullMove() /* BUG: ttMove is sometimes null even though ttHit is true. Somewhere in the code entries are being saved without a best move. Not sure whether or not this should ever be done. */&&
                    !ttMoveIsCapture)
                {
                    if (ttEval >= beta)
                    {
                    t_historyHeuristics[t_board.Friendly][ttMove.StartSquareIndex, ttMove.TargetSquareIndex] += depth * depth;

                        StoreKillerMove(ttMove, ply);
                    }
                }

                pvLine = TT.GetStoredLine();
                return true;
            }

            return false;
        }

        void StoreStaticEvaluation(ref int staticEvaluation, ref int evaluation)
        {
            // When in check, early pruning is disabled,
            // so the static evaluation is not used.
            if (inCheck) staticEvaluation = evaluation = Null;

            // If this position was already evaluated, use the stored value.
            else if (ttHit)
            {
                if (ttEntry.StaticEvaluation != Null)
                    staticEvaluation = ttEntry.StaticEvaluation;

                else staticEvaluation = Evaluate(t_board, out int _);

                if (ttEval != Null) evaluation = ttEval;
                else evaluation = staticEvaluation;
            }

            // If this is the first time this position is encountered,
            // calculate the static evaluation.
            else
            {
                staticEvaluation = evaluation = Evaluate(t_board, out int _);

                // TODO: Save static evaluation in the transposition table.
                //TT.StoreEvaluation(Null, Null, LookupFailed, Null, null, staticEvaluation);
            }

            hasStaticEvaluationImproved = !inCheck && (node.Grandparent == null || node.Grandparent.StaticEvaluation == Null ||
            staticEvaluation > node.Grandparent.StaticEvaluation);
        }


        bool Razoring(ref int evaluation, out int razoringScore)
        {
            if (!rootNode && !inCheck)
            {
                int score = evaluation + StaticPieceValues[Pawn][0];

                // If the evaluation is too much lower than beta,
                // either return the quiescence search score immediately at depth 1
                // or verify it first at depths up to 3.
                if (score < beta)
                {
                    if (depth == 1)
                    {
                        int newScore = QuiescenceSearch(node, alpha, beta, out Line _);

                        razoringScore = Max(newScore, score);

                        return true;
                    }

                    // Increase margin for higher depths.
                    score += StaticPieceValues[Pawn][0];

                    if (score < beta && depth <= 3)
                    {
                        int newScore = QuiescenceSearch(node, alpha, beta, out Line _);

                        // Verify the new score before returning it.
                        if (newScore < beta)
                        {
                            razoringScore = Max(newScore, score);

                            return true;
                        }
                    }
                }
            }

            razoringScore = Null;
            return false;
        }

        bool UseFutilityPruning(ref int evaluation)
        {
            if (!rootNode && !inCheck && depth <= 3)
            {
                // Should also check if eval is a mate score, 
                // otherwise the engine will be blind to certain checkmates.

                if (evaluation + s_futilityMargin[hasStaticEvaluationImproved ? 1 : 0][Min(depth, 63)] <= alpha)
                {
                    return true;
                }
            }

            return false;
        }

        bool NullMovePruning(int evaluation, int staticEvaluation, out int nullMovePruningScore)
        {
            nullMovePruningScore = Null;

            // Values and implementation are from Stockfish.
            if (!rootNode && !inCheck && useNullMovePruning && 
                evaluation >= beta && evaluation >= staticEvaluation)
            {
                t_board.MakeNullMove();


                Node newNode = node.AddNewChild();
                ref int score = ref newNode.Score;

                // The depth reduction depends on the static evaluation and depth.
                int depthReduction = Min((evaluation - beta) / 168, 7) + depth / 3 + 3;

                // Perform a null-window search, since we are only interested to know if the score can reach beta.
                // For more information, see: https://www.chessprogramming.org/Null_Window.
                score = -Search(newNode, depth - depthReduction, -beta, -beta + 1, out Line _, useNullMovePruning: false);


                t_board.UnmakeNullMove();

                if (WasSearchAborted) return false;
                if (score >= beta)
                {
                    // Avoid returning unproven wins.
                    if (IsMateScore(score)) score = beta;

                    nullMovePruningScore = score;
                    return true;
                }
            }

            return false;
        }

        bool ProbCut(ref int staticEvaluation, ref Line pvLine, out int probCutScore)
        {
            probCutScore = Null;

            // Following the Stockfish implementation.
            int probCutBeta = beta + 168 - (hasStaticEvaluationImproved ? 61 : 0);

            // Note: Stockfish adds the condition !(ttHit && ttEntry.Depth >= depth - 3 && ttEval != Null && ttEval < probCutBeta).
            // Adding it makes the search significantly slower, so it's currently avoided. This shouldn't have an effect on functionality.
            if (!rootNode && depth > ProbCutDepthReduction && !IsMateScore(beta))
            {
                // Note: should only generate moves with SEE score > probCutBeta - staticEvaluation.
                var moves = t_board.GenerateAllLegalMoves(capturesOnly: true);
                OrderMoves(moves, -1);

                for (int i = 0; i < moves.Count; i++)
                {
                    Move move = moves[i];

                    t_board.MakeMove(move, out int _, out int _);

                    Node newNode = node.AddNewChild();

                    // Perform a preliminary qsearch to verify that the move holds.
                    probCutScore = -QuiescenceSearch(newNode, -probCutBeta, -probCutBeta + 1, out Line probCutLine);

                    // If the qsearch held, perform the regular search.
                    if (probCutScore >= probCutBeta)
                    {
                        probCutScore = -Search(newNode, depth - ProbCutDepthReduction, -probCutBeta, -probCutBeta + 1, out probCutLine);
                    }

                    t_board.UnmakeMove(move);

                    if (probCutScore >= probCutBeta)
                    {
                        pvLine.Move = move;
                        pvLine.Next = probCutLine;

                        // Save ProbCut data into transposition table.
                        TT.StoreEvaluation(t_board, depth - (ProbCutDepthReduction - 1 /* Here the effective depth is 1 higher than the reduced prob cut depth. */),
                            ply, probCutScore, EvaluationType.LowerBound, pvLine, staticEvaluation);

                        return true;
                    }
                }
            }

            return false;
        }


        void InternalIterativeDeepening()
        {
            // If no move is stored in the transposition table for this position,
            // perform a reduced depth search and update the transposition values.
            if (!rootNode && ttMove.IsNullMove() &&
                depth > InternalIterativeDeepeningDepthReduction)
            {
                ref int score = ref node.Score;
                score = Search(node, depth - InternalIterativeDeepeningDepthReduction, alpha, beta, out Line _);

                ttEntry = TT.GetStoredEntry(t_board, out ttHit);
                ttEval = TT.GetStoredEvaluation(ply, alpha, beta);

                ttLine = ttHit ? ttEntry.Line : null;
                ttMove = ttLine?.Move ?? NullMove();
                ttMoveIsCapture= ttMove.IsCapture(t_board);
            }
        }


        bool CanExtend() => !rootNode && ply < t_rootDepth * 2;
    }

    /// <summary>
    /// The QuiescenceSearch function extends the normal Search, evaluating all legal captures. <br />
    /// For more information, see <see href="https://www.chessprogramming.org/Quiescence_Search"/>.
    /// </summary>
    private static int QuiescenceSearch(Node node, int alpha, int beta, out Line pvLine)
    {
        int ply = node.Ply;

        // pvLine == null -> branch was pruned.
        // pvLine == empty -> node is an All-Node.
        pvLine = null;


        if (WasSearchAborted) return Null;

        // Note: draws by repetition are not possible when all moves are captures.

        if (IsDrawByInsufficientMaterial()) return Draw;


        // Return the static evaluation immediately if the max ply was reached.
        if (WasMaxPlyReached()) return Evaluate(t_board, out int _);


        //if (ply > SelectiveDepth) SelectiveDepth = ply;


        #region Store Transposition Data
        // Was this position searched before?
        bool ttHit;

        // Store transposition table entry. To be accessed only if ttHit is true.
        TTEntry ttEntry = TT.GetStoredEntry(t_board, out ttHit);

        // Lookup transposition evaluation. If the lookup fails, ttEval == LookupFailed.
        int ttEval = TT.GetStoredEvaluation(ply, alpha, beta);

        // Store the best move found when this position was previously searched.
        Line ttLine = ttHit ? ttEntry.Line : null;
        Move ttMove = ttLine?.Move ?? NullMove();
        bool ttMoveIsCapture = ttMove.IsCapture(t_board);
        #endregion


        // Early Transposition Table Cutoff:
        // If the current position has been evaluated before at a depth
        // greater or equal the current depth, return the stored value.
        if (TTCutoff(out pvLine)) return ttEval;


        pvLine = new();


        #region Store Static Evaluation Data
        // Early pruning is disabled when in check.
        bool inCheck = t_board.IsInCheck(t_board.Friendly);

        // Evaluation of the current position.
        int staticEvaluation;

        // Approximation of the actual evaluation.
        // Found using the transposition table in case of a ttHit, otherwise evaluation = staticEvaluation.
        // Note: might use results of lower depth searches. 
        int evaluation;

        StoreStaticEvaluation();
        #endregion


        // Standing Pat:
        // Stop the search immediately if the evaluation estimate is above beta.
        // Based on the assumpion that there's at least one move that will improve the position,
        // so unless the position is a Zugzwang (which is a pretty rare case)
        // there's no way for the actual evaluation to be below beta.
        // For more information: https://www.chessprogramming.org/Quiescence_Search#Standing_Pat.
        if (evaluation >= beta)
        {
            // TODO: Save the static evaluation in the transposition table.

            return beta;
        }

        // Set the lower bound to the static evaluation.
        // Based on the assumption that the position is not Zugzwang
        // (https://www.chessprogramming.org/Quiescence_Search#Standing_Pat).
        // Note: this must always be done, otherwise if no captures are available the score returned may be invalid.
        if (alpha < evaluation) alpha = evaluation;


        var moves = t_board.GenerateAllLegalMoves(capturesOnly: true);

        // Note: checking for checkmate or stalemate here is not
        // possible because not all legal moves were generated.

        OrderMoves(moves, -1);


        EvaluationType evaluationType = EvaluationType.UpperBound;

        for (int i = 0; i < moves.Count; i++)
        {
            Move move = moves[i];

            t_board.MakeMove(move, out int _, out int _);


            t_totalSearchNodes++;

            Node newNode = node.AddNewChild();
            ref int score = ref newNode.Score;

            score = -QuiescenceSearch(newNode, -beta, -alpha, out Line nextLine);


#if DEBUG
            WriteLog($"quiesce, ply {ply}, move {i}: alpha {alpha}, beta {beta}, nodes {t_totalSearchNodes}, score {score} (fen {GetCurrentFen(t_board)})");
#endif

            t_board.UnmakeMove(move);


            // A new best move was found!
            if (score > alpha)
            {
                evaluationType = EvaluationType.Exact;

                alpha = score;

                pvLine.Move = move;
                pvLine.Next = nextLine;

                if (score >= beta)
                {
                    evaluationType = EvaluationType.LowerBound;

                    TT.StoreEvaluation(t_board, 0, ply, beta, evaluationType, pvLine, staticEvaluation);

                    return beta;
                }
            }
        }


        TT.StoreEvaluation(t_board, 0, ply, alpha, evaluationType, pvLine, staticEvaluation);
        return alpha;


        bool TTCutoff(out Line pvLine)
        {
            pvLine = null;

            if (ttHit && ttEval != Null)
            {
                pvLine = TT.GetStoredLine();
                return true;
            }

            return false;
        }

        void StoreStaticEvaluation()
        {
            // When in check, early pruning is disabled,
            // so the static evaluation is not used.
            if (inCheck)
            {
                staticEvaluation = Null;

                // In quiescence search, the evaluation must always be set to the current eval to avoid returning -Infinity if no captures are available.
                // Note: the implementation is different in Stockfish, where evaluation is set to -Infinity here,
                // and after the moves loop if no moves where found and the player is in check checkmate is returned.
                // I suspect their move generation still generates all legal moves when in check even inside qsearch.
                evaluation = Evaluate(t_board, out int _);
            }

            // If this position was already evaluated, use the stored value.
            else if (ttHit)
            {
                if (ttEntry.StaticEvaluation != Null)
                    staticEvaluation = ttEntry.StaticEvaluation;

                else staticEvaluation = Evaluate(t_board, out int _);

                if (ttEval != Null) evaluation = ttEval;
                else evaluation = staticEvaluation;
            }

            // If this is the first time this position is encountered,
            // calculate the static evaluation.
            else
            {
                staticEvaluation = evaluation = Evaluate(t_board, out int _);

                // TODO: Save static evaluation in the transposition table.
                //TT.StoreEvaluation(Null, Null, LookupFailed, Null, null, staticEvaluation);
            }
        }

        bool WasMaxPlyReached()
        {
            if (ply >= MaxPly)
            {
                return true;
            }

            return false;
        }
    }


    private static void StoreKillerMove(Move move, int ply)
    {
        if (!t_killerMoves[0, ply].IsNullMove() && !t_killerMoves[0, ply].Equals(move))
            t_killerMoves[1, ply] = t_killerMoves[0, ply];

        t_killerMoves[0, ply] = move;

#if DEBUG
        WriteLog($"new killer: {move}");
#endif
    }

    private static void UpdateQuietMoveStats(Move move, int depth, int ply)
    {
        // Moves with the same start and target square will be boosted in move ordering.
        t_historyHeuristics[t_board.Friendly][FirstSquareIndex(move.StartSquare), FirstSquareIndex(move.TargetSquare)] += depth * depth;

        // If the same move is available in a different position, it will be prioritized.
        StoreKillerMove(move, ply);
    }


    /// <summary>
    /// Rearrange the moves in <paramref name="moves"/> based on the likelihood of each move being 
    /// strong in the current position. <br /> If better moves are listed first, it's more likely 
    /// to get an early beta-cutoff (<see href="https://www.chessprogramming.org/Beta-Cutoff"/>)
    /// </summary>
    /// <param name="ply">Current ply in the search tree. Used to compare killer moves.</param>
    private static void OrderMoves(List<Move> moves, int ply)
    {
        // Save time by returning early if there aren't multiple moves to sort.
        if (moves.Count < 2) return;

        // If this position was searched before,
        // ttMove stores the best move that was previously found.
        // This move should be given the top priority.
        // Note: may be null in case the position wasn't searched before.
        Move ttMove = TT.GetStoredMove();


        List<int> scores = new();
        foreach (Move move in moves)
        {
            int moveFlag = move.Flag;
            bool isEnPassant = moveFlag == EnPassantCaptureFlag;

            int startSquareIndex = move.StartSquareIndex;
            int targetSquareIndex = move.TargetSquareIndex;

            int pieceType = t_board.PieceType(startSquareIndex);
            int capturedPieceType = isEnPassant ? Pawn : t_board.PieceType(targetSquareIndex);

            bool isCapture = capturedPieceType != None;

            // Moves with a higher score are more likely to cause a beta-cutoff,
            // speeding up the search, so they will be searched first.
            int moveScore = 0;

            // Give the highest priority to the best move that was previosly found.
            if (move.Equals(ttMove)) moveScore += 30000;

            // Captures are sorted with a high priority.
            else if (isCapture)
            {
                // Captures are sorted using MVV-LVA (Most Valuable Victim - Least Valuable Attacker).
                // A weak piece capturing a strong one will be given a
                // higher priority than a strong piece capturing a weak one.
                moveScore += MvvLva[pieceType][capturedPieceType];
            }

            // Quiet moves are sorted with a low priority, with the exception of killer moves.
            else
            {
                // Sort killer moves just below captures.
                if (move.Equals(t_killerMoves[0, ply])) moveScore += 10000;
                else if (move.Equals(t_killerMoves[1, ply])) moveScore += 9000;

                // Sort non-killer quiet moves by history heuristics.
                else
                {
                    moveScore += t_historyHeuristics[t_board.Friendly][startSquareIndex, targetSquareIndex];

                    //moveScoreGuess += PieceSquareTables.Read(move.PromotionPiece == None ? move.PieceType : move.PromotionPiece, targetSquareIndex, Board.Friendly == 0, gamePhase);
                    //moveScore += GetPieceValue(move.PromotionPiece);
                    //if (Board.PawnAttackersTo(targetSquareIndex, Board.Friendly, Board.OpponentTurn) != 0) moveScore -= 350;
                }
            }

            scores.Add(moveScore);
        }

        // Sort the moves based on scores
        for (int i = 0; i < moves.Count - 1; i++)
        {
            for (int j = i + 1; j > 0; j--)
            {
                int swapIndex = j - 1;
                if (scores[swapIndex] < scores[j])
                {
                    (moves[j], moves[swapIndex]) = (moves[swapIndex], moves[j]);
                    (scores[j], scores[swapIndex]) = (scores[swapIndex], scores[j]);
                }
            }
        }
    }

    private static void ResetQuietMoveStats()
    {
        t_killerMoves = new Move[2, MaxPly];
        t_historyHeuristics = new[] { new int[64, 64], new int[64, 64] };
    }
    

    /// <summary>Returns true if the given score is a forced checkmate (positive or negative).</summary>
    public static bool IsMateScore(int score)
    {
        return score != Null && Abs(score) >= Checkmate - MaxPly;
    }

    /// <summary>Returns true if the given score is a positive forced checkmate.</summary>
    public static bool IsMateWinScore(int score)
    {
        return score != Null && score >= Checkmate - MaxPly;
    }

    /// <summary>Returns true if the given score is a negative forced checkmate.</summary>
    public static bool IsMateLossScore(int score)
    {
        return score != Null && score <= -Checkmate + MaxPly;
    }

    public static int MateIn(int ply) => Checkmate - ply;

    public static int MatedIn(int ply) => -Checkmate + ply;


    /// <summary>If a position is reached three times, it's a draw.</summary>
    /// <remarks>The current implementation returns true even if the position was only reached twice.</remarks>
    public static bool IsDrawByRepetition() => t_board.PositionHistory.Count(other => other.ZobristKey == t_board.ZobristKey) >= 2;


    /// <summary>If there is not enough material on the board for either player to checkate the opponent, it's a draw.</summary>
    public static bool IsDrawByInsufficientMaterial()
    {
        int whitePieceCount = PieceCount(t_board.OccupiedSquares[0]);
        int blackPieceCount = PieceCount(t_board.OccupiedSquares[1]);

        // It's not a draw if a player has more than 2 pieces.
        if (whitePieceCount > 2 || blackPieceCount > 2) return false;

        // King vs king.
        if (whitePieceCount == 1 && blackPieceCount == 1) return true;


        int whiteKnightCount = PieceCount(t_board.Knights[0]);
        int blackKnightCount = PieceCount(t_board.Knights[1]);

        // King and knight vs king.
        if (whiteKnightCount == 1 && blackPieceCount == 1) return true;
        if (blackKnightCount == 1 && whitePieceCount == 1) return true;


        int whiteBishopCount = PieceCount(t_board.Bishops[0]);
        int blackBishopCount = PieceCount(t_board.Bishops[1]);

        // King and bishop vs king.
        if (whiteBishopCount == 1 && blackPieceCount == 1) return true;
        if (blackBishopCount == 1 && whitePieceCount == 1) return true;

        // King and bishop vs king and bishop with bishops of the same color.
        if (whitePieceCount == 2 && whiteBishopCount == 1 &&
            blackPieceCount == 2 && blackBishopCount == 1)
        {
            if ((t_board.Bishops[0] & Mask.LightSquares) != 0 && (t_board.Bishops[1] & Mask.LightSquares) != 0) return true;
            if ((t_board.Bishops[0] & Mask.DarkSquares) != 0 && (t_board.Bishops[1] & Mask.DarkSquares) != 0) return true;
        }

        return false;
    }


    public static Dictionary<int, Dictionary<int, int>> MvvLva = new()
    {
        [Pawn] = new()
        {
            [Pawn] = 10105,
            [Knight] = 10205,
            [Bishop] = 10305,
            [Rook] = 10405,
            [Queen] = 10505,
        },

        [Knight] = new()
        {
            [Pawn] = 10104,
            [Knight] = 10204,
            [Bishop] = 10304,
            [Rook] = 10404,
            [Queen] = 10504,
        },

        [Bishop] = new()
        {
            [Pawn] = 10103,
            [Knight] = 10203,
            [Bishop] = 10303,
            [Rook] = 10403,
            [Queen] = 10503,
        },

        [Rook] = new()
        {
            [Pawn] = 10102,
            [Knight] = 10202,
            [Bishop] = 10302,
            [Rook] = 10402,
            [Queen] = 10502,
        },

        [Queen] = new()
        {
            [Pawn] = 10101,
            [Knight] = 10201,
            [Bishop] = 10301,
            [Rook] = 10401,
            [Queen] = 10501,
        },

        [King] = new()
        {
            [Pawn] = 10100,
            [Knight] = 10200,
            [Bishop] = 10300,
            [Rook] = 10400,
            [Queen] = 10500,
        },
    };


    private static int LateMovePruningThreshold(int depth, bool improving)
    {
        // Values from Stockfish: https://github.com/official-stockfish/Stockfish/blob/master/src/search.cpp#L78-L81.
        
        if (improving) return 3 + depth * depth;
        else return (3 + depth * depth) / 2;
    }


    /// <summary>Stores information on the search progress and results of a thread, to be accessed from outside the thread.</summary>
    /// <remarks>Information that's only used inside the thread should not be stored here.</remarks>
    private class ThreadInfo
    {
        public Line MainLine = new();

        /// <summary>
        /// When a new <see cref="ThreadInfo"/> object is created, it's stored in the first available slot 
        /// of <see cref="s_threads"/> and ThreadStatic information on this thread is updated.
        /// </summary>
        public ThreadInfo()
        {
            for (int i = 0; i < s_threads.Length; i++)
            {
                // Store this ThreadInfo object in the first empty slot.
                if (s_threads[i] == null)
                {
                    t_isMainSearchThread = i == 0;

                    s_threads[i] = this;
                    break;
                }
            }
        }
    }
}

public class Line
{
    public Move Move;
    public Line? Next;

    public Line()
    {
        Move = NullMove();
        Next = null;
    }

    public Line(Move move, Line next = null)
    {
        Move = move;
        Next = next;
    }

    public Line(Line other)
    {
        if (other == null) return;
        Move = other.Move;
        Next = other.Next != null ? new(other.Next) : null;
    }

    public void MakeMoves(Board board, bool removeEntries = false)
    {
        if (!Move.IsNullMove()) board.MakeMove(Move, out int _, out int _);
        if (removeEntries) TT.ClearCurrentEntry();
        if (Next != null) Next.MakeMoves(board, removeEntries);
    }

    public void UnmakeMoves(Board board)
    {
        if (Next != null) Next.UnmakeMoves(board);
        if (!Move.IsNullMove()) board.UnmakeMove(Move);
    }

    public bool Equals(Line other)
    {
        return
            other != null &&
            Move.Equals(other.Move) &&
            (Next == null ? other.Next == null :
            Next.Equals(other.Next));
    }

    public override string ToString()
    {
        return $"{Move} {(Next != null ? Next.ToString() : "")}";
    }

    public int Length()
    {
        return 1 + (Next != null ? Next.Length() : 0);
    }
}


/// <summary>
/// The Node class holds all useful information about a node in the search tree.
/// Given a reference to the root node of a tree, the entire tree can be accessed.
/// </summary>
/// <remarks>
/// The search tree is made up of search nodes.
/// Each node represents a certain position on the board,
/// reached after a specific sequence of moves (called a line). <br />
/// The parent of a node is the node above it in the tree, closer to the root.
/// The children of a node are all the nodes reached after 1 move, the grandchildren after 2 moves and so on. <br />
/// Sibling nodes are nodes at the same distance from the root (or <c>ply</c>). <br />
/// Pruning a branch means returning the search early in a node, speeding up the search in the parent node.
/// </remarks>
public class Node
{
    /// <summary>Distance from the root node.</summary>
    public int Ply;

    /// <summary>The value returned by Evaluate() on the position of this node.</summary>
    public int StaticEvaluation;
    
    /// <summary>An evaluation estimate of the position. Often more accurate than the static evaluation.</summary>
    public int Evaluation;

    /// <summary>The score returned by the search function. Always the most accurate evaluation.</summary>
    /// <remarks>Not set for leaf nodes.</remarks>
    public int Score;

    /// <summary>The root of this node's tree.</summary>
    public Node Root;

    /// <summary>The node directly above this one in the tree.</summary>
    public Node? Parent;

    /// <summary>The nodes directly below this one in the tree.</summary>
    public List<Node> Children;


    /// <summary>Create a new root node.</summary>
    public Node()
    {
        Ply = 0;
        Root = this;
        Parent = null;
        Children = new();
    }

    /// <summary>Create a new child node.</summary>
    private Node(Node parent)
    {
        Ply = parent.Ply + 1;
        Root = parent.Root;
        Parent = parent;
        Children = new();
    }


    public Node Grandparent => Parent?.Parent;

    public List<Node> Grandchildren => Children.SelectMany(c => c.Children).ToList();


    public Node AddNewChild()
    {
        Node child = new(this);
        Children.Add(child);
        return child;
    }

    //public override string ToString()
    //{
    //    return
    //        $"{Ply}," +
    //        $"{Enum.GetName(typeof(SearchType), SearchType)}," +
    //        $"{Enum.GetName(typeof(NodeType), NodeType)}," +
    //        $"{(Children != null && Children.Count > 0 ? $"[{string.Join(";", Children)};]" : "[]")}";
    //}
}

public enum NodeType
{
    LeafNode,
    PVNode,
    CutNode,
    AllNode,
    PrunedNode,
    TTCutoffNode,
}

public enum SearchType
{
    Normal,
    LateMoveReductionsNormal,
    Quiescence,
    RazoringQuiescence,
    NullMovePruningNormal,
    ProbCutQuiescence,
    ProbCutNormal,
    InternalIterativeDeepeningNormal,
}
