using System.Diagnostics;
using static Utilities.Bitboard;
using static System.Math;
using static Piece;
using static Evaluation;
using static TranspositionTable;

public class Engine
{
    public const int Null = 32002;
    public const int Infinity = 32001;
    public const int Checkmate = 32000;
    public const int Draw = 0;


    public static Stopwatch SearchStopwatch = new();

    public static bool WasSearchAborted;

    // Indexed by [totalMoveCount].
    private static int[] _lateMoveThresholds;

    // Indexed by [improving(0/1)][depth].
    private static int[][] _futilityMargin;

    public static readonly int LimitedRazoringMargin = StaticPieceValues[Queen][0];

    public static readonly int[] _extraPromotions = { Knight, Rook, Bishop };

    public static float _totalSearchTime;

    public static bool _testing;

    public static Move[,] KillerMoves;

    public const int MaxPly = 64;

    public const int _maxKillerMoves = 2;

    // Limits the total amount of search depth extensions possible in a single branch.
    // Set to twice the search depth.
    public static int MaxExtensions;

    public static Line MainLine;
    public static Line CurrentMainLine;

    public static CancellationTokenSource cancelSearchTimer;

    public static Action OnSearchComplete;

    public static string _evaluation;

    public static float _progress;

    public static bool _versionTesting;

    public static int[] AspirationWindowsMultipliers = { 4, 4 };

    // Indexed by [colorIndex][startSquareIndex][targetSquareIndex].
    public static int[][,] History;

    public static int _whiteWinsCount;
    public static int _blackWinsCount;
    public static int _drawsCount;

    public static int[,] Reductions = new int[64, 64];

    public static float TimeLimit;
    public static bool UseTimeLimit;
    public static bool UseTimeManagement;
    public static bool UseMoveOrdering;
    public static int LateMoveReductionMinimumTreshold;
    public static double LateMoveReductionPercentage;
    public static ulong SearchNodes;
    public static List<int> SearchNodesPerDepth;
    public static List<string> BestMovesThisSearch;
    public static bool UseTranspositionTable;
    public static bool ResetTranspositionTableOnEachSearch;
    public static bool UseOpeningBook;
    public static int InternalIterativeDeepeningDepthReduction;
    public static int ProbCutDepthReduction;
    public static int VerificationSearchMinimumDepth;
    public static int NullMovePrunes;
    public static int MaxDepthReachedCount;
    public static int FutilityPrunes;
    public static int ShallowDepthThreshold;
    public static int MoveCountBasedPrunes;

    public static int Ply;

    public static int OptimumTime;
    public static int MaximumTime;

    public static int SelectiveDepth;

    public static Node RootNode;

    public static int MultiPvCount;

    public static List<Move> ExcludedRootMoves;


    private static int _totalSearchNodes;


    public static void Init()
    {
        ResizeTranspositionTable(8);

        for (int depth = 1; depth < 64; depth++)
        {
            for (int count = 1; count < 64; count++)
            {
                float d = (float)Log(depth);
                float c = (float)Log(count);
                Reductions[depth, count] = Max((int)Round(d * c / 2f) - 1, 0);
            }
        }

        _lateMoveThresholds =
        Enumerable.Range(0, 300).Select(totalMoveCount => (int)(
        (totalMoveCount + LateMoveReductionMinimumTreshold) - (totalMoveCount * LateMoveReductionPercentage))).ToArray();

        _futilityMargin = new[] {
            Enumerable.Range(0, 64).Select(depth => 165 * depth).ToArray(),
            Enumerable.Range(0, 64).Select(depth => 165 * (depth - 1)).ToArray(),
        };

        OnSearchComplete += FinishSearch;
    }


    public static int Perft(int depth, int startingDepth, Move previousMove = null, Move ancestorMove = null)
    {
        if (depth == 0)
        {
            if (startingDepth == 1)
                Console.WriteLine($"{previousMove} : 1");

            return 1;
        }

        var moves = Board.GenerateAllLegalMoves(promotionMode: 0);

        int numPositions = 0;
        foreach (var move in moves)
        {
            Board.MakeMove(move);
            numPositions += Perft(depth - 1, startingDepth, move, previousMove);
            Board.UnmakeMove(move);
        }

        if (depth == startingDepth - 1)
            Console.WriteLine($"{previousMove}: {numPositions}");

        return numPositions;
    }


    public static void FindBestMove()
    {
        Task.Factory.StartNew(
            StartSearch, TaskCreationOptions.LongRunning);

        cancelSearchTimer = new();
        if (UseTimeLimit) Task.Delay((int)(TimeLimit * 1000), cancelSearchTimer.Token).ContinueWith((t) => AbortSearch());
    }

    public static void AbortSearch()
    {
        WasSearchAborted = true;
    }

    public static void FinishSearch()
    {
        cancelSearchTimer?.Cancel();

        Console.WriteLine($"bestmove {MainLine.Move}");

        //Board.MakeMove(MainLine.Move);

        //using (StreamWriter writer = File.CreateText(@"C:\RootNode.txt"))
        //{
        //    Console.WriteLine($"Started Saving...");
        //    writer.Write(RootNode.ToString());
        //}
        //Console.WriteLine($"Saved Successfully!");
        //Console.WriteLine($"{RootNode}");
    }

    private static void StartSearch()
    {
        try
        {
            TranspositionTable.IsEnabled = UseTranspositionTable;

            WasSearchAborted = false;

            _progress = 0;
            SearchNodes = 0;
            _totalSearchNodes = 0;

            SearchNodesPerDepth = new();

            Board.PositionHistory = new();

            MainLine = new();
            CurrentMainLine = new();

            BestMovesThisSearch = new();

            if (ResetTranspositionTableOnEachSearch) TranspositionTable.Clear();

            // Opening book not available in UCI mode.

            SearchStopwatch.Restart();
            int depth = 1;

            int evaluation;
            do
            {
                ExcludedRootMoves = new();

                for (int pvIndex = 0; pvIndex < MultiPvCount; pvIndex++)
                {
                    int alpha = -Infinity;
                    int beta = Infinity;

                    int alphaWindow = 25;
                    int betaWindow = 25;

                    while (true)
                    {
                        // Reset on each iteration because of better performance.
                        // This behaviour is not expected and further research is required.
                        KillerMoves = new Move[_maxKillerMoves, MaxPly];
                        History = new[] { new int[64, 64], new int[64, 64] };

                        _progress = 0;

                        SelectiveDepth = 0;

                        // Maximum amount of search extensions inside any given branch.
                        // MaxExtensions = depth -> the SelDepth may be up to twice the depth.
                        // Further testing is needed to find the perfect value here.
                        MaxExtensions = depth;


                        RootNode = new(0, SearchType.Normal);
                        evaluation = Search(RootNode, depth, alpha, beta, out CurrentMainLine);

                        bool searchFailed = false;
                        if (evaluation <= alpha)
                        {
                            alphaWindow *= AspirationWindowsMultipliers[0];
                            searchFailed = true;
                        }

                        if (evaluation >= beta)
                        {
                            betaWindow *= AspirationWindowsMultipliers[1];
                            searchFailed = true;
                        }

                        alpha = Max(evaluation - alphaWindow, -Infinity);
                        beta = Min(evaluation + betaWindow, Infinity);

                        if (!searchFailed) break;
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
                    if (CurrentMainLine != null && !CurrentMainLine.Cleanup())
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
                        _totalSearchTime = SearchStopwatch.ElapsedMilliseconds;

                        RootNode.SetScore(evaluation);

                        //DepthReachedData = new(CurrentDepthReachedData);
                        //BestMovesThisSearch.Add(CurrentMainLine.Move.ToString());
                        MainLine = new(CurrentMainLine);
                        _evaluation = (evaluation * (Board.CurrentTurn == 1 ? -0.01f : 0.01f)).ToString("0");
                        SearchNodes = (ulong)_totalSearchNodes;
                        SearchNodesPerDepth.Add(_totalSearchNodes);

                        Console.WriteLine($"info " +
                            $"depth {depth} " +
                            $"seldepth {SelectiveDepth + 1} " +
                            $"multipv {pvIndex + 1} " +
                            $"score " +
                                (!IsMateScore(evaluation) ?
                                $"cp {evaluation} " :
                                $"mate {((evaluation > 0) ? "+" : "-")}{Ceiling((Checkmate - Abs(evaluation)) / 2.0)} ") +
                            $"nodes {SearchNodes} " +
                            $"nps {(int)Round(SearchNodes / (double)SearchStopwatch.ElapsedMilliseconds * 1000)} " +
                            $"time {SearchStopwatch.ElapsedMilliseconds} " +
                            $"pv {MainLine}");

                        ExcludedRootMoves.Add(MainLine?.Move);

                        //Stream stream = new FileStream(AppDomain.CurrentDomain.BaseDirectory + "RootNode.txt", FileMode.Create, FileAccess.Write);
                        //
                        //JsonSerializer.Serialize(stream, RootNode);
                        //stream.Close();
                    }

                    else break;

                    if (ExcludedRootMoves.Count >= Board.GenerateAllLegalMoves(promotionMode: 2).Count) break;
                }

                depth++;
            }
            while (
            !WasSearchAborted &&
            (!UseTimeManagement || SearchStopwatch.ElapsedMilliseconds <= OptimumTime) &&
            (UseTimeLimit || depth <= TimeLimit /* Time limit also represents the max depth. */));

            SearchStopwatch.Stop();
            OnSearchComplete.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }


    /// <summary>
    /// The Search function goes through every legal move,
    /// then recursively calls itself on each of the opponent's responses
    /// until the depth reaches 0. <br /> Finally, the positions reached are evaluated.
    /// The path that leads to the best "forced evaluation" is then chosoen. <br />
    /// The greater the depth, the further into the future the computer will be able to see,
    /// possibly finding more advanced tactics and better moves.
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
    /// <param name="depth">The remaining depth to search before evaluating the positions reached.</param>
    /// <param name="alpha">The lower bound of the evaluation</param>
    public static int Search(Node node, int depth, int alpha, int beta, out Line pvLine, bool useNullMovePruning = true, ulong previousCapture = 0, bool useMultiCut = true)
    {
        int ply = node.Ply;
        ref int extensions = ref node.Extensions;

        SearchType parentSearchType = node.SearchType;

        // pvLine == null -> branch was pruned.
        // pvLine == empty -> node is an All-Node.
        pvLine = null;

        bool rootNode = ply == 0;


        if (WasSearchAborted) return Null;

        // Detect draws by repetition.
        if (IsDrawByRepetition(Board.ZobristKey)) return Draw;

        if (IsDrawByInsufficientMaterial()) return Draw;


        // Return the static evaluation immediately if the max ply was reached.
        if (WasMaxPlyReached()) return Evaluate(out int _);


        // Once the depth reaches 0, keep searching until no more captures
        // are available using the QuiescenceSearch function.
        if (depth <= 0)
        {
            node.SearchType = SearchType.Quiescence;
            return QuiescenceSearch(node, alpha, beta, out pvLine);
        }


        if (ply > SelectiveDepth) SelectiveDepth = ply;


        // Mate Distance Pruning:
        // If a forced checkmate was found at a lower ply,
        // prune this branch.
        if (MateDistancePruning()) return alpha;


        #region Lookup Transposition Data
        // Was this position searched before?
        bool ttHit;

        // Store transposition table entry. To be accessed only if ttHit is true.
        Entry ttEntry = TranspositionTable.
            GetStoredEntry(out ttHit);

        // Lookup transposition evaluation. If the lookup fails, ttEval == LookupFailed.
        int ttEval = TranspositionTable.
            LookupEvaluation(depth, ply, alpha, beta);

        // Store the best move found when this position was previously searched.
        Line ttLine = ttHit ? ttEntry.Line : null;
        Move ttMove = ttLine?.Move;
        bool ttMoveIsCapture = IsCaptureOrPromotion(ttMove);
        #endregion


        // Early Transposition Table Cutoff:
        // If the current position has been evaluated before at a depth
        // greater or equal the current depth, return the stored value.
        if (TranspositionTableCutoff(out pvLine)) return ttEval;


        pvLine = new();


        #region Store Static Evaluation Data
        // Early pruning is disabled when in check.
        bool inCheck = Board.IsKingInCheck[Board.CurrentTurn];

        // Evaluation of the current position.
        ref int staticEvaluation = ref node.StaticEvaluation;

        // Has the static evaluation improved since our last turn?
        bool hasStaticEvaluationImproved;

        // Approximation of the actual evaluation.
        // Found using the transposition table in case of a ttHit, otherwise evaluation = staticEvaluation.
        // Note: might use results of lower depth searches. 
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

        // Reset search type after pruning.
        node.SearchType = parentSearchType;

        // If the position is not in the transposition table,
        // save it with a reduced depth search for better move ordering.
        InternalIterativeDeepening();


        // Generate a list of all the moves currently available.
        var moves = Board.GenerateAllLegalMoves(
            promotionMode: 2 /* Only queen promotions. Note: other promotions are tried if these end in a draw.*/);

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
        if (UseMoveOrdering) OrderMoves(moves, ply);


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

        // The current node's extensions count will now be used to store its children's count.
        // This value will be used at each iteration of the moves loop to restore it.
        int extensionsBackup = extensions;


        // evaluationType == UpperBound -> node is an All-Node. All nodes were searched and none reached alpha. Alpha is returned.
        // evaluationType == Exact -> node is a PV-Node. All nodes were searched and some reached alpha. The new alpha is returned.
        // evaluationType == LowerBound -> node is a Cut-Node. Not all nodes were searched, because a beta cutoff occured. Beta is returned.
        EvaluationType evaluationType = EvaluationType.UpperBound;
        node.NodeType = NodeType.AllNode;

        // Moves Loop:
        // Iterate through all legal moves and perform a depth - 1 search on each one.
        for (int i = 0; i < moves.Count; i++)
        {
            // Skip PV lines that have already been explored.
            if (rootNode && ExcludedRootMoves.Any(m => m.Equals(moves[i]))) continue;

            // Restore this node's extension count.
            // The value is modified for each child node,
            // so it needs to be reset here.
            // Note: should be reset before pruning to ensure the proper value is used on each node.
            extensions = extensionsBackup;


            // Make the move on the board.
            // The move must be unmade before moving onto the next one.
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
            _totalSearchNodes++;


            // Depth reduction.
            int R = 1;

            // Were late move reductions used on this move?
            bool usedLmr;

            // Reduce the depth of the next search if it is unlikely to reveal anything interesting.
            ReduceDepth();

            // Extend the depth of the next search if it is likely to be interesting.
            ExtendDepth(ref extensions);


            // Permorm a search on the new position with a depth reduced by R.
            // Note: the bounds need to be inverted (alpha = -beta, beta = -alpha), because what was previously the ceiling is now the score to beat, and viceversa.
            // Note: the score needs to be negated, because the evaluation in the point of view of the opponent is opposite to ours.
            node.SearchType = SearchType.LateMoveReductionsNormal;
            int score = -Search(node + 1, depth - R, -beta, -alpha, out Line nextLine);
            node.Child.SetScore(score);

            // In case late move reductions were used and the score exceeded alpha,
            // a re-search at full depth is needed to verify the score.
            if (usedLmr && score > alpha)
            {
                node.SearchType = SearchType.Normal;
                score = -Search(node + 1, depth - 1, -beta, -alpha, out nextLine);
                node.Child.SetScore(score);
            }


            // Look for promotions that avoid a draw.
            FindBetterPromotion();


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
                node.NodeType = NodeType.PVNode;

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
                    node.NodeType = NodeType.CutNode;

                    TranspositionTable.StoreEvaluation(depth, ply, beta, evaluationType, pvLine, staticEvaluation);

                    // If a quiet move caused a beta cutoff, update it's stats.
                    if (!isCapture) UpdateQuietMoveStats();

                    // Reset search type before returning.
                    node.SearchType = parentSearchType;
                    return beta;
                }
            }

            // Update the search progress.
            if (rootNode)
            {
                _progress = (float)i / moves.Count * 100;


                //Console.WriteLine($"info tree {RootNode}");
            }


            bool FutilityPruning()
            {
                if (useFutilityPruning && i > 0 &&
                !isCaptureOrPromotion && !givesCheck)
                {
                    // It's essential to unmake moves when pruning inside the moves loop.
                    Board.UnmakeMove(moves[i]);

                    FutilityPrunes++;
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

                    MoveCountBasedPrunes++;
                    return true;
                }

                return false;
            }


            void ReduceDepth()
            {
                // Late Move Reductions:
                // Reduce quite moves towards the end of the ordered moves list.
                usedLmr = false;
                // Only reduce if we aren't in check and the move doesn't give check to the opponent.
                if (!rootNode && i > _lateMoveThresholds[moves.Count] && !inCheck && !givesCheck)
                {
                    // Don't reduce captures, promotions and killer moves,
                    // unless we are past the moveCountBasedPruningThreshold (very late moves).
                    if (useLateMovePruning ||
                        (moves[i].CapturedPieceType == None &&
                        moves[i].PromotionPiece == None &&
                        !moves[i].Equals(KillerMoves[0, ply]) &&
                        !moves[i].Equals(KillerMoves[1, ply])))
                    {
                        usedLmr = true;
                        R += Reductions[Min(depth, 63), Min(i, 63)];
                    }
                }
            }

            void ExtendDepth(ref int extensions)
            {
                //if (newExtensions >= MaxExtensions) return;

                // Capture extension.
                //if (moves[i].CapturedPieceType != Piece.None)
                //{
                //    newExtensions++;
                //    R--;
                //}

                //if (newExtensions >= MaxExtensions) return;

                // Recapture extension.
                //if ((moves[i].TargetSquare & previousCapture) != 0)
                //{
                //    newExtensions++;
                //    R--;
                //}

                if (extensions >= MaxExtensions) return;

                // Passed pawn extension.
                if (moves[i].PieceType == Pawn &&
                    ((moves[i].TargetSquare & Mask.SeventhRank) != 0))
                {
                    extensions++;
                    R--;
                }

                //if (newExtensions >= MaxExtensions) return;

                // Promotion extension.
                //if (moves[i].PromotionPiece != Piece.None)
                //{
                //    newExtensions++;
                //    R--;
                //}
            }


            void FindBetterPromotion()
            {
                // Note: alternatives are only searched in case of a draw score.
                // A possible improvement would be to search for any
                // better promotion in case of, for example, a losing score.

                if (moves[i].PromotionPiece != None && score == Draw)
                {
                    foreach (int promotionPiece in _extraPromotions)
                    {
                        // Unmake the previous promotion.
                        Board.UnmakeMove(moves[i]);

                        moves[i].PromotionPiece = promotionPiece;

                        // Make the new promotion.
                        Board.MakeMove(moves[i]);

                        // Store the score of the new promotion.
                        // Note: because it's a rare edge case, reductions are not used here. 
                        // If this is found to be a bottleneck in the program, it could easily be optimized.
                        score = -Search(node + 1, depth - 1, -beta, -alpha, out nextLine);
                        node.Child.SetScore(score);

                        // If a better promotion was found, exit the loop.
                        if (score > Draw) break;
                    }
                }
            }


            void UpdateQuietMoveStats()
            {
                // Moves with the same start and target square will be boosted in move ordering.
                History[Board.CurrentTurn][FirstSquareIndex(moves[i].StartSquare), FirstSquareIndex(moves[i].TargetSquare)] += depth * depth;

                // 
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
        TranspositionTable.StoreEvaluation(depth, ply, alpha, evaluationType, pvLine, staticEvaluation);

        // Reset search type before returning.
        node.SearchType = parentSearchType;
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

        bool TranspositionTableCutoff(out Line pvLine)
        {
            pvLine = null;

            if (!rootNode && ttHit && ttEntry.Depth >= depth && ttEval != LookupFailed)
            {
                // Update quiet move stats.
                if (!ttMoveIsCapture)
                {
                    if (ttEval >= beta)
                    {
                        History[Board.CurrentTurn][FirstSquareIndex(ttMove.StartSquare), FirstSquareIndex(ttMove.TargetSquare)] += depth * depth;

                        StoreKillerMove(ttMove, ply);
                    }
                }

                pvLine = TranspositionTable.GetStoredLine();

                node.NodeType = NodeType.TranspositionTableCutoffNode;
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
                //TranspositionTable.StoreEvaluation(Null, Null, LookupFailed, Null, null, staticEvaluation);
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
                        node.SearchType = SearchType.RazoringQuiescence;
                        int newScore = QuiescenceSearch(node, alpha, beta, out Line _);
                        node.SetScore(newScore);

                        razoringScore = Max(newScore, score);

                        node.NodeType = NodeType.PrunedNode;
                        return true;
                    }

                    // Increase margin for higher depths.
                    score += StaticPieceValues[Pawn][0];

                    if (score < beta && depth <= 3)
                    {
                        node.SearchType = SearchType.RazoringQuiescence;
                        int newScore = QuiescenceSearch(node, alpha, beta, out Line _);
                        node.SetScore(newScore);

                        // Verify the new score before returning it.
                        if (newScore < beta)
                        {
                            razoringScore = Max(newScore, score);

                            node.NodeType = NodeType.PrunedNode;
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

                if (evaluation + _futilityMargin[hasStaticEvaluationImproved ? 1 : 0][Min(depth, 63)] <= alpha)
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


                // After the current turn is skipped, perform a reduced depth search.
                const int R = 3;

                node.SearchType = SearchType.NullMovePruningNormal;
                int score = -Search(node + 1, depth - R, -beta, -beta + 1 /* We are only interested to know if the score can reach beta. */, out Line _, useNullMovePruning: false);
                node.Child.SetScore(score);


                Board.UnmakeNullMove(move);

                if (WasSearchAborted) return false;
                if (score >= beta)
                {
                    // Note: score is not always equal to beta here because of,
                    // for example, a reduced depth of 0, where the static evaluation is returned.

                    // Avoid returning unproven wins.
                    if (IsMateScore(score)) score = beta;

                    NullMovePrunes++;

                    nullMovePruningScore = score;

                    node.NodeType = NodeType.PrunedNode;
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
                var moves = Board.GenerateAllLegalMoves(capturesOnly: true, promotionMode: 2);
                if (UseMoveOrdering) OrderMoves(moves, -1);

                for (int i = 0; i < moves.Count; i++)
                {
                    Board.MakeMove(moves[i]);

                    // Perform a preliminary qsearch to verify that the move holds.
                    node.SearchType = SearchType.ProbCutQuiescence;
                    probCutScore = -QuiescenceSearch(node + 1, -probCutBeta, -probCutBeta + 1, out Line probCutLine);
                    node.Child.SetScore(probCutScore);

                    // If the qsearch held, perform the regular search.
                    if (probCutScore >= probCutBeta)
                    {
                        node.SearchType = SearchType.ProbCutNormal;
                        probCutScore = -Search(node + 1, depth - ProbCutDepthReduction, -probCutBeta, -probCutBeta + 1, out probCutLine);
                        node.Child.SetScore(probCutScore);
                    }

                    Board.UnmakeMove(moves[i]);

                    if (probCutScore >= probCutBeta)
                    {
                        pvLine.Move = moves[i];
                        pvLine.Next = probCutLine;

                        // Save ProbCut data into transposition table.
                        TranspositionTable.StoreEvaluation(depth - (ProbCutDepthReduction - 1 /* Here the effective depth is 1 higher than the reduced prob cut depth. */),
                            ply, probCutScore, EvaluationType.LowerBound, pvLine, staticEvaluation);

                        node.NodeType = NodeType.PrunedNode;
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
                node.SearchType = SearchType.InternalIterativeDeepeningNormal;
                int score = Search(node, depth - InternalIterativeDeepeningDepthReduction, alpha, beta, out Line _);
                node.SetScore(score);

                ttEntry = TranspositionTable.GetStoredEntry(out ttHit);
                ttEval = TranspositionTable.LookupEvaluation(depth, ply, alpha, beta);

                ttLine = ttHit ? ttEntry.Line : null;
                ttMove = ttLine?.Move;
                ttMoveIsCapture = IsCaptureOrPromotion(ttMove);
            }
        }


        void CheckExtension(ref int extensions)
        {
            if (!rootNode && extensions < MaxExtensions && inCheck)
            {
                extensions++;
                depth++;
            }
        }

        bool WasMaxPlyReached()
        {
            if (ply >= MaxPly)
            {
                MaxDepthReachedCount++;

                //if (CurrentDepthReachedData.Any(d => d.Depth == ply)) CurrentDepthReachedData.Find(d => d.Depth == ply).Count++;
                //else CurrentDepthReachedData.Add(new(ply));

                return true;
            }

            return false;
        }

        void OneReplyExtension(ref int extensions)
        {
            if (!rootNode && extensions < MaxExtensions && moves.Count == 1)
            {
                extensions++;
                depth++;
            }
        }
    }

    /// <summary>
    /// The QuiescenceSearch function extends the normal Search, evaluating all legal captures.
    /// </summary>
    public static int QuiescenceSearch(Node node, int alpha, int beta, out Line pvLine)
    {
        int ply = node.Ply;

        // pvLine == null -> branch was pruned.
        // pvLine == empty -> node is an All-Node.
        pvLine = null;

        bool rootNode = ply == 0;


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
        Entry ttEntry = TranspositionTable.
            GetStoredEntry(out ttHit);

        // Lookup transposition evaluation. If the lookup fails, ttEval == LookupFailed.
        int ttEval = TranspositionTable.
            LookupEvaluation(0, ply, alpha, beta);

        // Store the best move found when this position was previously searched.
        Line ttLine = ttHit ? ttEntry.Line : null;
        Move ttMove = ttLine?.Move;
        bool ttMoveIsCapture = IsCaptureOrPromotion(ttMove);
        #endregion


        // Early Transposition Table Cutoff:
        // If the current position has been evaluated before at a depth
        // greater or equal the current depth, return the stored value.
        if (TranspositionTableCutoff(out pvLine)) return ttEval;


        pvLine = new();


        #region Store Static Evaluation Data
        // Early pruning is disabled when in check.
        bool inCheck = Board.IsKingInCheck[Board.CurrentTurn];

        // Evaluation of the current position.
        int staticEvaluation;

        // Has the static evaluation improved since our last turn?
        bool hasStaticEvaluationImproved;

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


        var moves = Board.GenerateAllLegalMoves(capturesOnly: true, promotionMode: 2);

        // Note: checking for checkmate or stalemate here is not
        // possible because not all legal moves were generated.


        if (UseMoveOrdering) OrderMoves(moves, -1);

        //bool isEndGame = gamePhase < EndgamePhaseScore;


        // evaluationType == UpperBound -> node is an All-Node. All nodes were searched and none reached alpha. Alpha is returned.
        // evaluationType == Exact -> node is a PV-Node. All nodes were searched and some reached alpha. The new alpha is returned.
        // evaluationType == LowerBound -> node is a Cut-Node. Not all nodes were searched, because a beta cutoff occured. Beta is returned.
        EvaluationType evaluationType = EvaluationType.UpperBound;
        node.NodeType = NodeType.AllNode;

        for (int i = 0; i < moves.Count; i++)
        {
            // Delta pruning
            //if (!isEndGame)
            //    if (GetPieceValue(move.CapturedPieceType) + 200 <= alpha) continue;

            Board.MakeMove(moves[i]);

            _totalSearchNodes++;

            node.SearchType = SearchType.Quiescence;
            int score = -QuiescenceSearch(node + 1, -beta, -alpha, out Line nextLine);
            node.Child.SetScore(score);

            Board.UnmakeMove(moves[i]);


            // A new best move was found!
            if (score > alpha)
            {
                evaluationType = EvaluationType.Exact;
                node.NodeType = NodeType.PVNode;

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
                    node.NodeType = NodeType.CutNode;

                    TranspositionTable.StoreEvaluation(0, ply, beta, evaluationType, pvLine, staticEvaluation);

                    return beta;
                }
            }
        }


        TranspositionTable.StoreEvaluation(0, ply, alpha, evaluationType, pvLine, staticEvaluation);
        return alpha;


        bool TranspositionTableCutoff(out Line pvLine)
        {
            pvLine = null;

            if (ttHit && ttEval != LookupFailed)
            {
                pvLine = TranspositionTable.GetStoredLine();

                node.NodeType = NodeType.TranspositionTableCutoffNode;
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

                // In quiescence search, evaluation must never be set to Null to avoid issues with standing pat.
                evaluation = -Infinity;
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
                //TranspositionTable.StoreEvaluation(Null, Null, LookupFailed, Null, null, staticEvaluation);
            }
        }

        bool WasMaxPlyReached()
        {
            if (ply >= MaxPly)
            {
                MaxDepthReachedCount++;

                //if (CurrentDepthReachedData.Any(d => d.Depth == ply)) CurrentDepthReachedData.Find(d => d.Depth == ply).Count++;
                //else CurrentDepthReachedData.Add(new(ply));

                return true;
            }

            return false;
        }
    }


    public static void StoreKillerMove(Move move, int ply)
    {
        if (KillerMoves[0, ply] != null && !KillerMoves[0, ply].Equals(move))
            KillerMoves[1, ply] = new(KillerMoves[0, ply]);

        KillerMoves[0, ply] = move;
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
        Move ttMove = TranspositionTable.GetStoredMove();


        List<int> scores = new();
        foreach (Move move in moves)
        {
            // A score given to each move based on various parameters.
            // Moves with a higer score are likely to be better.
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
                if (move.Equals(KillerMoves[0, ply])) moveScore += 9000;
                else if (move.Equals(KillerMoves[1, ply])) moveScore += 8000;

                // Sort non-killer quiet moves by history heuristic.
                else
                {
                    int targetSquareIndex = FirstSquareIndex(move.TargetSquare);

                    //if (History.Cast<int>().Any(x => x != 0))
                    //{
                    //    int a = 0;
                    //}

                    moveScore += History[Board.CurrentTurn][FirstSquareIndex(move.StartSquare), targetSquareIndex];

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

    public static bool IsMateScore(int score)
    {
        return score != Null && Abs(score) >= Checkmate - MaxPly;
    }


    /// <summary>If a position is reached three times, it's a draw.</summary>
    public static bool IsDrawByRepetition(ulong key) => Board.PositionHistory.Count(other => other == key) >= 2;


    /// <summary>If there is not enough material on the board for either player to checkate the opponent, it's a draw.</summary>
    public static bool IsDrawByInsufficientMaterial()
    {
        return
            PieceCount(Board.AllOccupiedSquares) == 2 || /* King vs king. */
            (PieceCount(Board.OccupiedSquares[0]) == 2 && (Board.Bishops[0] != 0 || Board.Knights[0] != 0)) || /* King and bishop vs king or king and knight vs king. */
            (PieceCount(Board.OccupiedSquares[1]) == 2 && (Board.Bishops[1] != 0 || Board.Knights[1] != 0)) || /* King vs king and bishop or king vs king and knight. */
            ((PieceCount(Board.OccupiedSquares[0]) == 2 && PieceCount(Board.Bishops[0]) == 1) &&
            (PieceCount(Board.OccupiedSquares[1]) == 2 && PieceCount(Board.Bishops[1]) == 1) &&
            FirstSquareIndex(Board.Bishops[0]) % 2 == FirstSquareIndex(Board.Bishops[1]) % 2); /* King and bishop vs king and bishop with bishops of the same color. */
    }

    public static bool IsCapture(Move move) =>
        move != null &&
        (move.CapturedPieceType != None);

    public static bool IsCaptureOrPromotion(Move move) =>
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
        if (removeEntries) TranspositionTable.ClearCurrentEntry();
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
public class Node
{
    /// <summary>
    /// Depth navigated along this node's branch.
    /// </summary>
    public int Ply;

    /// <summary>
    /// 
    /// </summary>
    public SearchType SearchType;

    /// <summary>
    /// 
    /// </summary>
    public NodeType NodeType;


    /// <summary>
    /// Extension count from the root to this node.
    /// </summary>
    public int Extensions;

    /// <summary>
    /// 
    /// </summary>
    public int Evaluation;

    /// <summary>
    /// 
    /// </summary>
    public int StaticEvaluation;

    public int Score;


    /// <summary>
    /// 
    /// </summary>
    public Node Parent;
    /// <summary>
    /// 
    /// </summary>
    public List<Node> Children;

    private int CurrentChildIndex;


    public Node(int ply, SearchType searchType, Node parent = null)
    {
        Ply = ply;
        SearchType = searchType;
        Parent = parent;
        Children = new();
        CurrentChildIndex = 0;
    }


    /// <summary>
    /// The last child that was added.
    /// </summary>
    public Node Child => Children.Last();

    public Node Grandparent => Parent?.Parent;

    public List<Node> Grandchildren => Children.SelectMany(c => c.Children).ToList();


    public static Node operator -(Node node, int index)
    {
        Node result = node;
        for (int i = 0; i < index; i++)
        {
            result = result.Parent;
            if (result == null) break;
        }
        return result;
    }

    /// <summary>
    /// Navigate down the tree from <paramref name="node"/> following the current path <paramref name="depth"/> times. <br />
    /// Any missing children will be added as necessary. <br />
    /// Note: the behaviour changes if the required child is missing at the end: the CurrentChildIndex of the parent of the last node will increase by 1. <br />
    /// Note: an alternative approach may be to use recursion.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="depth"></param>
    /// <returns></returns>
    public static Node operator +(Node node, int depth)
    {
        Node result = node;
        for (int i = 0; i < depth; i++)
        {
            // Select the current child.
            if (result.Children.Count > result.CurrentChildIndex)
                result = result.Children[result.CurrentChildIndex];

            // Add a new child node.
            else
            {
                result.Children.Add(new(result.Ply + 1, result.SearchType, result) { Extensions = result.Extensions });

                // Update the reference,
                // then increment the CurrentChildIndex if the max depth was reached.
                int childIndex = result.CurrentChildIndex;
                if ((i == depth - 1)) result.CurrentChildIndex++;

                result = result.Children[childIndex];

            }
        }

        return result;
    }

    public override string ToString()
    {
        return
            $"{Ply}," +
            $"{Enum.GetName(typeof(SearchType), SearchType)}," +
            $"{Enum.GetName(typeof(NodeType), NodeType)}," +
            $"{(Children != null && Children.Count > 0 ? $"[{string.Join(";", Children)};]" : "[]")}";
    }

    public Node GetRoot()
    {
        Node result = this;
        while (result != null && result.Ply > 0) result -= 1;
        return result;
    }

    public void SetScore(int score) => Score = score;
}

public enum NodeType
{
    LeafNode,
    PVNode,
    CutNode,
    AllNode,
    PrunedNode,
    TranspositionTableCutoffNode,
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