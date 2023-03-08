using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TMPro;

public class AI : MonoBehaviour
{
    public static AI Instance;

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
    public List<string> _bestMovesThisSearch;

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
    [SerializeField]
    private int _shallowDepthThreshold;

    public void ToggleBook(bool value) => _useOpeningBook = value;

    [SerializeField]
    private TextAsset _bookFile;

    [Header("Transpositions")]
    [SerializeField]
    private bool _useTranspositionTable;
    [SerializeField]
    private bool _resetTranspositionTableOnEachSearch;

    [Header("Stats")]
    public ulong _searchNodes;
    public int _nullMovePrunes;
    public int _maxDepthReachedCount;
    public int _futilityPrunes;
    public int _moveCountBasedPrunes;
    public List<int> _searchNodesPerDepth;
    public List<DepthReachedData> _depthReached;

    public EvaluationData version1;
    public EvaluationData version2;

    public static Book book;


    private void Awake()
    {
        Instance = this;

        AIPlayer.LateMoveReductionMinimumTreshold   = _lateMoveReductionMinimumTreshold;
        AIPlayer.LateMoveReductionPercentage        = _lateMoveReductionPercentage;

        AIPlayer.Init();

        version1.SetAllValuesToInspector();
        version2.SetAllValuesToInspector();
    }

    private void Start()
    {
        AIPlayer.TranspositionTable.enabled = _useTranspositionTable;

        book = BookCreator.LoadBookFromFile(_bookFile);
    }

    private void Update()
    {
        Arrow.ClearArrows();
        if (AIPlayer.MainLine?.Move != null && AIPlayer._evaluation != "Book") Arrow.DrawArrow2D(
            new(-3.5f + Board.GetFile(BitboardUtility.FirstSquareIndex(AIPlayer.MainLine.Move.StartSquare)), -3.5f + Board.GetRank(BitboardUtility.FirstSquareIndex(AIPlayer.MainLine.Move.StartSquare))),
            new(-3.5f + Board.GetFile(BitboardUtility.FirstSquareIndex(AIPlayer.MainLine.Move.TargetSquare)), -3.5f + Board.GetRank(BitboardUtility.FirstSquareIndex(AIPlayer.MainLine.Move.TargetSquare))),
            Color.yellow, zPos: -1);

        if (!AIPlayer.IsMateScore(AIPlayer._bestEval)) _evaluationText.text = $"Evaluation: {AIPlayer._evaluation}";
        _depthReachedText.text = $"Depth: {Board.DepthReached} ({(AIPlayer._searchTimeResult / 1000).ToString("0.00")}s, tot: {(AIPlayer._totalSearchTime / 1000).ToString("0.00")}s)";

        _searchTimeText.text = $"Time: {(AIPlayer.SearchTime.ElapsedMilliseconds / 1000f).ToString("0.00")}s";

        if (AIPlayer.MainLine != null) _mainLineText.text = $"Main Line: {AIPlayer.MainLine}";

        _searchProgressText.text = $"Progress: {AIPlayer._progress.ToString("0.00")}%";

        _whiteWinsText.text = $"White: {AIPlayer._whiteWinsCount}";
        _blackWinsText.text = $"Black: {AIPlayer._blackWinsCount}";
        _drawsText.text = $"Draws: {AIPlayer._drawsCount}";

        if (AIPlayer.MoveFound)
        {
            AIPlayer.MoveFound = false;

            if (AIPlayer.MainLine?.Move == null)
            {
                Debug.Log("No moves available!");
            }

            else
            {
                //Debug.Log(MainLine.ToString());

                Board.MakeMove(AIPlayer.MainLine.Move);
                BoardVisualizer.Instance._movesHistory.Push(AIPlayer.MainLine.Move);
                BoardVisualizer.Instance._positionHistory.Add(Board.ZobristKey);
                Board.UpdateSquares();
                BoardVisualizer.Instance.UpdateBoard();
                BoardVisualizer.Instance.SelectedPiece = -1;
                BoardVisualizer.Instance.ResetBoardColors();
            }

            if (AIPlayer.IsMateScore(AIPlayer._bestEval)) _evaluationText.text = $"Evaluation: {((AIPlayer._bestEval * (Board.CurrentTurn == 0 ? -1 : 1) > 0) ? "+" : "-")}M{((AIPlayer.CheckmateScore - Mathf.Abs(AIPlayer._bestEval)) - 1) / 2}";

            AIPlayer.MainLine = null;

            _depthReached.Sort((x, y) => -x.Count.CompareTo(y.Count));

            if (AIPlayer._versionTesting)
            {
                if (AIPlayer.GenerateAllLegalMoves().Count != 0 && !(BoardVisualizer.Instance._positionHistory.Count(key => key == Board.ZobristKey) >= 3) && BoardVisualizer.Instance._positionHistory.Count < 400 && !AIPlayer.DrawByInsufficientMaterial())
                    AIPlayer.PlayBestMove(Board.CurrentTurn == 0 ? version1 : version2);

                else
                {
                    if (Board.IsKingInCheck[Board.CurrentTurn] && AIPlayer.GenerateAllLegalMoves().Count == 0)
                    {
                        if (Board.OpponentTurn == 0)
                        {
                            AIPlayer._whiteWinsCount++;
                            Debug.Log($"Win: White");
                        }

                        else
                        {
                            AIPlayer._blackWinsCount++;
                            Debug.Log($"Win: Black");
                        }
                    }

                    else if (BoardVisualizer.Instance._positionHistory.Count(key => key == Board.ZobristKey) >= 3 || BoardVisualizer.Instance._positionHistory.Count >= 400 || AIPlayer.DrawByInsufficientMaterial())
                    {
                        AIPlayer._drawsCount++;
                        Debug.Log("Draw");
                    }

                    BoardVisualizer.Instance._positionHistory.Clear();
                    Fen.ConvertFromFen(BoardVisualizer.Instance.StartingFen);

                    AIPlayer.PlayBestMove(Board.CurrentTurn == 0 ? version1 : version2);
                }
            }
        }
    }

    private void FixedUpdate()
    {
        AIPlayer._nodeCountHistory.Enqueue(AIPlayer._nodeCount);

        if (AIPlayer._nodeCountHistory.Count >= (1 / Time.fixedDeltaTime))
        {
            _searchSpeedText.text = $"Speed: {(AIPlayer._nodeCount - AIPlayer._nodeCountHistory.Dequeue()) / 1000f} kN/s";
        }
    }


    public void PrintPerft(TMP_InputField text)
    {
        Board.PrintPerftTimer.Start();
        int depth;
        if (int.TryParse(text.text, out depth)) Debug.Log(Perft(depth, depth));
        Debug.Log(AIPlayer._moves);
        Board.UpdateSquares();
        BoardVisualizer.Instance.UpdateBoard();
        Board.PrintPerftTimer.Stop();
    }

    public int Perft(int depth, int startingDepth, Move previousMove = null)
    {
        if (depth == startingDepth - 1 && depth == 0)
        {
            AIPlayer._moves += previousMove + ": 1\n";
            Debug.Log(previousMove + ": 1");
        }

        if (depth == 0)
        {
            return 1;
        }

        Board.GenerateAllLegalMovesTimer.Start();
        var moves = AIPlayer.GenerateAllLegalMoves();
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
            AIPlayer._moves += previousMove + ": " + numPositions + "\n";
            Debug.Log(previousMove + ": " + numPositions);
        }

        return numPositions;
    }

    public void PrintEvaluation()
    {
        if (AIPlayer.evaluationData == null) AIPlayer.evaluationData = version1;
        Debug.Log(Evaluation.Evaluate(out int _, AIPlayer.evaluationData) * (Board.CurrentTurn == 1 ? -1 : 1));
    }


    public void PlayBestMove()
    {
        AIPlayer.TimeLimit = (float.Parse(AI.Instance.text.text));
        AIPlayer.UseTimeLimit = _useTimeLimit;
        AIPlayer.UseMoveOrdering = _useMoveOrdering;
        AIPlayer.ResetTranspositionTableOnEachSearch = _resetTranspositionTableOnEachSearch;
        AIPlayer.UseOpeningBook = _useOpeningBook;
        AIPlayer.ShallowDepthThreshold = _shallowDepthThreshold;

        AIPlayer.PlayBestMove();
    }

    public void StartVersionTest()
    {
        if (!AIPlayer._versionTesting)
        {
            AIPlayer._versionTesting = true;
            AIPlayer.PlayBestMove(Board.CurrentTurn == 0 ? version1 : version2);
        }
        else AIPlayer._versionTesting = false;
    }
}
