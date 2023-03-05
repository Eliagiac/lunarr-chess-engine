using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class AI : MonoBehaviour
{
    public static AI Instance;

    public static TranspositionTable TranspositionTable;

    [SerializeField]
    private TMP_InputField text;
    [SerializeField]
    private TextMeshProUGUI _whiteWinsText;
    [SerializeField]
    private TextMeshProUGUI _blackWinsText;
    [SerializeField]
    private TextMeshProUGUI _drawsText;
    [SerializeField]
    private TextMeshProUGUI _searchTimeText;
    [SerializeField]
    private TextMeshProUGUI _depthReachedText;
    [SerializeField]
    private TextMeshProUGUI _evaluationText;
    [SerializeField]
    private TextMeshProUGUI _mainLineText;
    [SerializeField]
    private TextMeshProUGUI _searchProgressText;
    [SerializeField]
    private TextMeshProUGUI _searchSpeedText;

    [SerializeField]
    private List<int> _depths;
    [SerializeField]
    private List<string> _bestMovesThisSearch;

    [SerializeField]
    private bool _useMoveOrdering;
    [SerializeField]
    private bool _useTimeLimit;
    [SerializeField]
    private bool _useOpeningBook;
    [SerializeField, Range(0, 100)]
    private float _lateMoveReductionPercentage;
    [SerializeField]
    private int _lateMoveReductionMinimumTreshold;

    public void ToggleBook(bool value) => _useOpeningBook = value;

    [SerializeField]
    private TextAsset _bookFile;

    [Header("Transpositions")]
    [SerializeField]
    private bool _useTranspositionTable;
    [SerializeField]
    private bool _resetTranspositionTableOnEachSearch;

    [Header("Stats")]
    [SerializeField]
    private ulong _searchNodes;
    [SerializeField]
    private int _nullMovePrunes;
    [SerializeField]
    private int _maxDepthReachedCount;
    [SerializeField]
    private int _futilityPrunes;
    [SerializeField]
    private List<int> _searchPerDepthNodes;

    private string _moves;

    private const int PositiveInfinity = 9999999;
    private const int NegativeInfinity = -PositiveInfinity;
    private const int CheckmateScore = 100000;

    private int _bestEval;

    public int _currentSearchNodes;


    private System.Diagnostics.Stopwatch SearchTime = new();
    private System.Diagnostics.Stopwatch CurrentSearchTime = new();
    private float _searchTimeResult;

    private static int _timeLimit;

    //private static bool AbortSearch => Instance._useTimeLimit && (Instance.SearchTime.ElapsedMilliseconds >= _timeLimit);
    private static bool AbortSearch { get; set; }

    private static int[] _lateMoveThreshold;

    private static readonly int[] _futilityMargin = { 
        0, 
        Evaluation.StaticPieceValues[Piece.Knight].Interpolate(Evaluation.OpeningPhaseScore + 1),
        Evaluation.StaticPieceValues[Piece.Rook].Interpolate(Evaluation.OpeningPhaseScore + 1) };

    private static readonly int LimitedRazoringMargin = Evaluation.StaticPieceValues[Piece.Queen].Interpolate(Evaluation.OpeningPhaseScore + 1);

    private static bool _nullMoveCheckmateFound;

    private Book book;

    private static readonly int[] _extraPromotions = { Piece.Knight, Piece.Rook, Piece.Bishop };

    private static float _totalSearchTime;

    private bool _testing;

    public static Move[,] KillerMoves;

    public const int MaxPly = 64;

    private const int _maxKillerMoves = 2;

    // Limits the total amount of search depth extensions possible in a single branch.
    // Set to twice the search depth.
    public static int MaxExtensions;


    public EvaluationData version1;
    public EvaluationData version2;


    [SerializeField]
    private List<DepthReachedData> _depthReached;
    private static List<DepthReachedData> s_depthReached;

    private static EvaluationData evaluationData;

    public Line MainLine;
    public Line CurrentMainLine;

    private CancellationTokenSource cancelSearchTimer;

    private System.Action OnSearchComplete;

    private bool MoveFound;

    private string _evaluation;

    private static float _progress;

    private bool _versionTesting;

    private int[] AspirationWindowsMultipliers = { 4, 4 };

    public ulong _nodeCount;
    private Queue<ulong> _nodeCountHistory = new();

    public static int[,] HistoryHeuristic = new int[64, 64];

    private int _whiteWinsCount;
    private int _blackWinsCount;
    private int _drawsCount;


    private void Awake()
    {
        Instance = this;

        TranspositionTable = new(8_388_608); // 2^23

        _lateMoveThreshold = Enumerable.Range(0, 300).Select(n => (int)((n + _lateMoveReductionMinimumTreshold) - (n * Instance._lateMoveReductionPercentage / 100))).ToArray();

        version1.SetAllValuesToInspector();
        version2.SetAllValuesToInspector();
    }

    private void Start()
    {
        TranspositionTable.enabled = _useTranspositionTable;

        book = BookCreator.LoadBookFromFile(_bookFile);

        OnSearchComplete += FinishSearch;
    }

    private void FixedUpdate()
    {
        _nodeCountHistory.Enqueue(_nodeCount);

        if (_nodeCountHistory.Count >= (1 / Time.fixedDeltaTime))
        {
            _searchSpeedText.text = $"Speed: {(_nodeCount - _nodeCountHistory.Dequeue()) / 1000f} kN/s";
        }
    }

    public void PlayBestMove(EvaluationData evaluationData)
    {
        AI.evaluationData = evaluationData;
        PlayBestMove();
    }

    public void PlayBestMove()
    {
        evaluationData = version1;

        MoveFound = false;

        _timeLimit = (int)(float.Parse(Instance.text.text) * 1000);

        Task.Factory.StartNew(() => StartSearch(), TaskCreationOptions.LongRunning);

        cancelSearchTimer = new();
        if (_useTimeLimit) Task.Delay(_timeLimit, cancelSearchTimer.Token).ContinueWith((t) => StopSearch());
    }

    private void StopSearch()
    {
        if (cancelSearchTimer == null || !cancelSearchTimer.IsCancellationRequested)
        {
            AbortSearch = true;
        }
    }

    private void FinishSearch()
    {
        cancelSearchTimer?.Cancel();
        MoveFound = true;
    }

    private void Update()
    {
        Arrow.ClearArrows();
        if (MainLine?.Move != null && _evaluation != "Book") Arrow.DrawArrow2D(
            new(-3.5f + Board.GetFile(BitboardUtility.FirstSquareIndex(MainLine.Move.StartSquare)), -3.5f + Board.GetRank(BitboardUtility.FirstSquareIndex(MainLine.Move.StartSquare))),
            new(-3.5f + Board.GetFile(BitboardUtility.FirstSquareIndex(MainLine.Move.TargetSquare)), -3.5f + Board.GetRank(BitboardUtility.FirstSquareIndex(MainLine.Move.TargetSquare))),
            Color.yellow, zPos: -1);

        if (!IsMateScore(_bestEval)) _evaluationText.text = $"Evaluation: {_evaluation}";
        _depthReachedText.text = $"Depth: {Board.DepthReached} ({(_searchTimeResult / 1000).ToString("0.00")}s, tot: {(_totalSearchTime / 1000).ToString("0.00")}s)";

        _searchTimeText.text = $"Time: {(SearchTime.ElapsedMilliseconds / 1000f).ToString("0.00")}s";

        if (MainLine != null) _mainLineText.text = $"Main Line: {MainLine}";

        _searchProgressText.text = $"Progress: {_progress.ToString("0.00")}%";

        _whiteWinsText.text = $"White: {_whiteWinsCount}";
        _blackWinsText.text = $"Black: {_blackWinsCount}";
        _drawsText.text = $"Draws: {_drawsCount}";

        if (MoveFound)
        {
            MoveFound = false;

            if (MainLine?.Move == null)
            {
                Debug.Log("No moves available!");
            }

            else
            {
                //Debug.Log(MainLine.ToString());

                Board.MakeMove(MainLine.Move);
                BoardVisualizer.Instance._movesHistory.Push(MainLine.Move);
                BoardVisualizer.Instance._positionHistory.Add(Board.ZobristKey);
                Board.UpdateSquares();
                BoardVisualizer.Instance.UpdateBoard();
                BoardVisualizer.Instance.SelectedPiece = -1;
                BoardVisualizer.Instance.ResetBoardColors();
            }

            if (IsMateScore(_bestEval)) _evaluationText.text = $"Evaluation: {((_bestEval * (Board.CurrentTurn == 0 ? -1 : 1) > 0) ? "+" : "-")}M{((CheckmateScore - Mathf.Abs(_bestEval)) - 1) / 2}";

            MainLine = null;

            _depthReached.Sort((x, y) => -x.Count.CompareTo(y.Count));

            if (_versionTesting)
            {
                if (GenerateAllLegalMoves().Count != 0 && !(BoardVisualizer.Instance._positionHistory.Count(key => key == Board.ZobristKey) >= 3) && BoardVisualizer.Instance._positionHistory.Count < 400 && !DrawByInsufficientMaterial())
                    PlayBestMove(Board.CurrentTurn == 0 ? version1 : version2);

                else
                {
                    if (Board.IsKingInCheck[Board.CurrentTurn] && GenerateAllLegalMoves().Count == 0)
                    {
                        if (Board.OpponentTurn == 0)
                        {
                            Instance._whiteWinsCount++;
                            Debug.Log($"Win: White");
                        }

                        else
                        {
                            Instance._blackWinsCount++;
                            Debug.Log($"Win: Black");
                        }
                    }

                    else if (BoardVisualizer.Instance._positionHistory.Count(key => key == Board.ZobristKey) >= 3 || BoardVisualizer.Instance._positionHistory.Count >= 400 || DrawByInsufficientMaterial())
                    {
                        Instance._drawsCount++;
                        Debug.Log("Draw");
                    }

                    BoardVisualizer.Instance._positionHistory.Clear();
                    Fen.ConvertFromFen(BoardVisualizer.Instance.StartingFen);

                    PlayBestMove(Board.CurrentTurn == 0 ? version1 : version2);
                }
            }
        }
    }

    private void StartSearch()
    {
        AbortSearch = false;

        _progress = 0;
        _searchNodes = 0;

        s_depthReached = new();

        _searchPerDepthNodes = new();

        MainLine = new();
        CurrentMainLine = new();

        _bestMovesThisSearch = new();

        if (_resetTranspositionTableOnEachSearch) TranspositionTable.Clear();

        if (_useOpeningBook && book.HasPosition(Board.ZobristKey))
        {
            MainLine.Move = book.GetRandomBookMoveWeighted(Board.ZobristKey);
            foreach (var bitboard in Board.Pieces)
            {
                if ((bitboard.Value[Board.OpponentTurn] & MainLine.Move.TargetSquare) != 0 && MainLine.Move.CapturedPieceType == Piece.None)
                {
                    MainLine.Move.CapturedPieceType = bitboard.Key;
                    break;
                }
            }

            _evaluation = "Book";

            OnSearchComplete.Invoke();
        }

        else
        {
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
                KillerMoves = new Move[_maxKillerMoves, MaxPly];

                CurrentSearchTime.Restart();

                s_depthReached = new();
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
                    Instance._depthReached = new(s_depthReached);
                    _searchTimeResult = CurrentSearchTime.ElapsedMilliseconds;
                    //Debug.Log($"{depth} : {CurrentSearchTime.Elapsed.ToString()}, ({alpha}, {beta}).");
                    _bestEval = evaluation;
                    _bestMovesThisSearch.Add(CurrentMainLine.Move.ToString());
                    MainLine = new(CurrentMainLine);
                    _evaluation = (evaluation * (Board.CurrentTurn == 1 ? -0.01f : 0.01f)).ToString("0.00");
                    _searchNodes += (ulong)_currentSearchNodes;
                    _searchPerDepthNodes.Add(_currentSearchNodes);
                }

                depth++;
            }
            while (!AbortSearch && (_useTimeLimit || depth <= int.Parse(text.text))); // && !IsMateScore(evaluation));
            SearchTime.Stop();

            OnSearchComplete.Invoke();
            
            Board.SearchTimer.Stop();
        }
    }

    public void PrintPerft(TMP_InputField text)
    {
        Board.PrintPerftTimer.Start();
        int depth;
        if (int.TryParse(text.text, out depth)) Debug.Log(Perft(depth, depth));
        Debug.Log(_moves);
        Board.UpdateSquares();
        BoardVisualizer.Instance.UpdateBoard();
        Board.PrintPerftTimer.Stop();
    }

    public int Perft(int depth, int startingDepth, Move previousMove = null)
    {
        if (depth == startingDepth - 1 && depth == 0)
        {
            _moves += previousMove + ": 1\n";
            Debug.Log(previousMove + ": 1");
        }

        if (depth == 0)
        {
            return 1;
        }

        Board.GenerateAllLegalMovesTimer.Start();
        var moves = GenerateAllLegalMoves();
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
            Debug.Log(previousMove + ": " + numPositions);
        }

        return numPositions;
    }

    public void PrintEvaluation()
    {
        if (evaluationData == null) evaluationData = version1;
        Debug.Log(Evaluation.Evaluate(out int _, evaluationData) * (Board.CurrentTurn == 1 ? -1 : 1));
    }


    public static int Search(int depth, int plyFromRoot, int alpha, int beta, int extensions, EvaluationData evaluationData, out Line pvLine, bool useNullMovePruning = true, int nullMoveSearchPlyFromRoot = 0, ulong previousCapture = 0, bool useMultiCut = true)
    {
        pvLine = null;

        if (AbortSearch) return 0;
        //Instance._currentSearchNodes++;
        //Instance._nodeCount++;

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

        // Mate distance pruning.
        if (plyFromRoot > 0)
        {
            alpha = Mathf.Max(alpha, -CheckmateScore + plyFromRoot);
            beta = Mathf.Min(beta, CheckmateScore - plyFromRoot);

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

        if (depth <= 0)
        {
            return QuiescenceSearch(alpha, beta, plyFromRoot, evaluationData, out pvLine);
        }

        pvLine = new();


        bool inCheck = Board.IsKingInCheck[Board.CurrentTurn];

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
                Instance._nullMovePrunes++;

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


        int staticEvaluation = Evaluation.Evaluate(out int gamePhase, evaluationData);

        // Futility pruning condition.
        bool useFutilityPruning = false;
        if (depth < 3 && !inCheck)
        {
            if (staticEvaluation + _futilityMargin[depth] <= alpha)
            {
                useFutilityPruning = true;
            }
        }


        // Razoring.
        // Inspired by Strelka: https://www.chessprogramming.org/Razoring#Strelka.
        // As implemented in Wukong JS: https://github.com/maksimKorzh/wukongJS/blob/main/wukong.js#L1575-L1591.
        if (plyFromRoot > 0)
        {
            int score = staticEvaluation + Evaluation.StaticPieceValues[Piece.Pawn].Interpolate(Evaluation.OpeningPhaseScore + 1);
            if (score < beta)
            {
                if (depth == 1)
                {
                    int newScore = QuiescenceSearch(alpha, beta, plyFromRoot, evaluationData, out Line _);
                    return Mathf.Max(newScore, score);
                }

                score += Evaluation.StaticPieceValues[Piece.Pawn].Interpolate(Evaluation.OpeningPhaseScore + 1);
                if (score < beta && depth <= 3)
                {
                    int newScore = QuiescenceSearch(alpha, beta, plyFromRoot, evaluationData, out Line _);
                    if (newScore < beta) return Mathf.Max(newScore, score);
                }
            }
        }


        // Check extension.
        if (extensions < MaxExtensions && inCheck)
        {
            extensions++;
            depth++;
        }


        if (plyFromRoot >= MaxPly)
        {
            Instance._maxDepthReachedCount++;

            if (s_depthReached.Any(d => d.Depth == plyFromRoot)) s_depthReached.Find(d => d.Depth == plyFromRoot).Count++;
            else s_depthReached.Add(new(plyFromRoot));

            return Evaluation.Evaluate(out int _, evaluationData);
        }


        Move bestMove = null;

        Board.GenerateAllLegalMovesTimer.Start();
        var moves = GenerateAllLegalMoves();
        if (Instance._useMoveOrdering) OrderMoves(moves, plyFromRoot, gamePhase);
        Board.GenerateAllLegalMovesTimer.Stop();

        if (moves.Count == 0)
        {
            if (s_depthReached.Any(d => d.Depth == plyFromRoot)) s_depthReached.Find(d => d.Depth == plyFromRoot).Count++;
            else s_depthReached.Add(new(plyFromRoot));

            if (Board.IsKingInCheck[Board.CurrentTurn])
            {
                if (!useNullMovePruning && nullMoveSearchPlyFromRoot == 0) _nullMoveCheckmateFound = true;
                return -(CheckmateScore - plyFromRoot);
            }
            return 0;
        }


        // One reply extension.
        if (extensions < MaxExtensions && moves.Count == 1)
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


            // Futility pruning.
            if (useFutilityPruning && i > 0 &&
                moves[i].CapturedPieceType == Piece.None &&
                moves[i].PromotionPiece == Piece.None &&
                !Board.IsKingInCheck[Board.CurrentTurn])
            {
                Board.UnmakeMoveTimer.Start();
                Board.UnmakeMove(moves[i]);
                Board.UnmakeMoveTimer.Stop();

                Instance._futilityPrunes++;

                continue;
            }


            // Depth reductions/extensions.
            int R = 1;
            int newExtensions = 0;

            // Late move reduction.
            bool LMR = false;
            // Only reduce if depth > 2, we aren't in check and the move doesn't give check to the opponent.
            if (depth > 2 && i > _lateMoveThreshold[moves.Count] && !inCheck && !Board.IsKingInCheck[Board.CurrentTurn])
            {
                // Don't reduce captures, promotions and killer moves
                if (moves[i].CapturedPieceType == Piece.None &&
                    moves[i].PromotionPiece == Piece.None &&
                    !moves[i].Equals(KillerMoves[0, plyFromRoot]) &&
                    !moves[i].Equals(KillerMoves[1, plyFromRoot]))
                {
                    LMR = true;
                    R++;
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
            if (evaluation >= beta)
            {
                Board.StoreEvaluationTimer.Start();
                TranspositionTable.StoreEvaluation(depth, plyFromRoot, beta, TranspositionTable.LowerBound, new(moves[i]));
                Board.StoreEvaluationTimer.Stop();

                HistoryHeuristic[BitboardUtility.FirstSquareIndex(moves[i].StartSquare), BitboardUtility.FirstSquareIndex(moves[i].TargetSquare)] += depth * depth;

                if (moves[i].CapturedPieceType == Piece.None)
                {
                    StoreKillerMove(moves[i], plyFromRoot);
                }

                return beta;
            }

            if (evaluation > alpha)
            {
                evalType = TranspositionTable.Exact;
                bestMove = moves[i];

                alpha = evaluation;

                pvLine.Move = moves[i];
                pvLine.Next = nextLine;

                //if (plyFromRoot == 0 && !AbortSearch)
                //{
                //    Instance.CurrentMainLine.Move = moves[i];
                //}
            }

            // Update search progress
            if (plyFromRoot == 0)
            {
                _progress = ((float)i / moves.Count) * 100;
            }
        }

        Board.StoreEvaluationTimer.Start();
        TranspositionTable.StoreEvaluation(depth, plyFromRoot, alpha, evalType, pvLine);
        Board.StoreEvaluationTimer.Stop();

        return alpha;
    }

    public static int QuiescenceSearch(int alpha, int beta, int plyFromRoot, EvaluationData evaluationData, out Line pvLine)
    {
        pvLine = null;

        //Instance._currentSearchNodes++;
        //Instance._nodeCount++;

        Board.QuiescenceSearchTimer.Start();
        if (AbortSearch) return 0;


        // Standing pat.
        int evaluation = Evaluation.Evaluate(out int gamePhase, evaluationData);
        if (evaluation >= beta)
        {
            Board.QuiescenceSearchTimer.Stop();

            if (s_depthReached.Any(d => d.Depth == plyFromRoot)) s_depthReached.Find(d => d.Depth == plyFromRoot).Count++;
            else s_depthReached.Add(new(plyFromRoot));

            return beta;
        }

        if (alpha < evaluation) alpha = evaluation;


        if (plyFromRoot >= MaxPly)
        {
            Instance._maxDepthReachedCount++;

            if (s_depthReached.Any(d => d.Depth == plyFromRoot)) s_depthReached.Find(d => d.Depth == plyFromRoot).Count++;
            else s_depthReached.Add(new(plyFromRoot));

            return evaluation;
        }

        var moves = GenerateAllLegalMoves(true);

        if (moves.Count == 0)
        {
            Board.QuiescenceSearchTimer.Stop();

            if (s_depthReached.Any(d => d.Depth == plyFromRoot)) s_depthReached.Find(d => d.Depth == plyFromRoot).Count++;
            else s_depthReached.Add(new(plyFromRoot));

            return alpha;
        }

        pvLine = new();

        if (Instance._useMoveOrdering) OrderMoves(moves, -1, gamePhase, false);

        bool isEndGame = gamePhase < Evaluation.EndgamePhaseScore;

        foreach (var move in moves)
        {
            ulong oldKey = Board.ZobristKey;
            ulong correctOldKey = Zobrist.CalculateZobristKey();

            // Delta pruning
            if (!isEndGame)
                if (Evaluation.GetPieceValue(move.CapturedPieceType, Board.OpponentTurn, gamePhase) + 200 <= alpha) continue;

            Board.MakeMoveTimer.Start();
            Board.MakeMove(move);
            Board.MakeMoveTimer.Stop();

            evaluation = -QuiescenceSearch(-beta, -alpha, plyFromRoot + 1, evaluationData, out Line nextLine);

            Board.UnmakeMoveTimer.Start();
            Board.UnmakeMove(move);
            Board.UnmakeMoveTimer.Stop();

            if (evaluation >= beta)
            {
                Board.QuiescenceSearchTimer.Stop();

                return beta;
            }

            if (evaluation > alpha)
            {
                alpha = evaluation;

                pvLine.Move = move;
                pvLine.Next = nextLine;
            }
        }

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
        return Mathf.Abs(score) > CheckmateScore - 1000;
    }


    public void StartVersionTest()
    {
        if (!_versionTesting)
        {
            _versionTesting = true;
            PlayBestMove(Board.CurrentTurn == 0 ? version1 : version2);
        }
        else _versionTesting = false;
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

    public void MakeMoves(bool removeEntries = false)
    {
        Board.MakeMove(Move);
        if (removeEntries) AI.TranspositionTable.ClearEntry();
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
