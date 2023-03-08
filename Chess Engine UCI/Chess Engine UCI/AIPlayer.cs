using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class AIPlayer
{
    public static TranspositionTable TranspositionTable;

    public static string _moves;

    public const int PositiveInfinity = 9999999;
    public const int NegativeInfinity = -PositiveInfinity;
    public const int CheckmateScore = 100000;

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
    public static bool ResetTranspositionTableOnEachSearch;
    public static bool UseOpeningBook;
    public static List<DepthReachedData> DepthReachedData;
    public static List<DepthReachedData> CurrentDepthReachedData;
    public static int NullMovePrunes;
    public static int MaxDepthReachedCount;
    public static int FutilityPrunes;
    public static int ShallowDepthThreshold;
    public static int MoveCountBasedPrunes;

    public static int Ply;

    public static int OptimumTime;
    public static int MaximumTime;


    public static void Init()
    {
        TranspositionTable = new(8_388_608); // 2^23

        LateMoveThreshold = Enumerable.Range(0, 300).Select(n => (int)((n + LateMoveReductionMinimumTreshold) - (n * LateMoveReductionPercentage / 100))).ToArray();

        for (int depth = 1; depth < 64; depth++)
        {
            for (int count = 1; count < 64; count++)
            {
                float d = (float)System.Math.Log(depth);
                float c = (float)System.Math.Log(count);
                Reductions[depth, count] = Math.Max((int)Math.Round(d * c / 2f) - 1, 0);
            }
        }

        OnSearchComplete += FinishSearch;
    }


    public static int Perft(int depth, int startingDepth, Move previousMove = null)
    {
        if (depth == startingDepth - 1 && depth == 0)
        {
            _moves += previousMove + ": 1\n";
            Console.WriteLine(previousMove + ": 1");
        }

        if (depth == 0)
        {
            return 1;
        }

        Board.GenerateAllLegalMovesTimer.Start();
        var moves = GenerateAllLegalMoves(promotionMode : 0);
        Board.GenerateAllLegalMovesTimer.Stop();

        int numPositions = 0;

        foreach (var move in moves)
        {

            Board.MakeMoveTimer.Start();
            Board.MakeMove(move);
            Board.MakeMoveTimer.Stop();

            numPositions += Perft(depth - 1, startingDepth, move);

            Board.UnmakeMoveTimer.Start();
            Board.UnmakeMove(move);
            Board.UnmakeMoveTimer.Stop();
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

        // Opening book not available in UCI mode.
        Board.SearchTimer.Start();

        SearchTime.Restart();
        int depth = 1;

        int evaluation;

        int alpha = NegativeInfinity;
        int beta = PositiveInfinity;

        int alphaWindow = 25;
        int betaWindow = 25;
        do
        {
            // Reset on each iteration because of better performance.
            // This behaviour is not expected and further research is required.
            KillerMoves = new Move[_maxKillerMoves, MaxPly];

            CurrentSearchTime.Restart();
            _progress = 0;

            CurrentDepthReachedData = new();
            MaxExtensions = depth * 2;


            evaluation = Search(depth, 0, alpha, beta, 0, evaluationData, out CurrentMainLine);

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

                Board.DepthReached = depth;
                DepthReachedData = new(CurrentDepthReachedData);
                _searchTimeResult = CurrentSearchTime.ElapsedMilliseconds;
                //Debug.Log($"{depth} : {CurrentSearchTime.Elapsed.ToString()}, ({alpha}, {beta}).");
                _bestEval = evaluation;
                BestMovesThisSearch.Add(CurrentMainLine.Move.ToString());
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

        Board.SearchTimer.Stop();
    }

    public static int Search(int depth, int plyFromRoot, int alpha, int beta, int extensions, EvaluationData evaluationData, out Line pvLine, bool useNullMovePruning = true, int nullMoveSearchPlyFromRoot = 0, ulong previousCapture = 0, bool useMultiCut = true)
    {
        pvLine = null;
        if (AbortSearch) return 0;

        // Detect draws by repetition.
        Board.DetectDrawByRepetitionTimer.Start();
        if (plyFromRoot > 0)
        {
            if (Board.PositionHistory.Count(key => key == Board.ZobristKey) >= 2)
            {
                Board.DetectDrawByRepetitionTimer.Stop();
                return 0;
            }
        }
        Board.DetectDrawByRepetitionTimer.Stop();

        if (depth <= 0)
        {
            return QuiescenceSearch(alpha, beta, plyFromRoot, evaluationData, out pvLine);
        }

        // Mate distance pruning.
        if (plyFromRoot > 0)
        {
            alpha = Math.Max(alpha, -CheckmateScore + plyFromRoot);
            beta = Math.Min(beta, CheckmateScore - plyFromRoot);

            if (alpha >= beta)
            {
                return alpha;
            }
        }

        Board.LookupEvaluationTimer.Start();
        int ttVal = TranspositionTable.LookupEvaluation(depth, plyFromRoot, alpha, beta);
        Board.LookupEvaluationTimer.Stop();

        // Don't use traspositions at ply from root 0 to analyse possible draws by repetition.
        if (ttVal != TranspositionTable.lookupFailed && plyFromRoot > 0)
        {
            Board.TranspositionCounter++;
            pvLine = TranspositionTable.GetStoredLine();
            return ttVal;
        }

        pvLine = new();


        bool inCheck = Board.IsKingInCheck[Board.CurrentTurn];
        int staticEvaluation = Evaluation.Evaluate(out int gamePhase, evaluationData);

        // Razoring.
        // Inspired by Strelka: https://www.chessprogramming.org/Razoring#Strelka.
        // As implemented in Wukong JS: https://github.com/maksimKorzh/wukongJS/blob/main/wukong.js#L1575-L1591.
        if (plyFromRoot > 0)
        {
            int score = staticEvaluation +
                (inCheck ? 600 : // Larger margin if we are in check.
                Evaluation.StaticPieceValues[Piece.Pawn][0]);

            if (score < beta)
            {
                if (depth == 1)
                {
                    int newScore = QuiescenceSearch(alpha, beta, plyFromRoot, evaluationData, out Line _);
                    return Math.Max(newScore, score);
                }

                score +=
                    inCheck ? 600 : // Larger margin if we are in check.
                    Evaluation.StaticPieceValues[Piece.Pawn][0];

                if (score < beta && depth <= 3)
                {
                    int newScore = QuiescenceSearch(alpha, beta, plyFromRoot, evaluationData, out Line _);
                    if (newScore < beta) return Math.Max(newScore, score);
                }
            }
        }

        // Futility pruning condition.
        bool useFutilityPruning = false;
        if (plyFromRoot > 0 && depth < 3 && !inCheck)
        {
            // Should also check if eval is a mate score, 
            // otherwise the engine will be blind to certain checkmates.

            if (staticEvaluation + _futilityMargin[depth] <= alpha)
            {
                useFutilityPruning = true;
            }
        }

        // Null move pruning.
        if (depth > 2 && useNullMovePruning && plyFromRoot > 0 && !inCheck)
        {
            NullMove move = new();
            Board.MakeNullMove(move);

            const int R = 3;
            int evaluation = -Search(depth - R, plyFromRoot, -beta, -beta + 1, extensions, evaluationData, out Line _, useNullMovePruning: false, nullMoveSearchPlyFromRoot: 0);

            Board.UnmakeNullMove(move);

            if (AbortSearch) return 0;
            if (evaluation >= beta)
            {
                NullMovePrunes++;

                return beta;
            }

            // Checkmate threat extension.
            if (extensions < MaxExtensions && _nullMoveCheckmateFound)
            {
                extensions++;
                depth++;
            }
            _nullMoveCheckmateFound = false;
        }


        // Check extension.
        if (plyFromRoot > 0 && extensions < MaxExtensions && inCheck)
        {
            extensions++;
            depth++;
        }


        if (plyFromRoot >= MaxPly)
        {
            MaxDepthReachedCount++;

            if (CurrentDepthReachedData.Any(d => d.Depth == plyFromRoot)) CurrentDepthReachedData.Find(d => d.Depth == plyFromRoot).Count++;
            else CurrentDepthReachedData.Add(new(plyFromRoot));

            return Evaluation.Evaluate(out int _, evaluationData);
        }


        Move bestMove = null;

        Board.GenerateAllLegalMovesTimer.Start();
        var moves = GenerateAllLegalMoves();
        if (UseMoveOrdering) OrderMoves(moves, plyFromRoot, gamePhase);
        Board.GenerateAllLegalMovesTimer.Stop();

        if (moves.Count == 0)
        {
            if (CurrentDepthReachedData.Any(d => d.Depth == plyFromRoot)) CurrentDepthReachedData.Find(d => d.Depth == plyFromRoot).Count++;
            else CurrentDepthReachedData.Add(new(plyFromRoot));

            if (Board.IsKingInCheck[Board.CurrentTurn])
            {
                if (!useNullMovePruning && nullMoveSearchPlyFromRoot == 0) _nullMoveCheckmateFound = true;
                return -(CheckmateScore - plyFromRoot);
            }
            return 0;
        }


        // One reply extension.
        if (plyFromRoot > 0 && extensions < MaxExtensions && moves.Count == 1)
        {
            extensions++;
            depth++;
        }


        // Multi-cut.
        // Temporarily disabled because results are highly inconsistent: it often gives misleading results, causing nonsense moves to be played.
        // Occasionally it does make the search faster, but it more often makes it slower.
        //if (plyFromRoot > 0 && depth >= 3 && useMultiCut)
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


        int evalType = TranspositionTable.UpperBound;

        for (int i = 0; i < moves.Count; i++)
        {
            Board.MakeMoveTimer.Start();
            Board.MakeMove(moves[i]);
            Board.MakeMoveTimer.Stop();


            bool captureOrPromotion =
                moves[i].CapturedPieceType != Piece.None ||
                moves[i].PromotionPiece != Piece.None;

            bool givesCheck = Board.IsKingInCheck[Board.CurrentTurn];

            // Futility pruning.
            if (useFutilityPruning && i > 0 &&
                !captureOrPromotion && !givesCheck)
            {
                Board.UnmakeMoveTimer.Start();
                Board.UnmakeMove(moves[i]);
                Board.UnmakeMoveTimer.Stop();

                FutilityPrunes++;
                continue;
            }

            // Move count based pruning.
            // Inspired by Stockfish: https://github.com/official-stockfish/Stockfish/blob/master/src/search.cpp#L1005
            bool moveCountBasedPruning = plyFromRoot > 0 &&
                depth < ShallowDepthThreshold &&
                i > MoveCountBasedPruningThreshold(depth);

            // Skip quiet late moves at shallow depths.
            if (moveCountBasedPruning &&
                !captureOrPromotion && !givesCheck)
            {
                Board.UnmakeMoveTimer.Start();
                Board.UnmakeMove(moves[i]);
                Board.UnmakeMoveTimer.Stop();

                MoveCountBasedPrunes++;
                continue;
            }


            _currentSearchNodes++;
            _nodeCount++;


            // Depth reductions/extensions.
            int R = 1;
            int newExtensions = 0;

            // Late move reduction.
            bool LMR = false;
            // Only reduce if depth > 2, we aren't in check and the move doesn't give check to the opponent.
            if (depth > 2 && i > LateMoveThreshold[moves.Count] && !inCheck && !givesCheck)
            {
                // Don't reduce captures, promotions and killer moves, unless we are past the moveCountBasedPruningThreshold (very late moves)
                if (moveCountBasedPruning ||
                    (moves[i].CapturedPieceType == Piece.None &&
                    moves[i].PromotionPiece == Piece.None &&
                    !moves[i].Equals(KillerMoves[0, plyFromRoot]) &&
                    !moves[i].Equals(KillerMoves[1, plyFromRoot])))
                {
                    LMR = true;
                    R += Reductions[Math.Min(depth, 63), Math.Min(i, 63)];
                }
            }

            // Capture extension.
            //if (newExtensions < MaxExtensions && moves[i].CapturedPieceType != Piece.None)
            //{
            //    newExtensions++;
            //    R--;
            //}

            // Recapture extension.
            //if (newExtensions < MaxExtensions && (moves[i].TargetSquare & previousCapture) != 0)
            //{
            //    newExtensions++;
            //    R--;
            //}

            // Passed pawn extension.
            if (newExtensions < MaxExtensions &&
                moves[i].PieceType == Piece.Pawn &&
                ((moves[i].TargetSquare & Mask.SeventhRank) != 0))
            {
                newExtensions++;
                R--;
            }

            // Promotion extension.
            //if (newExtensions < MaxExtensions &&
            //    moves[i].PromotionPiece != Piece.None)
            //{
            //    newExtensions++;
            //    R--;
            //}


            int evaluation = -Search(depth - R, plyFromRoot + 1, -beta, -alpha, extensions + newExtensions, evaluationData, out Line nextLine, previousCapture: moves[i].CapturedPieceType != Piece.None ? moves[i].TargetSquare : 0);

            // Late move reduction failed.
            if (LMR && evaluation > alpha) evaluation = -Search(depth - --R, plyFromRoot + 1, -beta, -alpha, extensions + newExtensions, evaluationData, out nextLine, nullMoveSearchPlyFromRoot: plyFromRoot + 1, previousCapture: moves[i].CapturedPieceType != Piece.None ? moves[i].TargetSquare : 0);


            // Look for promotions that avoid stalemate.
            if (moves[i].PromotionPiece != Piece.None && evaluation == 0)
            {
                foreach (int promotionPiece in _extraPromotions)
                {
                    Board.UnmakeMoveTimer.Start();
                    Board.UnmakeMove(moves[i]);
                    Board.UnmakeMoveTimer.Stop();

                    moves[i].PromotionPiece = promotionPiece;

                    Board.MakeMoveTimer.Start();
                    Board.MakeMove(moves[i]);
                    Board.MakeMoveTimer.Stop();

                    evaluation = -Search(depth - 1, plyFromRoot + 1, -beta, -alpha, extensions + newExtensions, evaluationData, out nextLine, previousCapture: moves[i].CapturedPieceType != Piece.None ? moves[i].TargetSquare : 0);

                    if (evaluation != 0) break;
                }
            }

            // Try to avoid draws by repetition.
            //if (plyFromRoot == 0)
            //{
            //    // This move would cause an immediate draw by repetition.
            //    if (BoardVisualizer.Instance._positionHistory.Count(key => key == Board.ZobristKey) >= 2)
            //    {
            //        evaluation = 0;
            //        //Debug.Log("Draw by repetition detected!");
            //    }
            //
            //    // This move would allow the opponent to cause a draw by repetition.
            //    // Only prevent it if drawing wouldn't be beneficial.
            //    if (evaluation > 0)
            //    {
            //        var opponentResponses = GenerateAllLegalMoves();
            //        foreach (var response in opponentResponses)
            //        {
            //            Board.MakeMoveTimer.Start();
            //            Board.MakeMove(response);
            //            Board.MakeMoveTimer.Stop();
            //    
            //            if (BoardVisualizer.Instance._positionHistory.Count(key => key == Board.ZobristKey) >= 2) evaluation = 0;
            //    
            //            Board.UnmakeMoveTimer.Start();
            //            Board.UnmakeMove(response);
            //            Board.UnmakeMoveTimer.Stop();
            //    
            //            if (evaluation == 0) break;
            //        }
            //    }
            //}


            Board.UnmakeMoveTimer.Start();
            Board.UnmakeMove(moves[i]);
            Board.UnmakeMoveTimer.Stop();


            if (AbortSearch) return 0;
            if (evaluation > alpha)
            {
                // Fail-high.
                if (evaluation >= beta)
                {
                    Board.StoreEvaluationTimer.Start();
                    TranspositionTable.StoreEvaluation(depth, plyFromRoot, beta, TranspositionTable.LowerBound, new(moves[i]));
                    Board.StoreEvaluationTimer.Stop();

                    if (moves[i].CapturedPieceType == Piece.None)
                    {
                        HistoryHeuristic[BitboardUtility.FirstSquareIndex(moves[i].StartSquare), BitboardUtility.FirstSquareIndex(moves[i].TargetSquare)] += depth * depth;

                        StoreKillerMove(moves[i], plyFromRoot);
                    }

                    return beta;
                }

                evalType = TranspositionTable.Exact;
                bestMove = moves[i];

                alpha = evaluation;

                pvLine.Move = moves[i];
                pvLine.Next = nextLine;
            }

            // Update search progress
            if (plyFromRoot == 0)
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

        Board.StoreEvaluationTimer.Start();
        TranspositionTable.StoreEvaluation(depth, plyFromRoot, alpha, evalType, pvLine);
        Board.StoreEvaluationTimer.Stop();

        return alpha;
    }

    public static int QuiescenceSearch(int alpha, int beta, int plyFromRoot, EvaluationData evaluationData, out Line pvLine)
    {
        pvLine = null;
        if (AbortSearch) return 0;

        Board.QuiescenceSearchTimer.Start();

        Board.LookupEvaluationTimer.Start();
        int ttVal = TranspositionTable.LookupEvaluation(0, plyFromRoot, alpha, beta);
        Board.LookupEvaluationTimer.Stop();

        if (ttVal != TranspositionTable.lookupFailed)
        {
            Board.TranspositionCounter++;
            pvLine = TranspositionTable.GetStoredLine();
            return ttVal;
        }

        // Standing pat.
        int evaluation = Evaluation.Evaluate(out int gamePhase, evaluationData);
        if (evaluation >= beta)
        {
            Board.QuiescenceSearchTimer.Stop();

            if (CurrentDepthReachedData.Any(d => d.Depth == plyFromRoot)) CurrentDepthReachedData.Find(d => d.Depth == plyFromRoot).Count++;
            else CurrentDepthReachedData.Add(new(plyFromRoot));

            return beta;
        }

        if (alpha < evaluation) alpha = evaluation;


        if (plyFromRoot >= MaxPly)
        {
            MaxDepthReachedCount++;

            if (CurrentDepthReachedData.Any(d => d.Depth == plyFromRoot)) CurrentDepthReachedData.Find(d => d.Depth == plyFromRoot).Count++;
            else CurrentDepthReachedData.Add(new(plyFromRoot));

            return evaluation;
        }

        var moves = GenerateAllLegalMoves(true);

        if (moves.Count == 0)
        {
            Board.QuiescenceSearchTimer.Stop();

            if (CurrentDepthReachedData.Any(d => d.Depth == plyFromRoot)) CurrentDepthReachedData.Find(d => d.Depth == plyFromRoot).Count++;
            else CurrentDepthReachedData.Add(new(plyFromRoot));

            return alpha;
        }

        pvLine = new();

        if (UseMoveOrdering) OrderMoves(moves, -1, gamePhase, false);

        bool isEndGame = gamePhase < Evaluation.EndgamePhaseScore;

        int evalType = TranspositionTable.UpperBound;

        foreach (var move in moves)
        {
            // Delta pruning
            if (!isEndGame)
                if (Evaluation.GetPieceValue(move.CapturedPieceType, Board.OpponentTurn, gamePhase) + 200 <= alpha) continue;

            Board.MakeMoveTimer.Start();
            Board.MakeMove(move);
            Board.MakeMoveTimer.Stop();

            _currentSearchNodes++;
            _nodeCount++;

            evaluation = -QuiescenceSearch(-beta, -alpha, plyFromRoot + 1, evaluationData, out Line nextLine);

            Board.UnmakeMoveTimer.Start();
            Board.UnmakeMove(move);
            Board.UnmakeMoveTimer.Stop();

            if (evaluation >= beta)
            {
                Board.StoreEvaluationTimer.Start();
                TranspositionTable.StoreEvaluation(0, plyFromRoot, beta, TranspositionTable.LowerBound, new(move));
                Board.StoreEvaluationTimer.Stop();

                Board.QuiescenceSearchTimer.Stop();
                return beta;
            }

            if (evaluation > alpha)
            {
                evalType = TranspositionTable.Exact;

                alpha = evaluation;

                pvLine.Move = move;
                pvLine.Next = nextLine;
            }
        }


        Board.StoreEvaluationTimer.Start();
        TranspositionTable.StoreEvaluation(0, plyFromRoot, alpha, evalType, pvLine);
        Board.StoreEvaluationTimer.Stop();

        Board.QuiescenceSearchTimer.Stop();
        return alpha;
    }


    public static void StoreKillerMove(Move move, int plyFromRoot)
    {
        //Move firstKillerMove = KillerMoves[0, plyFromRoot];
        //
        //// Only store the move if it's not already the first.
        //if (move.Equals(firstKillerMove)) return;

        //// Shift the previous moves up by 1.
        //const int startingIndex = _maxKillerMoves - 1;
        //for (int i = startingIndex; i > 0; i--)
        //    KillerMoves[i, plyFromRoot] = KillerMoves[i - 1, plyFromRoot];

        // Store the move in the first slot.
        //KillerMoves[0, plyFromRoot] = move;

        if (KillerMoves[0, plyFromRoot] != null)
            KillerMoves[1, plyFromRoot] = new(KillerMoves[0, plyFromRoot]);

        KillerMoves[0, plyFromRoot] = move;
    }


    public static List<Move> GenerateAllLegalMoves(bool capturesOnly = false, int promotionMode = 2)
    {
        List<Move> moves = new();

        foreach (var bitboard in Board.Pieces)
        {
            ulong bit = 1;
            while (bit != 0)
            {
                if ((bitboard.Value[Board.CurrentTurn] & bit) != 0)
                {
                    moves.AddRange(Board.GenerateLegalMovesList(bitboard.Value[Board.CurrentTurn] & bit, promotionMode, capturesOnly));
                }

                bit <<= 1;
            }
        }

        return moves;
    }

    private static void OrderMoves(List<Move> moves, int plyFromRoot, int gamePhase, bool useTranspositionTable = true)
    {
        Board.OrderMovesTimer.Start();

        Move hashMove = null;
        Board.GetStoredMoveTimer.Start();
        if (useTranspositionTable) hashMove = TranspositionTable.GetStoredMove();
        Board.GetStoredMoveTimer.Stop();

        var moveScores = new List<int>();
        foreach (var move in moves)
        {
            int moveScoreGuess = 0;

            // Give priority to the best moves that were previosly found.
            //if (plyFromRoot == 0 && Instance._bestMovesThisSearch.Count > 0 && move.Equals(Instance._bestMovesThisSearch[-1])) moveScoreGuess += 2000;

            if (move.Equals(hashMove)) moveScoreGuess += 30000;

            // Sort captures.
            else if (move.CapturedPieceType != Piece.None)
            {
                moveScoreGuess += 10 * Evaluation.GetPieceValue(move.CapturedPieceType, Board.OpponentTurn, gamePhase) - Evaluation.GetPieceValue(move.PieceType, Board.CurrentTurn, gamePhase);
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
                    moveScoreGuess += HistoryHeuristic[BitboardUtility.FirstSquareIndex(move.StartSquare), BitboardUtility.FirstSquareIndex(move.TargetSquare)];


                    Board.EvaluatePieceSquareTablesTimer.Start();
                    moveScoreGuess += PieceSquareTables.Read(move.PromotionPiece == Piece.None ? move.PieceType : move.PromotionPiece, BitboardUtility.FirstSquareIndex(move.TargetSquare), Board.CurrentTurn == 0, gamePhase);
                    Board.EvaluatePieceSquareTablesTimer.Stop();

                    moveScoreGuess += Evaluation.GetPieceValue(move.PromotionPiece, Board.CurrentTurn, gamePhase);
                    if ((Board.PawnAttackedSquares[Board.OpponentTurn] & move.TargetSquare) != 0) moveScoreGuess -= 350;
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

        Board.OrderMovesTimer.Stop();
    }

    public static bool IsMateScore(int score)
    {
        return Math.Abs(score) > CheckmateScore - 1000;
    }


    public static bool DrawByInsufficientMaterial()
    {
        return
            BitboardUtility.OccupiedSquaresCount(Board.AllOccupiedSquares) == 2 || /* King vs king. */
            (BitboardUtility.OccupiedSquaresCount(Board.OccupiedSquares[0]) == 2 && (Board.Bishops[0] != 0 || Board.Knights[0] != 0)) || /* King and bishop vs king or king and knight vs king. */
            (BitboardUtility.OccupiedSquaresCount(Board.OccupiedSquares[1]) == 2 && (Board.Bishops[1] != 0 || Board.Knights[1] != 0)) || /* King vs king and bishop or king vs king and knight. */
            ((BitboardUtility.OccupiedSquaresCount(Board.OccupiedSquares[0]) == 2 && BitboardUtility.OccupiedSquaresCount(Board.Bishops[0]) == 1) &&
            (BitboardUtility.OccupiedSquaresCount(Board.OccupiedSquares[1]) == 2 && BitboardUtility.OccupiedSquaresCount(Board.Bishops[1]) == 1) &&
            BitboardUtility.FirstSquareIndex(Board.Bishops[0]) % 2 == BitboardUtility.FirstSquareIndex(Board.Bishops[1]) % 2); /* King and bishop vs king and bishop with bishops of the same colour. */
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
