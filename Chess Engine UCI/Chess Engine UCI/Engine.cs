using System.Diagnostics;
using static Utilities.Bitboard;
using static System.Math;
using static Piece;
using static Evaluation;

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


    /// <summary><see cref="Stopwatch"/> that keeps track of the total time taken by the current search.</summary>
    /// <remarks>Note that it is not reset on different iterations of iterative deepening.</remarks>
    private static readonly Stopwatch s_searchStopwatch = new();

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

    /// <summary>Depth reduction for late moves depending on the search depth and the current move number.</summary>
    /// <remarks>Later moves will receive greater depth reductions.</remarks>
    private static readonly int[][] s_lateMoveDepthReductions =
        Enumerable.Range(0, 64).Select(depth =>
        Enumerable.Range(0, 64).Select(moveNumber =>
        {
            if (depth == 0 || moveNumber == 0) return 0;

            return Max((int)Round(Log(depth) * Log(moveNumber) / 2f) - 1, 0);
        }
        ).ToArray()).ToArray();

    /// <summary>Killer moves are quiet moves that caused a beta cutoff, indexed by <c>[KillerMoveIndex, Ply]</c>.<br />
    /// If the same move is found in another position at the same ply, it will be prioritized.</summary>
    /// <remarks>2 killer moves are stored at each ply. Storing more would increase the complexity of adding a new move.</remarks>
    private static Move[,] s_killerMoves = new Move[2, MaxPly];

    private static int s_maxExtensions;

    private static Line? s_currentMainLine;

    private static CancellationTokenSource? s_abortSearchTimer;

    /// <summary>Bonus based on the success of a move in other positions.</summary>
    /// <remarks>Moves are identified using butterfly boards (https://www.chessprogramming.org/Butterfly_Boards) with [ColorIndex][StartSquareIndex, TargetSquareIndex].</remarks>
    private static int[][,] s_historyHeuristics = new[] { new int[64, 64], new int[64, 64] };

    private static int s_depthLimit;
    private static int s_timeLimit;
    private static int s_optimumTime;

    private static bool s_useDepthLimit;
    private static bool s_useTimeLimit;
    private static bool s_useTimeManagement;

    private static int s_maxDepthReached;


    private static int s_multiPvCount = 1;

    private static List<Move> s_excludedRootMoves = new();


    private static int s_totalSearchNodes;


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

    
    /// <summary>The total number of legal moves after each move in the current position and down the game tree until the desired depth is reached.</summary>
    /// <returns>A string with each root move on a new line and the legal move count after it. For example, "a2a4: 20".</returns>
    public static string PerftResults(int depth)
    {
        List<string> results = new();

        var moves = Board.GenerateAllLegalMoves();
        foreach (var move in moves)
        {
            Board.MakeMove(move);
            results.Add($"{move}: {Perft(depth - 1)}");
            Board.UnmakeMove(move);
        }

        return string.Join('\n', results);


        int Perft(int depth)
        {
            if (depth == 0) return 1;

            var moves = Board.GenerateAllLegalMoves();
            int numPositions = 0;
            foreach (var move in moves)
            {
                Board.MakeMove(move);
                numPositions += Perft(depth - 1);
                Board.UnmakeMove(move);
            }
            return numPositions;
        }
    }


    /// <summary>Start a new search using the last applied limits and store the best move in <see cref="MainLine"/>.</summary>
    public static void FindBestMove()
    {
        Task.Factory.StartNew(
            StartSearch, TaskCreationOptions.LongRunning);

        s_abortSearchTimer = new();
        if (s_useTimeLimit) Task.Delay(s_timeLimit, s_abortSearchTimer.Token).ContinueWith((t) => AbortSearch());
    }

    public static void AbortSearch()
    {
        WasSearchAborted = true;
    }

    public static void FinishSearch()
    {
        s_abortSearchTimer?.Cancel();

        Console.WriteLine($"bestmove {MainLine?.Move?.ToString() ?? ""}");
    }

    private static void StartSearch()
    {
        WasSearchAborted = false;

        s_totalSearchNodes = 0;

        MainLine = new();
        s_currentMainLine = new();

        if (ResetTTOnEachSearch) TT.Clear();

        // Opening book not available in UCI mode.

        s_searchStopwatch.Restart();

        int evaluation;

        // Start at depth 1 and increase it until the search is stopped.
        int depth = 1;
        do
        {
            // Do a full depth search for the first s_multiPvCount root moves.
            // After a move is searched it will be added to s_excludedRootMoves.
            s_excludedRootMoves = new();
            for (int pvIndex = 0; pvIndex < s_multiPvCount; pvIndex++)
            {
                s_maxDepthReached = 0;

                int alpha = -Infinity;
                int beta = Infinity;

                // After the first search (depth 1), the next searches will shrinken the windows to be close to the returned score.
                // These are the initial window sizes.
                int alphaWindow = 25;
                int betaWindow = 25;

                while (true)
                {
                    // Reset on each iteration because of better performance.
                    // This behaviour is not expected and further research is required.
                    ResetQuietMoveStats();

                    // Maximum amount of search extensions inside any given branch.
                    // s_maxExtensions = depth -> the SelDepth may be up to twice the depth.
                    // Further testing is needed to find the perfect value here.
                    s_maxExtensions = depth;


                    Node root = new();
                    ref int score = ref root.Score;

                    score = Search(root, depth, alpha, beta, out s_currentMainLine);

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
                    }

                    alpha = Max(score - alphaWindow, -Infinity);
                    beta = Min(score + betaWindow, Infinity);

                    evaluation = score;

                    if (!searchFailed || WasSearchAborted) break;
                }


                // The "Focused Search" technique is an idea I came up with that involves
                // performing a search at a reduced depth at the end of the main line found by the computer,
                // to get a more realistic representation of the final evaluation.
                // A new search is then done using these new values to "verify" the line,
                // and if a different move is returned the whole process is repeated.
                // For more information: https://www.reddit.com/r/ComputerChess/comments/1192dur/engine_optimization_idea_focused_search/?utm_source=share&utm_medium=web2x&context=3
                // Disabled because of inconsintent performance and results.
                // Further research will need to be done.
                #region Focused Search
                    if (s_currentMainLine != null && !s_currentMainLine.Cleanup())
                    {
                        //    // To make sure we are not overlooking some of the opponent's responses to our chosen line,
                        //    // do a new search at the end of the line with a reduced depth.
                        //    // The new results will be stored in the transposition table, and looked up
                        //    // by another full search done afterwards.
                        //    // This should not slow down the search because of the limited depth for the new search
                        //    // and the quick transposition lookup times.
                        //    // The process is repeated until the line is found to be the best even after the verification.
                        //
                        //    Line newMainLine;
                        //
                        //    while (true)
                        //    {
                        //        CurrentMainLine.MakeMoves(removeEntries: true);
                        //        Line test;
                        //        int eval = Search(depth / 2, 0, NegativeInfinity, PositiveInfinity, 0, evaluationData, out test);
                        //        CurrentMainLine.UnmakeMoves();
                        //
                        //        evaluation = Search(depth, 0, alpha, beta, 0, evaluationData, out newMainLine);
                        //
                        //        if (newMainLine != null && !newMainLine.Cleanup())
                        //        {
                        //            Debug.Log($"{CurrentMainLine.Length()} {newMainLine.Length()} {test.Length()}");
                        //
                        //            if (newMainLine.Move.Equals(CurrentMainLine.Move)) break;
                        //
                        //            CurrentMainLine = new(newMainLine);
                        //        }
                        //
                        //        else break;
                        //
                        //        Debug.Log($"Skipped move {CurrentMainLine.Move.ToString()} at depth {depth} with aspiration windows of ({alpha}, {beta}).");
                        //    }
                        //
                        //    CurrentMainLine = newMainLine;
                    }
                    #endregion

                if (!WasSearchAborted)
                {
                    MainLine = new(s_currentMainLine);

                    Console.WriteLine($"info " +
                        $"depth {depth} " +
                        $"seldepth {s_maxDepthReached + 1} " +
                        $"multipv {pvIndex + 1} " +
                        $"score " +
                            (!IsMateScore(evaluation) ?
                            $"cp {evaluation} " :
                            $"mate {((evaluation > 0) ? "+" : "-")}{Ceiling((Checkmate - Abs(evaluation)) / 2.0)} ") +
                        $"nodes {s_totalSearchNodes} " +
                        $"nps {(int)Round(s_totalSearchNodes / (double)s_searchStopwatch.ElapsedMilliseconds * 1000)} " +
                        $"time {s_searchStopwatch.ElapsedMilliseconds} " +
                        $"pv {MainLine}");

                    s_excludedRootMoves.Add(MainLine?.Move);
                }

                else break;

                // If all legal moves have been searched, exit the multi pv loop immediately.
                if (s_excludedRootMoves.Count >= Board.GenerateAllLegalMoves().Count) break;
            }

            depth++;
        }
        while
        (!WasSearchAborted &&
        (!s_useTimeManagement || s_searchStopwatch.ElapsedMilliseconds <= s_optimumTime) &&
        (!s_useDepthLimit || depth <= s_depthLimit));

        s_searchStopwatch.Stop();
        FinishSearch();
    }


    /// <summary>
    /// The <see cref="Search"/> function goes through every legal move,
    /// then recursively calls itself on each of the opponent's responses
    /// until the depth reaches 0. <br /> Finally, the positions reached are evaluated.
    /// The path that leads to the best "forced evaluation" is then chosen. <br />
    /// The greater the depth, the further into the future the computer will be able to see,
    /// possibly finding more advanced tactics and better moves.
    /// </summary>
    /// <remarks>
    /// Terms such as evaluation, value and score all refer to the predicted quality of a position:
    /// 'evaluation' (similarly to 'value' or 'eval') is used to describe approximations of the score, such as the values returned 
    /// by the Evaluate() function, while 'score' is the value returned by the search function,
    /// which is more accurate as it looked into future positions as well.
    /// </remarks>
    /// <param name="depth">The remaining depth to search before evaluating the positions reached.</param>
    /// <param name="alpha">The lower bound of the evaluation</param>
    private static int Search(Node node, int depth, int alpha, int beta, out Line pvLine, bool useNullMovePruning = true, ulong previousCapture = 0, bool useMultiCut = true)
    {
        int ply = node.Ply;
        ref int extensions = ref node.Extensions;

        bool rootNode = ply == 0;

        // pvLine == null -> branch was pruned.
        // pvLine == empty -> the node is an All-Node.
        pvLine = null;


        if (WasSearchAborted) return Null;

        // Check for a draw, but never return early at the root.
        if (!rootNode && IsDrawByRepetition(Board.ZobristKey)) return Draw;

        if (!rootNode && IsDrawByInsufficientMaterial()) return Draw;


        // Return the static evaluation immediately if the max ply was reached.
        if (ply >= MaxPly) return Evaluate(out int _);


        // Once the depth reaches 0, keep searching until no more captures
        // are available using the QuiescenceSearch function.
        if (depth <= 0)
        {
            return QuiescenceSearch(node, alpha, beta, out pvLine);
        }

        // Keep track of the highest depth reached this search.
        if (ply > s_maxDepthReached) s_maxDepthReached = ply;


        // Mate Distance Pruning:
        // If a forced checkmate was found at a lower ply, prune this branch.
        if (MateDistancePruning()) return alpha;


        #region Lookup Transposition Data
        // Was this position searched before?
        bool ttHit;

        // Lookup transposition table entry. Should be accessed only if ttHit is true.
        TTEntry ttEntry = TT.GetStoredEntry(out ttHit);

        // Lookup transposition evaluation.
        int ttEval = TT.GetStoredEvaluation(ply);

        // Store the best move found when this position was previously searched.
        Line? ttLine = ttHit ? ttEntry.Line : null;
        Move? ttMove = ttLine?.Move;
        bool ttMoveIsCapture = IsCaptureOrPromotion(ttMove);
        #endregion


        // Early Transposition Table Cutoff:
        // If the current position has been evaluated before at a depth
        // greater or equal the current depth, return the stored value.
        if (TTCutoff(out pvLine)) return ttEval;


        pvLine = new();


        #region Store Static Evaluation Data
        // Early pruning is disabled when in check.
        bool inCheck = Board.IsKingInCheck[Board.CurrentTurn];

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
        if (NullMovePruning(ref evaluation, out int nullMovePruningScore)) return nullMovePruningScore;


        if (ProbCut(ref staticEvaluation, ref pvLine, out int probCutScore)) return probCutScore;
        #endregion

        // If the position is not in the transposition table,
        // save it with a reduced depth search for better move ordering.
        InternalIterativeDeepening();


        // Generate a list of all the moves currently available.
        var moves = Board.GenerateAllLegalMoves();


        // If no legal moves were found, it's either
        // checkmate for the current player or stalemate.
        if (moves.Count == 0)
        {
            // Checkmate.
            if (Board.IsKingInCheck[Board.CurrentTurn])
            {
                return -(Checkmate - ply); // Mated in [ply].
            }

            // Stalemate.
            else return Draw;
        }


        // Rearrange the moves list to try to come across better moves earlier,
        // causing more beta cutoffs and a faster search overall.
        OrderMoves(moves, ply);


        // Extend the search when in check.
        CheckExtension(ref extensions);

        // Extend if only one legal move is available.
        // TODO: Further research needed. May need to limit one reply extensions to only when in check.
        //OneReplyExtension();


        // Multi-cut.
        // Temporarily disabled because results are highly inconsistent: it often gives misleading results, causing nonsense moves to be played.
        // Occasionally it does make the search faster, but it more often makes it slower.
        // FORGOT TO MAKE MOVES BEFORE SEARCHING.
        //if (!rootNode && depth >= 3 && useMultiCut)
        //{
        //    const int R = 2;
        //
        //    int c = 0;
        //    const int M = 6;
        //    for (int i = 0; i < M; i++)
        //    {
        //        Board.MakeMove(moves[i]);
        //
        //        int score = -Search(depth - R, ply + 1, -beta, -beta + 1, extensions, evaluationData, out pvLine, useMultiCut : false);
        //
        //        Board.UnmakeMove(moves[i]);
        //
        //        if (score >= beta)
        //        {
        //            const int C = 3;
        //            if (++c == C) return beta;
        //        }
        //    }
        //}


        // evaluationType == UpperBound -> node is an All-Node. All nodes were searched and none reached alpha. Alpha is returned.
        // evaluationType == Exact -> node is a PV-Node. All nodes were searched and some reached alpha. The new alpha is returned.
        // evaluationType == LowerBound -> node is a Cut-Node. Not all nodes were searched, because a beta cutoff occured. Beta is returned.
        EvaluationType evaluationType = EvaluationType.UpperBound;

        // Moves Loop:
        // Iterate through all legal moves and perform a depth - 1 search on each one.
        for (int i = 0; i < moves.Count; i++)
        {
            // Skip PV lines that have already been explored.
            if (rootNode && s_excludedRootMoves.Any(m => m.Equals(moves[i]))) continue;

            // Make the move on the board.
            // The move must be unmade before moving on to the next one.
            Board.MakeMove(moves[i]);


            // Store information on the move for pruning purposes.
            bool isCapture = IsCapture(moves[i]);
            bool isCaptureOrPromotion = IsCaptureOrPromotion(moves[i]);

            bool givesCheck = Board.IsKingInCheck[Board.CurrentTurn];


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
            s_totalSearchNodes++;

            // If the branch wasn't pruned, create a new child node.
            Node newNode = node.AddNewChild();
            ref int score = ref newNode.Score;
            ref int newExtensions = ref newNode.Extensions;

            // Depth reduction.
            int depthReduction = 1;

            bool usedLmr;

            // Reduce the depth of the next search if it is unlikely to reveal anything interesting.
            ReduceDepth();

            // Extend the depth of the next search if it is likely to be interesting.
            ExtendDepth(ref newExtensions);


            // Permorm a search on the new position with a depth reduced by R.
            // Note: the bounds need to be inverted (alpha = -beta, beta = -alpha), because what was previously the ceiling is now the score to beat, and viceversa.
            // Note: the score needs to be negated, because the evaluation in the point of view of the opponent is opposite to ours.
            score = -Search(newNode, depth - depthReduction, -beta, -alpha, out Line nextLine);

            // In case late move reductions were used and the score exceeded alpha,
            // a re-search at full depth is needed to verify the score.
            if (usedLmr && score > alpha)
                score = -Search(newNode, depth - 1, -beta, -alpha, out nextLine);


            // Look for promotions that avoid a draw.
            //FindBetterPromotion();


            // Unmake the move on the board.
            // This must be done before moving onto the next move.
            Board.UnmakeMove(moves[i]);


            // If the search was aborted, don't return incorrect values.
            if (WasSearchAborted) return Null;

            // A new best move was found!
            if (score > alpha)
            {
                // Verification Search:
                // When a new best move is found at a high depth, and the score
                // is significantly higher than the previous best score,
                // the best line found should be verified at a slightly deeper depth.
                //if (depth >= VerificationSearchMinimumDepth &&
                //    score - alpha >= 1 && !nextLine.Cleanup())
                //{
                //    var old = Board.ZobristKey;
                //    // Reach the end of the computer's "plan".
                //    nextLine.MakeMoves();
                //
                //    Search(node.Child, depth - R, -beta, -alpha, evaluationData, out Line newNextLine);
                //
                //    nextLine.UnmakeMoves();
                //    if (Board.ZobristKey != old)
                //    {
                //        Console.WriteLine("!!!");
                //    }
                //
                //    nextLine = newNextLine;
                //}


                evaluationType = EvaluationType.Exact;

                alpha = score;

                pvLine.Move = moves[i];
                pvLine.Next = nextLine;

                // Fail-High:
                // If the score is higher than beta, it means the move is
                // too good for the opponent and this node will be avoided.
                // There's no need to look at any other moves since we already know this one is worse than we can afford.
                // Note: beta is usually either negativeInfinity or -alpha of the parent node.
                // This means that no cutoffs can occur until the first branch of the root has been explored up to a leaf node.
                if (score >= beta)
                {
                    evaluationType = EvaluationType.LowerBound;

                    TT.StoreEvaluation(depth, ply, beta, evaluationType, pvLine, staticEvaluation);

                    // If a quiet move caused a beta cutoff, update it's stats.
                    if (!isCapture) UpdateQuietMoveStats();

                    return beta;
                }
            }


            bool FutilityPruning()
            {
                if (useFutilityPruning && i > 0 &&
                !isCaptureOrPromotion && !givesCheck)
                {
                    // It's essential to unmake moves when pruning inside the moves loop.
                    Board.UnmakeMove(moves[i]);
                    return true;
                }

                return false;
            }

            bool LateMovePruning()
            {
                useLateMovePruning = !rootNode &&
                    depth < ShallowDepthThreshold &&
                    i > LateMovePruningThreshold(depth);

                if (useLateMovePruning &&
                    !isCaptureOrPromotion && !givesCheck)
                {
                    Board.UnmakeMove(moves[i]);
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
                if (!rootNode && i > s_lateMoveThresholds[moves.Count] && !inCheck && !givesCheck)
                {
                    // Don't reduce captures, promotions and killer moves,
                    // unless we are past the moveCountBasedPruningThreshold (very late moves).
                    if (useLateMovePruning ||
                        (moves[i].CapturedPieceType == None &&
                        moves[i].PromotionPiece == None &&
                        !moves[i].Equals(s_killerMoves[0, ply]) &&
                        !moves[i].Equals(s_killerMoves[1, ply])))
                    {
                        usedLmr = true;
                        depthReduction += s_lateMoveDepthReductions[Min(depth, 63)][Min(i, 63)];
                    }
                }
            }

            void ExtendDepth(ref int extensions)
            {
                //if (newExtensions >= s_maxExtensions) return;

                // Capture extension.
                //if (moves[i].CapturedPieceType != Piece.None)
                //{
                //    newExtensions++;
                //    R--;
                //}

                //if (newExtensions >= s_maxExtensions) return;

                // Recapture extension.
                //if ((moves[i].TargetSquare & previousCapture) != 0)
                //{
                //    newExtensions++;
                //    R--;
                //}

                if (extensions >= s_maxExtensions) return;

                // Passed pawn extension.
                if (moves[i].PieceType == Pawn &&
                    ((moves[i].TargetSquare & Mask.SeventhRank) != 0))
                {
                    extensions++;
                    depthReduction--;
                }

                //if (newExtensions >= s_maxExtensions) return;

                // Promotion extension.
                //if (moves[i].PromotionPiece != Piece.None)
                //{
                //    newExtensions++;
                //    R--;
                //}
            }


            //void FindBetterPromotion()
            //{
            //    // Note: alternatives are only searched in case of a draw score.
            //    // A possible improvement would be to search for any
            //    // better promotion in case of, for example, a losing score.
            //
            //    if (moves[i].PromotionPiece != None && score == Draw)
            //    {
            //        foreach (int promotionPiece in new int[] { Knight, Rook, Bishop })
            //        {
            //            // Unmake the previous promotion.
            //            Board.UnmakeMove(moves[i]);
            //
            //            moves[i].PromotionPiece = promotionPiece;
            //
            //            // Make the new promotion.
            //            Board.MakeMove(moves[i]);
            //
            //            // Store the score of the new promotion.
            //            // Note: because it's a rare edge case, reductions are not used here. 
            //            // If this is found to be a bottleneck in the program, it could easily be optimized.
            //            score = -Search(node + 1, depth - 1, -beta, -alpha, out nextLine);
            //            node.Child.SetScore(score);
            //
            //            // If a better promotion was found, exit the loop.
            //            if (score > Draw) break;
            //        }
            //    }
            //}


            void UpdateQuietMoveStats()
            {
                // Moves with the same start and target square will be boosted in move ordering.
                s_historyHeuristics[Board.CurrentTurn][FirstSquareIndex(moves[i].StartSquare), FirstSquareIndex(moves[i].TargetSquare)] += depth * depth;

                StoreKillerMove(moves[i], ply);
            }
        }

        // Store killer move in case the best move found is quiet, even if it didn't cause a beta cutoff.
        // Disabled because of ambiguous performance.

        // In case of an all-node, the pvLine will have a null move.
        //if (pvLine.Move?.CapturedPieceType == Piece.None)
        //{
        //    History[FirstSquareIndex(pvLine.Move.StartSquare), FirstSquareIndex(pvLine.Move.TargetSquare)] += depth * depth;
        //
        //    StoreKillerMove(pvLine.Move, ply);
        //}

        // Once all legal moves have been searched, save the best score found in the transposition table and return it.
        TT.StoreEvaluation(depth, ply, alpha, evaluationType, pvLine, staticEvaluation);

        return alpha;


        bool MateDistancePruning()
        {
            if (!rootNode)
            {
                // Limit alpha and beta to the losing and winning scores respectively.
                alpha = Max(alpha, -Checkmate + ply);
                beta = Min(beta, Checkmate - (ply + 1));

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
                if (ttMove != null /* BUG: ttMove is sometimes null even though ttHit is true. Somewhere in the code entries are being saved without a best move. Not sure whether or not this should ever be done. */&& 
                    !ttMoveIsCapture)
                {
                    if (ttEval >= beta)
                    {
                        s_historyHeuristics[Board.CurrentTurn][FirstSquareIndex(ttMove.StartSquare), FirstSquareIndex(ttMove.TargetSquare)] += depth * depth;

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

                else staticEvaluation = Evaluate(out int _);

                if (ttEval != Null) evaluation = ttEval;
                else evaluation = staticEvaluation;
            }

            // If this is the first time this position is encountered,
            // calculate the static evaluation.
            else
            {
                staticEvaluation = evaluation = Evaluate(out int _);

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

        bool NullMovePruning(ref int evaluation, out int nullMovePruningScore)
        {
            nullMovePruningScore = Null;

            if (!rootNode && !inCheck && useNullMovePruning &&
                depth > 2 && evaluation >= beta)
            {
                // Used for backup data storage.
                NullMove move = new();

                Board.MakeNullMove(move);

                Node newNode = node.AddNewChild();
                ref int score = ref newNode.Score;

                // After the current turn is skipped, perform a reduced depth search.
                const int R = 3;

                score = -Search(newNode, depth - R, -beta, -beta + 1 /* We are only interested to know if the score can reach beta. */, out Line _, useNullMovePruning: false);


                Board.UnmakeNullMove(move);

                if (WasSearchAborted) return false;
                if (score >= beta)
                {
                    // Note: score is not always equal to beta here because of,
                    // for example, a reduced depth of 0, where the static evaluation is returned.

                    // Avoid returning unproven wins.
                    if (IsMateScore(score)) score = beta;

                    nullMovePruningScore = score;

                    return true;
                }
            }

            return false;
        }

        // Following the Stockfish implementation.
        bool ProbCut(ref int staticEvaluation, ref Line pvLine, out int probCutScore)
        {
            probCutScore = Null;

            if (!rootNode && depth > ProbCutDepthReduction && !IsMateScore(beta))
            {
                // Value from Stockfish.
                int probCutBeta = beta + 191 - 54 * (hasStaticEvaluationImproved ? 1 : 0);

                // Note: should only generate moves with SEE score > probCutBeta - staticEvaluation.
                var moves = Board.GenerateAllLegalMoves(capturesOnly: true);
                OrderMoves(moves, -1);

                for (int i = 0; i < moves.Count; i++)
                {
                    Board.MakeMove(moves[i]);

                    Node newNode = node.AddNewChild();

                    // Perform a preliminary qsearch to verify that the move holds.
                    probCutScore = -QuiescenceSearch(newNode, -probCutBeta, -probCutBeta + 1, out Line probCutLine);

                    // If the qsearch held, perform the regular search.
                    if (probCutScore >= probCutBeta)
                    {
                        probCutScore = -Search(newNode, depth - ProbCutDepthReduction, -probCutBeta, -probCutBeta + 1, out probCutLine);
                    }

                    Board.UnmakeMove(moves[i]);

                    if (probCutScore >= probCutBeta)
                    {
                        pvLine.Move = moves[i];
                        pvLine.Next = probCutLine;

                        // Save ProbCut data into transposition table.
                        TT.StoreEvaluation(depth - (ProbCutDepthReduction - 1 /* Here the effective depth is 1 higher than the reduced prob cut depth. */),
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
            if (!rootNode /* Cannot use at the root because of MultiPV */ &&
                depth > InternalIterativeDeepeningDepthReduction && ttMove == null)
            {
                ref int score = ref node.Score;
                score = Search(node, depth - InternalIterativeDeepeningDepthReduction, alpha, beta, out Line _);

                ttEntry = TT.GetStoredEntry(out ttHit);
                ttEval = TT.GetStoredEvaluation(ply);

                ttLine = ttHit ? ttEntry.Line : null;
                ttMove = ttLine?.Move;
                ttMoveIsCapture = IsCaptureOrPromotion(ttMove);
            }
        }


        void CheckExtension(ref int extensions)
        {
            if (!rootNode && extensions < s_maxExtensions && inCheck)
            {
                extensions++;
                depth++;
            }
        }

        void OneReplyExtension(ref int extensions)
        {
            if (!rootNode && extensions < s_maxExtensions && moves.Count == 1)
            {
                extensions++;
                depth++;
            }
        }
    }

    /// <summary>
    /// The QuiescenceSearch function extends the normal Search, evaluating all legal captures.
    /// For more information, see https://www.chessprogramming.org/Quiescence_Search.
    /// </summary>
    public static int QuiescenceSearch(Node node, int alpha, int beta, out Line pvLine)
    {
        int ply = node.Ply;

        // pvLine == null -> branch was pruned.
        // pvLine == empty -> node is an All-Node.
        pvLine = null;


        if (WasSearchAborted) return Null;

        // Note: draws by repetition are not possible when all moves are captures.

        if (IsDrawByInsufficientMaterial()) return Draw;


        // Return the static evaluation immediately if the max ply was reached.
        if (WasMaxPlyReached()) return Evaluate(out int _);


        //if (ply > SelectiveDepth) SelectiveDepth = ply;


        #region Store Transposition Data
        // Was this position searched before?
        bool ttHit;

        // Store transposition table entry. To be accessed only if ttHit is true.
        TTEntry ttEntry = TT.GetStoredEntry(out ttHit);

        // Lookup transposition evaluation. If the lookup fails, ttEval == LookupFailed.
        int ttEval = TT.GetStoredEvaluation(ply);

        // Store the best move found when this position was previously searched.
        Line ttLine = ttHit ? ttEntry.Line : null;
        Move ttMove = ttLine?.Move;
        bool ttMoveIsCapture = IsCaptureOrPromotion(ttMove);
        #endregion


        // Early Transposition Table Cutoff:
        // If the current position has been evaluated before at a depth
        // greater or equal the current depth, return the stored value.
        if (TTCutoff(out pvLine)) return ttEval;


        pvLine = new();


        #region Store Static Evaluation Data
        // Early pruning is disabled when in check.
        bool inCheck = Board.IsKingInCheck[Board.CurrentTurn];

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
        if (alpha < evaluation) alpha = evaluation;


        var moves = Board.GenerateAllLegalMoves(capturesOnly: true);

        // Note: checking for checkmate or stalemate here is not
        // possible because not all legal moves were generated.


        OrderMoves(moves, -1);

        //bool isEndGame = gamePhase < EndgamePhaseScore;


        // evaluationType == UpperBound -> node is an All-Node. All nodes were searched and none reached alpha. Alpha is returned.
        // evaluationType == Exact -> node is a PV-Node. All nodes were searched and some reached alpha. The new alpha is returned.
        // evaluationType == LowerBound -> node is a Cut-Node. Not all nodes were searched, because a beta cutoff occured. Beta is returned.
        EvaluationType evaluationType = EvaluationType.UpperBound;

        for (int i = 0; i < moves.Count; i++)
        {
            // Delta pruning
            //if (!isEndGame)
            //    if (GetPieceValue(move.CapturedPieceType) + 200 <= alpha) continue;

            Board.MakeMove(moves[i]);

            s_totalSearchNodes++;

            Node newNode = node.AddNewChild();
            ref int score = ref newNode.Score;

            score = -QuiescenceSearch(newNode, -beta, -alpha, out Line nextLine);

            Board.UnmakeMove(moves[i]);


            // A new best move was found!
            if (score > alpha)
            {
                evaluationType = EvaluationType.Exact;

                alpha = score;

                pvLine.Move = moves[i];
                pvLine.Next = nextLine;

                // Fail-High:
                // If the score is higher than beta, it means the move is
                // too good for the opponent and the parent node will be avoided.
                // There's no need to look at any other moves since we already know that this one is worse than we can afford.
                // Note: beta is either negativeInfinity or -alpha of the parent node.
                // This means that no cutoffs can occur until the first branch of the root has been explored fully.
                if (score >= beta)
                {
                    evaluationType = EvaluationType.LowerBound;

                    TT.StoreEvaluation(0, ply, beta, evaluationType, pvLine, staticEvaluation);

                    return beta;
                }
            }
        }


        TT.StoreEvaluation(0, ply, alpha, evaluationType, pvLine, staticEvaluation);
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
                evaluation = Evaluate(out int _);
            }

            // If this position was already evaluated, use the stored value.
            else if (ttHit)
            {
                if (ttEntry.StaticEvaluation != Null)
                    staticEvaluation = ttEntry.StaticEvaluation;

                else staticEvaluation = Evaluate(out int _);

                if (ttEval != Null) evaluation = ttEval;
                else evaluation = staticEvaluation;
            }

            // If this is the first time this position is encountered,
            // calculate the static evaluation.
            else
            {
                staticEvaluation = evaluation = Evaluate(out int _);

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


    public static void StoreKillerMove(Move move, int ply)
    {
        if (s_killerMoves[0, ply] != null && !s_killerMoves[0, ply].Equals(move))
            s_killerMoves[1, ply] = new(s_killerMoves[0, ply]);

        s_killerMoves[0, ply] = move;
    }


    /// <summary>
    /// Order <paramref name="moves"/> based on the likelihood of a move being strong in the current position.
    /// </summary>
    /// <param name="moves">Move list to order</param>
    /// <param name="ply">Current ply in the search tree. Used to compare killer moves</param>
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
            // Moves with a higher score are more likely to cause a beta cutoff,
            // speeding up the search, so they will be searched first.
            int moveScore = 0;

            // Give the highest priority to the best move that was previosly found.
            if (move.Equals(ttMove)) moveScore += 30000;

            // Captures are sorted with a high priority.
            else if (move.CapturedPieceType != None)
            {
                // Captures are sorted using MVV-LVA (Most Valuable Victim - Least Valuable Attacker).
                // A weak piece capturing a strong one will be given a
                // higher priority than a strong piece capturing a weak one.
                moveScore += MvvLva[move.PieceType][move.CapturedPieceType];
            }

            // Quiet moves are sorted with a low priority, with the exception of killer moves.
            else
            {
                // Sort killer moves just below captures.
                if (move.Equals(s_killerMoves[0, ply])) moveScore += 10000;
                else if (move.Equals(s_killerMoves[1, ply])) moveScore += 9000;

                // Sort non-killer quiet moves by history heuristics.
                else
                {
                    int startSquareIndex = FirstSquareIndex(move.StartSquare);
                    int targetSquareIndex = FirstSquareIndex(move.TargetSquare);
                    moveScore += s_historyHeuristics[Board.CurrentTurn][startSquareIndex, targetSquareIndex];

                    //moveScoreGuess += PieceSquareTables.Read(move.PromotionPiece == None ? move.PieceType : move.PromotionPiece, targetSquareIndex, Board.CurrentTurn == 0, gamePhase);
                    //moveScore += GetPieceValue(move.PromotionPiece);
                    //if (Board.PawnAttackersTo(targetSquareIndex, Board.CurrentTurn, Board.OpponentTurn) != 0) moveScore -= 350;
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
        s_killerMoves = new Move[2, MaxPly];
        s_historyHeuristics = new[] { new int[64, 64], new int[64, 64] };
    }
    

    public static bool IsMateScore(int score)
    {
        return score != Null && Abs(score) >= Checkmate - MaxPly;
    }


    /// <summary>If a position is reached three times, it's a draw.</summary>
    public static bool IsDrawByRepetition(ulong key) => Board.PositionHistory.Count(other => other == key) >= 3;


    /// <summary>If there is not enough material on the board for either player to checkate the opponent, it's a draw.</summary>
    public static bool IsDrawByInsufficientMaterial()
    {
        int whitePieceCount = PieceCount(Board.OccupiedSquares[0]);
        int blackPieceCount = PieceCount(Board.OccupiedSquares[1]);

        // It's not a draw if a player has more than 2 pieces.
        if (whitePieceCount > 2 || blackPieceCount > 2) return false;

        // King vs king.
        if (whitePieceCount == 1 && blackPieceCount == 1) return true;


        int whiteKnightCount = PieceCount(Board.Knights[0]);
        int blackKnightCount = PieceCount(Board.Knights[1]);

        // King and knight vs king.
        if (whiteKnightCount == 1 && blackPieceCount == 1) return true;
        if (blackKnightCount == 1 && whitePieceCount == 1) return true;


        int whiteBishopCount = PieceCount(Board.Bishops[0]);
        int blackBishopCount = PieceCount(Board.Bishops[1]);

        // King and bishop vs king.
        if (whiteBishopCount == 1 && blackPieceCount == 1) return true;
        if (blackBishopCount == 1 && whitePieceCount == 1) return true;

        // King and bishop vs king and bishop with bishops of the same color.
        if (whitePieceCount == 2 && whiteBishopCount == 1 &&
            blackPieceCount == 2 && blackBishopCount == 1)
        {
            if ((Board.Bishops[0] & Mask.LightSquares) != 0 && (Board.Bishops[1] & Mask.LightSquares) != 0) return true;
            if ((Board.Bishops[0] & Mask.DarkSquares) != 0 && (Board.Bishops[1] & Mask.DarkSquares) != 0) return true;
        }

        return false;
    }
    
    private static bool IsCapture(Move? move) =>
        move != null &&
        (move.CapturedPieceType != None);

    private static bool IsCaptureOrPromotion(Move? move) =>
        move != null &&
        (move.CapturedPieceType != None ||
        move.PromotionPiece != None);


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


    // Values from Stockfish: https://github.com/official-stockfish/Stockfish/blob/master/src/search.cpp#L77-L80
    private static int LateMovePruningThreshold(int depth) => (3 + depth * depth) / 2;
}

public class Line
{
    public Move Move;
    public Line Next;

    public Line(Move move = null, Line next = null)
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

    public void MakeMoves(bool removeEntries = false)
    {
        Board.MakeMove(Move);
        if (removeEntries) TT.ClearCurrentEntry();
        if (Next != null) Next.MakeMoves(removeEntries);
    }

    public void UnmakeMoves()
    {
        if (Next != null) Next.UnmakeMoves();
        Board.UnmakeMove(Move);
    }

    // In case of an All-Node, the pvLine will have a null move.
    // This function prevents this.
    public bool Cleanup()
    {
        if (Next != null)
        {
            if (Next.Cleanup()) Next = null;
        }

        if (Move == null) return true;
        else return false;
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
        return $"{(Move != null ? Move.ToString() : "")} {(Next != null ? Next.ToString() : "")}";
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

    /// <summary>Extension count from the root to this node.</summary>
    public int Extensions;

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
        Extensions = parent.Extensions;
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
