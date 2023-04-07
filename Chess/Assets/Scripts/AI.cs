using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    //public List<DepthReachedData> _depthReached;
    //
    //public EvaluationData version1;
    //public EvaluationData version2;

    public static Book book;


    private void Awake()
    {
        Instance = this;

        Zobrist.Init();

        MoveData.ComputeMoveData();
        MoveData.GenerateDirectionalMasks();
        MoveData.ComputeMagicBitboards();

        Engine.UseMoveOrdering = true;
        Engine.UseOpeningBook = false;
        Engine.LateMoveReductionMinimumTreshold = 1;
        Engine.LateMoveReductionPercentage = 100;
        Engine.UseTranspositionTable = true;
        Engine.ResetTranspositionTableOnEachSearch = false;
        Engine.ShallowDepthThreshold = 8;
        Engine.UseOpeningBook = false;
        Engine.InternalIterativeDeepeningDepthReduction = 5;
        Engine.ProbCutDepthReduction = 4;
        Engine.MultiPvCount = 1;
        
        Engine.Init();

        //Fen.ConvertFromFen(Fen.StartingFen);

        //AIPlayer.LateMoveReductionMinimumTreshold   = _lateMoveReductionMinimumTreshold;
        //AIPlayer.LateMoveReductionPercentage        = _lateMoveReductionPercentage;
        //
        //AIPlayer.Init();
        //
        //version1.SetAllValuesToInspector();
        //version2.SetAllValuesToInspector();
    }

    private void Start()
    {
        //AIPlayer.TranspositionTable.enabled = _useTranspositionTable;

        //book = BookCreator.LoadBookFromFile(_bookFile);
    }

    private void Update()
    {
        _searchSpeedText.text = $"Speed: {Engine.NodesPerSecond} kN/s";

        //Arrow.ClearArrows();
        //if (AIPlayer.MainLine?.Move != null && AIPlayer._evaluation != "Book") Arrow.DrawArrow2D(
        //    new(-3.5f + Board.GetFile(BitboardUtility.FirstSquareIndex(AIPlayer.MainLine.Move.StartSquare)), -3.5f + Board.GetRank(BitboardUtility.FirstSquareIndex(AIPlayer.MainLine.Move.StartSquare))),
        //    new(-3.5f + Board.GetFile(BitboardUtility.FirstSquareIndex(AIPlayer.MainLine.Move.TargetSquare)), -3.5f + Board.GetRank(BitboardUtility.FirstSquareIndex(AIPlayer.MainLine.Move.TargetSquare))),
        //    Color.yellow, zPos: -1);
        //
        //if (!AIPlayer.IsMateScore(AIPlayer._bestEval)) _evaluationText.text = $"Evaluation: {AIPlayer._evaluation}";
        _depthReachedText.text = $"Depth: {Engine.Depth} ({(Engine._searchTimeResult / 1000).ToString("0.00")}s, tot: {(Engine._totalSearchTime / 1000).ToString("0.00")}s)";
        //
        _searchTimeText.text = $"Time: {(Engine.SearchTime.ElapsedMilliseconds / 1000f).ToString("0.00")}s";
        //
        //if (AIPlayer.MainLine != null) _mainLineText.text = $"Main Line: {AIPlayer.MainLine}";
        //
        //_searchProgressText.text = $"Progress: {AIPlayer._progress.ToString("0.00")}%";
        //
        //_whiteWinsText.text = $"White: {AIPlayer._whiteWinsCount}";
        //_blackWinsText.text = $"Black: {AIPlayer._blackWinsCount}";
        //_drawsText.text = $"Draws: {AIPlayer._drawsCount}";
        //
        //if (AIPlayer.MoveFound)
        //{
        //    AIPlayer.MoveFound = false;
        //
        //    if (AIPlayer.MainLine?.Move == null)
        //    {
        //        Debug.Log("No moves available!");
        //    }
        //
        //    else
        //    {
        //        //Debug.Log(MainLine.ToString());
        //
        //        Board.MakeMove(AIPlayer.MainLine.Move);
        //        BoardVisualizer.Instance._movesHistory.Push(AIPlayer.MainLine.Move);
        //        Board.UpdateSquares();
        //        BoardVisualizer.Instance.UpdateBoard();
        //        BoardVisualizer.Instance.SelectedPiece = -1;
        //        BoardVisualizer.Instance.ResetBoardColors();
        //    }
        //
        //    if (AIPlayer.IsMateScore(AIPlayer._bestEval)) _evaluationText.text = $"Evaluation: {((AIPlayer._bestEval * (Board.CurrentTurn == 0 ? -1 : 1) > 0) ? "+" : "-")}M{((AIPlayer.CheckmateScore - Mathf.Abs(AIPlayer._bestEval)) - 1) / 2}";
        //
        //    AIPlayer.MainLine = null;
        //
        //    _depthReached.Sort((x, y) => -x.Count.CompareTo(y.Count));
        //
        //    if (AIPlayer._versionTesting)
        //    {
        //        if (AIPlayer.GenerateAllLegalMoves().Count != 0 && !(Board.PositionHistory.Count(key => key == Board.ZobristKey) >= 3) && Board.PositionHistory.Count < 400 && !AIPlayer.DrawByInsufficientMaterial())
        //            AIPlayer.PlayBestMove(Board.CurrentTurn == 0 ? version1 : version2);
        //
        //        else
        //        {
        //            if (Board.IsKingInCheck[Board.CurrentTurn] && AIPlayer.GenerateAllLegalMoves().Count == 0)
        //            {
        //                if (Board.OpponentTurn == 0)
        //                {
        //                    AIPlayer._whiteWinsCount++;
        //                    Debug.Log($"Win: White");
        //                }
        //
        //                else
        //                {
        //                    AIPlayer._blackWinsCount++;
        //                    Debug.Log($"Win: Black");
        //                }
        //            }
        //
        //            else if (Board.PositionHistory.Count(key => key == Board.ZobristKey) >= 3 || Board.PositionHistory.Count >= 400 || AIPlayer.DrawByInsufficientMaterial())
        //            {
        //                AIPlayer._drawsCount++;
        //                Debug.Log("Draw");
        //            }
        //
        //            Board.PositionHistory.Clear();
        //            Fen.ConvertFromFen(BoardVisualizer.Instance.StartingFen);
        //
        //            AIPlayer.PlayBestMove(Board.CurrentTurn == 0 ? version1 : version2);
        //        }
        //    }
        //}
    }

    public void PlayBestMove()
    {
        //AIPlayer.TimeLimit = (float.Parse(AI.Instance.text.text));
        //AIPlayer.UseTimeLimit = _useTimeLimit;
        //AIPlayer.UseMoveOrdering = _useMoveOrdering;
        //AIPlayer.ResetTranspositionTableOnEachSearch = _resetTranspositionTableOnEachSearch;
        //AIPlayer.UseOpeningBook = _useOpeningBook;
        //AIPlayer.ShallowDepthThreshold = _shallowDepthThreshold;
        //
        //AIPlayer.PlayBestMove();

        if (GameHandler.IsSearching) GameHandler.PlayBestMove();
    }

    public void StartVersionTest()
    {
        //if (!AIPlayer._versionTesting)
        //{
        //    AIPlayer._versionTesting = true;
        //    AIPlayer.PlayBestMove(Board.CurrentTurn == 0 ? version1 : version2);
        //}
        //else AIPlayer._versionTesting = false;
    }
}
