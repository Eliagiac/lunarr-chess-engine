using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static TranspositionTable;

public class AIPlayer
{
    public static TranspositionTable TranspositionTable;

    public static string _moves;

    public const int PositiveInfinity = 32001;
    public const int NegativeInfinity = -PositiveInfinity;
    public const int CheckmateScore = 32000;
    public const int None = 32002;

    public static int _bestEval;

    public static int _currentSearchNodes;


    public static System.Diagnostics.Stopwatch SearchTime = new();
    public static System.Diagnostics.Stopwatch CurrentSearchTime = new();
    public static float _searchTimeResult;

    public static bool AbortSearch { get; set; }

    // Indexed by LateMoveThreshold[totalMoveCount]
    public static int[] LateMoveThreshold;

    public static readonly int[] _futilityMargin = { 0, Evaluation.StaticPieceValues[Piece.Knight][0], Evaluation.StaticPieceValues[Piece.Rook][0] };

    public static readonly int LimitedRazoringMargin = Evaluation.StaticPieceValues[Piece.Queen][0];

    public static bool _nullMoveCheckmateFound;

    public static readonly int[] _extraPromotions = { Piece.Knight, Piece.Rook, Piece.Bishop };

    public static float _totalSearchTime;

    public static bool _testing;

    public static Move[,] KillerMoves;

    public const int MaxPly = 64;

    public const int _maxKillerMoves = 2;

    // Limits the total amount of search depth extensions possible in a single branch.
    // Set to twice the search depth.
    public static int MaxExtensions;

    public static EvaluationData evaluationData;

    public static Line MainLine;
    public static Line CurrentMainLine;

    public static CancellationTokenSource cancelSearchTimer;

    public static Action OnSearchComplete;

    public static bool MoveFound;

    public static string _evaluation;

    public static float _progress;

    public static bool _versionTesting;

    public static int[] AspirationWindowsMultipliers = { 4, 4 };

    public static ulong _nodeCount;
    public static Queue<ulong> _nodeCountHistory = new();

    public static int[,] HistoryHeuristic = new int[64, 64];

    public static int _whiteWinsCount;
    public static int _blackWinsCount;
    public static int _drawsCount;

    public static int[,] Reductions = new int[64, 64];

    public static float TimeLimit;
    public static bool UseTimeLimit;
    public static bool UseMoveOrdering;
    public static int LateMoveReductionMinimumTreshold;
    public static float LateMoveReductionPercentage;
    public static ulong SearchNodes;
    public static List<int> SearchNodesPerDepth;
    public static List<string> BestMovesThisSearch;
    public static bool UseTranspositionTable;
    public static bool ResetTranspositionTableOnEachSearch;
    public static bool UseOpeningBook;
    public static List<DepthReachedData> DepthReachedData;
    public static List<DepthReachedData> CurrentDepthReachedData;
    public static int NullMovePrunes;
    public static int MaxDepthReachedCount;
    public static int FutilityPrunes;
    public static int ShallowDepthThreshold;
    public static int MoveCountBasedPrunes;
    public static int FutilityPruningMaxDepth;
    public static int NullMovePruningHighDepthThreshold; 
    public static int QuiescenceMoveCountPruningThreshold;

    public static int Ply;

    public static int OptimumTime;
    public static int MaximumTime;

    private static int NullMovePruningMinPly;
    private static int NullMovePruningColor;


    public static void Init()
    {
        TranspositionTable = new(1_000_000); // 2^23

        LateMoveThreshold = Enumerable.Range(0, 300).Select(n => (int)((n + LateMoveReductionMinimumTreshold) - (n * LateMoveReductionPercentage / 100))).ToArray();

        for (int depth = 1; depth < 64; depth++)
        {
            for (int count = 1; count < 64; count++)
            {
                float d = (float)Math.Log(depth);
                float c = (float)Math.Log(count);
                Reductions[depth, count] = Math.Max((int)Math.Round(d * c / 2f) - 1, 0);
            }
        }

        OnSearchComplete += FinishSearch;
    }


    public static int Perft(int depth, int startingDepth, Move previousMove = null, Move ancestorMove = null)
    {
        if (depth == startingDepth - 1 && depth == 0)
        {
            _moves += previousMove + ": 1\n";
            Console.WriteLine(previousMove + ": 1");
        }

        //if (depth == startingDepth - 1 && depth == 0 && ancestorMove.ToString() == "b2b3")
        //{
        //    _moves += previousMove + ": 1\n";
        //    Console.WriteLine(previousMove + ": 1");
        //}

        if (depth == 0)
        {
            return 1;
        }

        var moves = Board.GenerateAllLegalMoves(promotionMode: 0);

        int numPositions = 0;

        foreach (var move in moves)
        {
            //if (depth == startingDepth && move.ToString() == "b2b3")
            //{
            //    int a = 0;
            //}

            Board.MakeMove(move);
            numPositions += Perft(depth - 1, startingDepth, move, previousMove);
            Board.UnmakeMove(move);
        }

        if (depth == startingDepth - 1)
        {
            _moves += previousMove + ": " + numPositions + "\n";
            Console.WriteLine(previousMove + ": " + numPositions);
        }

        return numPositions;
    }


    public static void PlayBestMove(EvaluationData evaluationData)
    {
        AIPlayer.evaluationData = evaluationData;
        PlayBestMove();
    }

    public static void PlayBestMove()
    {
        evaluationData = new();

        MoveFound = false;


        Task.Factory.StartNew(() => StartSearch(), TaskCreationOptions.LongRunning);

        cancelSearchTimer = new();
        if (UseTimeLimit) Task.Delay((int)(TimeLimit * 1000), cancelSearchTimer.Token).ContinueWith((t) => StopSearch());
    }

    public static void StopSearch()
    {
        AbortSearch = true;
    }

    public static void FinishSearch()
    {
        cancelSearchTimer?.Cancel();
        MoveFound = true;
        Board.MakeMove(MainLine.Move);
    }

    private static void StartSearch()
    {
        TranspositionTable.Enabled = UseTranspositionTable;

        AbortSearch = false;

        _progress = 0;
        SearchNodes = 0;
        _currentSearchNodes = 0;

        DepthReachedData = new();

        SearchNodesPerDepth = new();

        MainLine = new();
        CurrentMainLine = new();

        BestMovesThisSearch = new();

        if (ResetTranspositionTableOnEachSearch) TranspositionTable.Clear();

        KillerMoves = new Move[_maxKillerMoves, MaxPly];

        // Opening book not available in UCI mode.

        SearchTime.Restart();
        int depth = 1;

        int evaluation;

        int alpha = NegativeInfinity;
        int beta = PositiveInfinity;

        int alphaWindow = 25;
        int betaWindow = 25;
        do
        {
            CurrentSearchTime.Restart();
            _progress = 0;

            CurrentDepthReachedData = new();
            MaxExtensions = depth * 2;


            evaluation = Search(NodeType.PV, depth, 0, alpha, beta, 0, evaluationData, false, out CurrentMainLine);

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

            alpha = evaluation - alphaWindow;
            beta = evaluation + betaWindow;

            if (searchFailed)
            {
                CurrentSearchTime.Stop();
                continue;
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
            if (!CurrentMainLine.Cleanup() && CurrentMainLine != null)
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

            CurrentSearchTime.Stop();

            if (!AbortSearch)
            {
                _totalSearchTime = SearchTime.ElapsedMilliseconds;

                DepthReachedData = new(CurrentDepthReachedData);
                _searchTimeResult = CurrentSearchTime.ElapsedMilliseconds;
                //Debug.Log($"{depth} : {CurrentSearchTime.Elapsed.ToString()}, ({alpha}, {beta}).");
                _bestEval = evaluation;
                //BestMovesThisSearch.Add(CurrentMainLine.Move.ToString());
                MainLine = new(CurrentMainLine);
                _evaluation = (evaluation * (Board.CurrentTurn == 1 ? -0.01f : 0.01f)).ToString("0");
                SearchNodes = (ulong)_currentSearchNodes;
                SearchNodesPerDepth.Add(_currentSearchNodes);

                Console.WriteLine($"info depth {depth} score cp {(!IsMateScore(evaluation) ? (int)Math.Round(evaluation / 2.6) : $"{((evaluation > 0) ? "+" : "-")}M{((CheckmateScore - Math.Abs(evaluation)) - 1) / 2}")} nodes {SearchNodes} nps {(int)Math.Round(SearchNodes / (double)SearchTime.ElapsedMilliseconds * 1000)} time {SearchTime.ElapsedMilliseconds} pv {MainLine}");
            }

            depth++;
        }
        while (
        !AbortSearch &&
        SearchTime.ElapsedMilliseconds <= (OptimumTime > 0 ? OptimumTime : int.MaxValue) &&
        (UseTimeLimit || depth <= TimeLimit /* Time limit also represents the max depth. */));
        SearchTime.Stop();

        OnSearchComplete.Invoke();
    }

    public static int Search(NodeType nodeType, int depth, int plyFromRoot, int alpha, int beta, int extensions, EvaluationData evaluationData, bool cutNode, out Line pvLine)
    {
        pvLine = null;
        if (AbortSearch) return 0;

        bool pvNode = nodeType == NodeType.PV;
        bool rootNode = plyFromRoot == 0;

        // Detect draws by repetition and max depth reached.
        if (!rootNode)
        {
            if (Board.PositionHistory.Count(key => key == Board.ZobristKey) >= 2)
            {
                return 0;
            }

            if (plyFromRoot >= MaxPly)
            {
                MaxDepthReachedCount++;

                //if (CurrentDepthReachedData.Any(d => d.Depth == plyFromRoot)) CurrentDepthReachedData.Find(d => d.Depth == plyFromRoot).Count++;
                //else CurrentDepthReachedData.Add(new(plyFromRoot));

                return Evaluation.Evaluate(out int _, evaluationData);
            }
        }

        if (depth <= 0)
        {
            return QuiescenceSearch(nodeType, alpha, beta, plyFromRoot, evaluationData, out pvLine);
        }

        // Mate distance pruning.
        if (!rootNode)
        {
            alpha = Math.Max(alpha, -CheckmateScore + plyFromRoot);
            beta = Math.Min(beta, CheckmateScore - plyFromRoot);

            if (alpha >= beta)
            {
                return alpha;
            }
        }

        //int ttVal = TranspositionTable.LookupEvaluation(depth, plyFromRoot, alpha, beta);
        //
        //// Don't use traspositions at ply from root 0 to analyse possible draws by repetition.
        //if (ttVal != TranspositionTable.lookupFailed && plyFromRoot > 0)
        //{
        //    pvLine = TranspositionTable.GetStoredLine();
        //    return ttVal;
        //}

        var ttEntry = TranspositionTable.GetStoredEntry(out bool ttHit);
        var ttValue = ttHit ? TranspositionTable.CorrectRetrievedMateScore(ttEntry.Value, plyFromRoot) : None;
        var ttLine = ttHit ? ttEntry.Line : null;
        var ttMove = ttLine?.Move;
        bool ttCapture = ttMove != null ? 
            (ttMove.CapturedPieceType == Piece.None && ttMove.PromotionPiece == Piece.None) : false;

        // Check for a transposition table cutoff at non-PV nodes.
        if (!pvNode && ttHit && ttEntry.Depth >= depth &&
            ttValue != None /* From stockfish (reason unclear).*/ &&
            (ttEntry.Bound & (ttValue >= beta ? LowerBound : UpperBound)) != 0)
        {
            if (ttMove != null)
            {
                // Update killer move stats.
                if (ttValue >= beta)
                {
                    if (ttMove.CapturedPieceType == Piece.None && ttMove.PromotionPiece == Piece.None)
                    {
                        HistoryHeuristic[BitboardUtility.FirstSquareIndex(ttMove.StartSquare), BitboardUtility.FirstSquareIndex(ttMove.TargetSquare)] += depth * depth;

                        StoreKillerMove(ttMove, plyFromRoot);
                    }
                }
            }

            pvLine = ttLine;
            return ttValue;
        }

        pvLine = new();

        bool inCheck = Board.IsKingInCheck[Board.CurrentTurn];

        bool improving;
        int staticEvaluation;
        int pureStaticEvaluation;
        int gamePhase;

        // Store the static evaluation of the position
        if (inCheck)
        {
            staticEvaluation = pureStaticEvaluation = None;
            improving = false;
            goto MovesLoop;
        }

        else if (ttHit)
        {
            if (ttEntry.Value == None) staticEvaluation = pureStaticEvaluation = Evaluation.Evaluate(out gamePhase, evaluationData);
            else staticEvaluation = pureStaticEvaluation = ttEntry.Value;

            if (ttValue != None &&
                (ttEntry.Bound & (ttValue > staticEvaluation ? LowerBound : UpperBound)) != 0)
            {
                staticEvaluation = ttValue;
            }
        }

        else
        {
            int bonus = 0; // Yet to be implemented;

            pureStaticEvaluation = Evaluation.Evaluate(out gamePhase, evaluationData);
            staticEvaluation = pureStaticEvaluation + bonus;
        }

        // TODO: Set improving.

        // Razoring.
        // Inspired by Strelka: https://www.chessprogramming.org/Razoring#Strelka.
        // As implemented in Wukong JS: https://github.com/maksimKorzh/wukongJS/blob/main/wukong.js#L1575-L1591.
        if (!rootNode)
        {
            int score = staticEvaluation + Evaluation.StaticPieceValues[Piece.Pawn][0];

            if (score < beta)
            {
                if (depth == 1)
                {
                    int newScore = QuiescenceSearch(NodeType.NonPV, alpha, beta, plyFromRoot, evaluationData, out Line _);
                    return Math.Max(newScore, score);
                }

                score += Evaluation.StaticPieceValues[Piece.Pawn][0];

                if (score < beta && depth <= 3)
                {
                    int newScore = QuiescenceSearch(NodeType.NonPV, alpha, beta, plyFromRoot, evaluationData, out Line _);
                    if (newScore < beta) return Math.Max(newScore, score);
                }
            }
        }

        //// Futility pruning condition.
        //bool useFutilityPruning = false;
        //if (!rootNode && depth < 3 && !inCheck)
        //{
        //    // Should also check if eval is a mate score, 
        //    // otherwise the engine will be blind to certain checkmates.
        //
        //    if (staticEvaluation + _futilityMargin[depth] <= alpha)
        //    {
        //        useFutilityPruning = true;
        //    }
        //}

        // Futility pruning.
        if (!rootNode && depth < FutilityPruningMaxDepth &&
            !IsMateScore(staticEvaluation) &&
            staticEvaluation - FutilityPruningMargin(depth) >= beta)
        {
            FutilityPrunes++;
            return staticEvaluation;
        }


        // Null move pruning.
        if (!pvNode && staticEvaluation >= beta &&
            pureStaticEvaluation >= beta - 36 * depth + 225 &&
            plyFromRoot >= NullMovePruningMinPly &&
            Board.CurrentTurn != NullMovePruningColor)
        {
            int R = (823 + 67 * depth) / 256 + Math.Min((staticEvaluation - beta) / 200, 3);

            NullMove move = new();
            Board.MakeNullMove(move);

            int evaluation = -Search(NodeType.NonPV, depth - R, plyFromRoot + 1, -beta, -beta + 1, extensions, evaluationData, !cutNode, out Line _);

            Board.UnmakeNullMove(move);

            if (AbortSearch) return 0;
            if (evaluation >= beta)
            {
                if (IsWinningScore(evaluation)) evaluation = beta;

                if (NullMovePruningMinPly != 0 ||
                    (!IsMateScore(beta) && depth < NullMovePruningHighDepthThreshold))
                {
                    NullMovePrunes++;
                    return evaluation;
                }

                // At high depths, do a verification search
                // with null move pruning disabled for our color.

                NullMovePruningMinPly = plyFromRoot + (3 * (depth - R) / 4);
                NullMovePruningColor = Board.CurrentTurn;

                int newEvaluation = Search(NodeType.NonPV, depth - R, plyFromRoot, beta - 1, beta, extensions, evaluationData, false, out Line _);

                NullMovePruningMinPly = 0;

                if (newEvaluation >= beta)
                {
                    NullMovePrunes++;
                    return newEvaluation;
                }
            }

            //// Checkmate threat extension.
            //if (extensions < MaxExtensions && _nullMoveCheckmateFound)
            //{
            //    extensions++;
            //    depth++;
            //}
            //_nullMoveCheckmateFound = false;
        }


    // When in check, early pruning is skipped.
    MovesLoop:

        // Check extension.
        if (!rootNode && extensions < MaxExtensions && inCheck)
        {
            extensions++;
            depth++;
        }


        Move bestMove = null;

        var moves = Board.GenerateAllLegalMoves(promotionMode: 2);
        if (UseMoveOrdering) OrderMoves(moves, plyFromRoot);

        if (moves.Count == 0)
        {
            //if (CurrentDepthReachedData.Any(d => d.Depth == plyFromRoot)) CurrentDepthReachedData.Find(d => d.Depth == plyFromRoot).Count++;
            //else CurrentDepthReachedData.Add(new(plyFromRoot));

            if (Board.IsKingInCheck[Board.CurrentTurn])
            {
                //if (!useNullMovePruning && nullMoveSearchPlyFromRoot == 0) _nullMoveCheckmateFound = true;
                return -(CheckmateScore - plyFromRoot);
            }
            return 0;
        }


        // One reply extension.
        if (!rootNode && extensions < MaxExtensions && moves.Count == 1)
        {
            extensions++;
            depth++;
        }


        // Multi-cut.
        // Temporarily disabled because results are highly inconsistent: it often gives misleading results, causing nonsense moves to be played.
        // Occasionally it does make the search faster, but it more often makes it slower.
        //if (!rootNode && depth >= 3 && useMultiCut)
        //{
        //    const int R = 2;
        //
        //    int c = 0;
        //    const int M = 6;
        //    for (int i = 0; i < M; i++)
        //    {
        //        int evaluation = -Search(depth - R, plyFromRoot + 1, -beta, -beta + 1, extensions, evaluationData, useMultiCut : false);
        //
        //        if (evaluation >= beta)
        //        {
        //            const int C = 3;
        //            if (++c == C) return beta;
        //        }
        //    }
        //}

        bool pvExact = pvNode && ttHit && ttEntry.Bound == Exact;

        int evalType = UpperBound;

        for (int i = 0; i < moves.Count; i++)
        {
            Board.MakeMove(moves[i]);


            bool captureOrPromotion =
                moves[i].CapturedPieceType != Piece.None ||
                moves[i].PromotionPiece != Piece.None;

            bool givesCheck = Board.IsKingInCheck[Board.CurrentTurn];

            //// Futility pruning.
            //if (useFutilityPruning && i > 0 &&
            //    !captureOrPromotion && !givesCheck)
            //{
            //    Board.UnmakeMove(moves[i]);
            //
            //    FutilityPrunes++;
            //    continue;
            //}

            // Move count based pruning.
            // Inspired by Stockfish: https://github.com/official-stockfish/Stockfish/blob/master/src/search.cpp#L1005
            bool moveCountBasedPruning = !rootNode &&
                depth < ShallowDepthThreshold &&
                i > MoveCountBasedPruningThreshold(depth);

            // Skip quiet late moves at shallow depths.
            if (moveCountBasedPruning &&
                !captureOrPromotion && !givesCheck)
            {
                Board.UnmakeMove(moves[i]);

                MoveCountBasedPrunes++;
                continue;
            }


            _currentSearchNodes++;
            _nodeCount++;

            int newExtensions = extensions;
            int newDepth = depth - 1;

            // Capture extension.
            //if (newExtensions < MaxExtensions && moves[i].CapturedPieceType != Piece.None)
            //{
            //    newExtensions++;
            //    newDepth--;
            //}

            // Recapture extension.
            //if (newExtensions < MaxExtensions && (moves[i].TargetSquare & previousCapture) != 0)
            //{
            //    newExtensions++;
            //    newDepth--;
            //}

            // Passed pawn extension.
            //if (newExtensions < MaxExtensions &&
            //    moves[i].PieceType == Piece.Pawn &&
            //    ((moves[i].TargetSquare & Mask.SeventhRank) != 0))
            //{
            //    newExtensions++;
            //    newDepth--;
            //}

            // Promotion extension.
            //if (newExtensions < MaxExtensions &&
            //    moves[i].PromotionPiece != Piece.None)
            //{
            //    newExtensions++;
            //    newDepth--;
            //}

            // Depth reductions/extensions.
            int R = 0;
            int D = newDepth;

            // Late move reduction.
            bool lmr = false;
            // Only reduce if depth > 2, we aren't in check and the move doesn't give check to the opponent.
            // Don't reduce captures and promotions, unless we are past the moveCountBasedPruningThreshold (very late moves)
            if (depth > 2 && i > LateMoveThreshold[moves.Count] && !inCheck && !givesCheck &&
                (moveCountBasedPruning || captureOrPromotion))
            {
                lmr = true;
                R = Reductions[Math.Min(depth, 63), Math.Min(i, 63)];

                int lmrDepth = Math.Max(depth - R, 0);

                if (lmrDepth < FutilityPruningMaxDepth && !inCheck &&
                    staticEvaluation + 256 + 200 * lmrDepth <= alpha)
                {
                    Board.UnmakeMove(moves[i]);

                    FutilityPrunes++;
                    continue;
                }

                // Exact PV node extension.
                //if (newExtensions < MaxExtensions && pvExact)
                //{
                //    newExtensions++;
                //    R--;
                //}

                // TT capture reduction.
                if (ttMove != null && ttMove.CapturedPieceType != Piece.None) R++;

                // Cut node reduction.
                if (cutNode) R += 2;

                // Clamp depth.
                D = Math.Max(newDepth - Math.Max(R, 0), 1);
            }


            int evaluation = -Search(NodeType.NonPV, D, plyFromRoot + 1, -(alpha + 1), -alpha, newExtensions, evaluationData, true, out Line nextLine);

            // Late move reduction failed.
            if (lmr && evaluation > alpha && D < newDepth) evaluation = -Search(NodeType.NonPV, newDepth, plyFromRoot + 1, -(alpha + 1), -alpha, newExtensions, evaluationData, !cutNode, out nextLine);

            // For PV nodes only, do a full PV search on the first move or after a fail
            // high (in the latter case search only if value < beta), otherwise let the
            // parent node fail low with value <= alpha and try another move.
            if (pvNode && (i == 0 || (evaluation > alpha && (rootNode || evaluation < beta))))
            {
                evaluation = -Search(NodeType.PV, newDepth, plyFromRoot + 1, -beta, -alpha, newExtensions, evaluationData, false, out nextLine);
            }


            //// Look for promotions that avoid stalemate.
            //if (moves[i].PromotionPiece != Piece.None && evaluation == 0)
            //{
            //    foreach (int promotionPiece in _extraPromotions)
            //    {
            //        Board.UnmakeMove(moves[i]);
            //
            //        moves[i].PromotionPiece = promotionPiece;
            //
            //        Board.MakeMove(moves[i]);
            //
            //        evaluation = -Search(newDepth - 1, plyFromRoot + 1, -beta, -alpha, newExtensions, evaluationData, out nextLine);
            //
            //        if (evaluation != 0) break;
            //    }
            //}


            Board.UnmakeMove(moves[i]);


            if (AbortSearch) return 0;
            if (evaluation > alpha)
            {
                // Fail-high.
                if (evaluation >= beta)
                {
                    TranspositionTable.StoreEvaluation(depth, plyFromRoot, beta, LowerBound, new(moves[i]));

                    if (moves[i].CapturedPieceType == Piece.None && moves[i].PromotionPiece == Piece.None)
                    {
                        HistoryHeuristic[BitboardUtility.FirstSquareIndex(moves[i].StartSquare), BitboardUtility.FirstSquareIndex(moves[i].TargetSquare)] += depth * depth;

                        StoreKillerMove(moves[i], plyFromRoot);
                    }

                    return beta;
                }

                evalType = Exact;
                bestMove = moves[i];

                alpha = evaluation;

                pvLine.Move = moves[i];
                pvLine.Next = nextLine;
            }

            // Update search progress
            if (rootNode)
            {
                _progress = ((float)i / moves.Count) * 100;
            }
        }

        // Store killer move in case the best move found is quiet, even if it didn't cause a beta cutoff.
        // Disabled because of ambiguous performance.

        // In case of an all-node, the pvLine will have a null move.
        //if (pvLine.Move?.CapturedPieceType == Piece.None)
        //{
        //    HistoryHeuristic[BitboardUtility.FirstSquareIndex(pvLine.Move.StartSquare), BitboardUtility.FirstSquareIndex(pvLine.Move.TargetSquare)] += depth * depth;
        //
        //    StoreKillerMove(pvLine.Move, plyFromRoot);
        //}

        TranspositionTable.StoreEvaluation(depth, plyFromRoot, alpha, evalType, pvLine);

        return alpha;
    }

    public static int QuiescenceSearch(NodeType nodeType, int alpha, int beta, int plyFromRoot, EvaluationData evaluationData, out Line pvLine)
    {
        pvLine = null;
        if (AbortSearch) return 0;

        bool pvNode = nodeType == NodeType.PV;

        if (plyFromRoot >= MaxPly)
        {
            MaxDepthReachedCount++;

            //if (CurrentDepthReachedData.Any(d => d.Depth == plyFromRoot)) CurrentDepthReachedData.Find(d => d.Depth == plyFromRoot).Count++;
            //else CurrentDepthReachedData.Add(new(plyFromRoot));

            return Evaluation.Evaluate(out int _, evaluationData);
        }

        var ttEntry = TranspositionTable.GetStoredEntry(out bool ttHit);
        var ttValue = ttHit ? TranspositionTable.CorrectRetrievedMateScore(ttEntry.Value, plyFromRoot) : None;
        var ttLine = ttHit ? ttEntry.Line : null;
        var ttMove = ttLine?.Move;

        // Check for a transposition table cutoff at non-PV nodes.
        if (!pvNode && ttHit &&
            ttValue != None /* From stockfish (reason unclear).*/ &&
            (ttValue >= beta ? (ttEntry.Bound & LowerBound) != 0 : (ttEntry.Bound & UpperBound) != 0))
            return ttValue;


        bool inCheck = Board.IsKingInCheck[Board.CurrentTurn];

        int staticEvaluation;
        int futilityPruningBase;

        if (inCheck)
        {
            staticEvaluation = futilityPruningBase = NegativeInfinity;
        }

        else
        {
            if (ttHit)
            {
                if (ttEntry.Value == None) staticEvaluation = Evaluation.Evaluate(out _, evaluationData);
                else staticEvaluation = ttEntry.Value;

                if (ttValue != None &&
                    (ttEntry.Bound & (ttValue > staticEvaluation ? LowerBound : UpperBound)) != 0)
                {
                    staticEvaluation = ttValue;
                }
            }

            else staticEvaluation = Evaluation.Evaluate(out _, evaluationData);

            // Stand pat.
            if (staticEvaluation >= beta)
            {
                //if (CurrentDepthReachedData.Any(d => d.Depth == plyFromRoot)) CurrentDepthReachedData.Find(d => d.Depth == plyFromRoot).Count++;
                //else CurrentDepthReachedData.Add(new(plyFromRoot));

                // TODO: Save data in transposition table.

                return staticEvaluation;
            }

            if (pvNode && staticEvaluation > alpha) alpha = staticEvaluation;

            futilityPruningBase = staticEvaluation + 153;
        }

        var moves = Board.GenerateAllLegalMoves(capturesOnly : true, promotionMode : 2);

        pvLine = new();

        if (UseMoveOrdering) OrderMoves(moves, -1);

        if (moves.Count == 0)
        {
            //if (CurrentDepthReachedData.Any(d => d.Depth == plyFromRoot)) CurrentDepthReachedData.Find(d => d.Depth == plyFromRoot).Count++;
            //else CurrentDepthReachedData.Add(new(plyFromRoot));

            // BUG: It seems that in position "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - ", after the move at index 11, the program is returning mate incorrectly.
            // If no captures are available, check if no legal moves are available and return checkmate/stalemate.
            //if (Board.GenerateAllLegalMoves(capturesOnly: false, promotionMode: 2).Count == 0)
            //{
            //    if (Board.IsKingInCheck[Board.CurrentTurn])
            //    {
            //        //if (!useNullMovePruning && nullMoveSearchPlyFromRoot == 0) _nullMoveCheckmateFound = true;
            //        return -(CheckmateScore - plyFromRoot);
            //    }
            //
            //    return 0;
            //}

            if (inCheck)
            {
                // BUG: In case of a check were no capture responses are available, a checkmate score is returned because of line 883 and because line 913 is only used when not in check (this behaviour was taken from Stockfish).
                // Temporary Fix: Return the static evaluation of the position.
                return Evaluation.Evaluate(out _, evaluationData);
            }
        }

        //bool isEndGame = gamePhase < Evaluation.EndgamePhaseScore;

        int bestValue = staticEvaluation;
        int evalType = UpperBound;

        for (int i = 0; i < moves.Count; i++)
        {
            //// Delta pruning
            //if (!isEndGame)
            //    if (Evaluation.GetPieceValue(moves[i].CapturedPieceType) + 200 <= alpha) continue;

            Board.MakeMove(moves[i]);

            bool promotion = moves[i].PromotionPiece != Piece.None;
            bool captureOrPromotion =
                moves[i].CapturedPieceType != Piece.None ||
                promotion;

            bool givesCheck = Board.IsKingInCheck[Board.CurrentTurn];

            if (!givesCheck && !IsWinningScore(-futilityPruningBase) && !promotion)
            {
                if (i > QuiescenceMoveCountPruningThreshold)
                {
                    Board.UnmakeMove(moves[i]);

                    continue;
                }

                int futilityPruningValue = futilityPruningBase + Evaluation.GetPieceValue(Board.PieceType(moves[i].TargetSquare));

                if (futilityPruningValue <= alpha)
                {
                    Board.UnmakeMove(moves[i]);

                    bestValue = Math.Max(bestValue, futilityPruningValue);
                    continue;
                }

                // TODO: skip moves with negative SEE if futilityBase <= alpha.
            }

            // TODO: skip moves with negative SEE.

            _currentSearchNodes++;
            _nodeCount++;

            int evaluation = -QuiescenceSearch(nodeType, -beta, -alpha, plyFromRoot + 1, evaluationData, out Line nextLine);

            Board.UnmakeMove(moves[i]);

            if (evaluation > alpha)
            {
                // Fail-high.
                if (evaluation >= beta)
                {
                    TranspositionTable.StoreEvaluation(0, plyFromRoot, beta, LowerBound, new(moves[i]));

                    return beta;
                }

                evalType = Exact;

                alpha = evaluation;

                pvLine.Move = moves[i];
                pvLine.Next = nextLine;
            }
        }


        TranspositionTable.StoreEvaluation(0, plyFromRoot, bestValue, evalType, pvLine);
        return bestValue;
    }


    public static void StoreKillerMove(Move move, int plyFromRoot)
    {
        if (move.Equals(KillerMoves[0, plyFromRoot])) return;

        if (KillerMoves[0, plyFromRoot] != null)
            KillerMoves[1, plyFromRoot] = new(KillerMoves[0, plyFromRoot]);

        KillerMoves[0, plyFromRoot] = move;
    }


    private static void OrderMoves(List<Move> moves, int plyFromRoot)
    {
        Move hashMove = TranspositionTable.GetStoredMove();

        var moveScores = new List<int>();
        foreach (var move in moves)
        {
            int moveScoreGuess = 0;

            if (move.Equals(hashMove))
            {
                moveScoreGuess += 30000;
            }

            // Sort captures.
            else if (move.CapturedPieceType != Piece.None)
            {
                moveScoreGuess += 10 * Evaluation.GetPieceValue(move.CapturedPieceType) - Evaluation.GetPieceValue(move.PieceType);
                moveScoreGuess += 10000;

                // MVV-LVA has poor performance compared to the above code (in terms of nodes searched).
                //moveScoreGuess = MvvLva[move.PieceType][move.CapturedPieceType];
            }

            // Sort quiet moves.
            else
            {
                // plyFromRoot -1 only occurs in quiescence search, where killer moves are not considered.
                if (plyFromRoot != -1 && move.Equals(KillerMoves[0, plyFromRoot])) moveScoreGuess += 9000;
                else if (plyFromRoot != -1 && move.Equals(KillerMoves[1, plyFromRoot])) moveScoreGuess += 8000;

                // Sort non-killer quiet moves by history heuristic, piece square tables, etc.
                else
                {
                    int targetSquareIndex = BitboardUtility.FirstSquareIndex(move.TargetSquare);

                    moveScoreGuess += HistoryHeuristic[BitboardUtility.FirstSquareIndex(move.StartSquare), targetSquareIndex];

                    //moveScoreGuess += PieceSquareTables.Read(move.PromotionPiece == Piece.None ? move.PieceType : move.PromotionPiece, targetSquareIndex, Board.CurrentTurn == 0, gamePhase);

                    moveScoreGuess += Evaluation.GetPieceValue(move.PromotionPiece);
                    if (Board.PawnAttackersTo(targetSquareIndex, Board.CurrentTurn, Board.OpponentTurn) != 0) moveScoreGuess -= 350;
                }
            }

            moveScores.Add(moveScoreGuess);
        }

        // Sort the moves list based on scores
        for (int i = 0; i < moves.Count - 1; i++)
        {
            for (int j = i + 1; j > 0; j--)
            {
                int swapIndex = j - 1;
                if (moveScores[swapIndex] < moveScores[j])
                {
                    (moves[j], moves[swapIndex]) = (moves[swapIndex], moves[j]);
                    (moveScores[j], moveScores[swapIndex]) = (moveScores[swapIndex], moveScores[j]);
                }
            }
        }
    }

    public static bool IsMateScore(int score) => Math.Abs(score) > CheckmateScore - 1000;

    public static bool IsWinningScore(int score) => score > CheckmateScore - 1000;

    private static int FutilityPruningMargin(int depth, bool improving = false) => 154 * depth;


    public static bool DrawByInsufficientMaterial()
    {
        return
            BitboardUtility.OccupiedSquaresCount(Board.AllOccupiedSquares) == 2 || /* King vs king. */
            (BitboardUtility.OccupiedSquaresCount(Board.OccupiedSquares[0]) == 2 && (Board.Bishops[0] != 0 || Board.Knights[0] != 0)) || /* King and bishop vs king or king and knight vs king. */
            (BitboardUtility.OccupiedSquaresCount(Board.OccupiedSquares[1]) == 2 && (Board.Bishops[1] != 0 || Board.Knights[1] != 0)) || /* King vs king and bishop or king vs king and knight. */
            ((BitboardUtility.OccupiedSquaresCount(Board.OccupiedSquares[0]) == 2 && BitboardUtility.OccupiedSquaresCount(Board.Bishops[0]) == 1) &&
            (BitboardUtility.OccupiedSquaresCount(Board.OccupiedSquares[1]) == 2 && BitboardUtility.OccupiedSquaresCount(Board.Bishops[1]) == 1) &&
            BitboardUtility.FirstSquareIndex(Board.Bishops[0]) % 2 == BitboardUtility.FirstSquareIndex(Board.Bishops[1]) % 2); /* King and bishop vs king and bishop with bishops of the same color. */
    }


    public static Dictionary<int, Dictionary<int, int>> MvvLva = new()
    {
        [Piece.Pawn] = new()
        {
            [Piece.Pawn] = 10105,
            [Piece.Knight] = 10205,
            [Piece.Bishop] = 10305,
            [Piece.Rook] = 10405,
            [Piece.Queen] = 10505,
            [Piece.King] = 10605
        },

        [Piece.Knight] = new()
        {
            [Piece.Pawn] = 10104,
            [Piece.Knight] = 10204,
            [Piece.Bishop] = 10304,
            [Piece.Rook] = 10404,
            [Piece.Queen] = 10504,
            [Piece.King] = 10604
        },

        [Piece.Bishop] = new()
        {
            [Piece.Pawn] = 10103,
            [Piece.Knight] = 10203,
            [Piece.Bishop] = 10303,
            [Piece.Rook] = 10403,
            [Piece.Queen] = 10503,
            [Piece.King] = 10603
        },

        [Piece.Rook] = new()
        {
            [Piece.Pawn] = 10102,
            [Piece.Knight] = 10202,
            [Piece.Bishop] = 10302,
            [Piece.Rook] = 10402,
            [Piece.Queen] = 10502,
            [Piece.King] = 10602
        },

        [Piece.Queen] = new()
        {
            [Piece.Pawn] = 10101,
            [Piece.Knight] = 10201,
            [Piece.Bishop] = 10301,
            [Piece.Rook] = 10401,
            [Piece.Queen] = 10501,
            [Piece.King] = 10601
        },

        [Piece.King] = new()
        {
            [Piece.Pawn] = 10100,
            [Piece.Knight] = 10200,
            [Piece.Bishop] = 10300,
            [Piece.Rook] = 10400,
            [Piece.Queen] = 10500,
            [Piece.King] = 10600
        },
    };


    // Values from Stockfish: https://github.com/official-stockfish/Stockfish/blob/master/src/search.cpp#L77-L80
    private static int MoveCountBasedPruningThreshold(int depth) => (3 + depth * depth) / 2;
}

[System.Serializable]
public class DepthReachedData
{
    public int Depth;
    public int Count;

    public DepthReachedData(int depth)
    {
        Depth = depth;
        Count = 1;
    }

    public DepthReachedData(DepthReachedData depthReachedData)
    {
        Depth = depthReachedData.Depth;
        Count = depthReachedData.Count;
    }
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

    //public void MakeMoves(bool removeEntries = false)
    //{
    //    Board.MakeMove(Move);
    //    if (removeEntries) AI.TranspositionTable.ClearEntry();
    //    if (Next != null) Next.MakeMoves(removeEntries);
    //}

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

public enum NodeType
{
    NonPV,
    PV
}
