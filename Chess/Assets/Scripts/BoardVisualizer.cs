using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;

public class BoardVisualizer : MonoBehaviour
{
    public static BoardVisualizer Instance;
    
    [Header("Parents")]
    [SerializeField]
    private Transform _squaresParent;
    [SerializeField]
    private Transform _piecesParent;

    [Header("Text Fields")]
    [SerializeField]
    private TextMeshProUGUI _zobristKeyText;

    [Header("Squares")]
    [SerializeField]
    private GameObject _squarePrefab;

    [Header("Colours")]
    [SerializeField]
    private Color _whiteColour;
    [SerializeField]
    private Color _blackColour;
    [SerializeField]
    private Color _whiteHighlightedColour;
    [SerializeField]
    private Color _blackHighlightedColour;
    [SerializeField]
    private Color _whiteSelectedColour;
    [SerializeField]
    private Color _blackSelectedColour;

    [Header("Pieces")]
    [SerializeField]
    private GameObject _kingW;
    [SerializeField]
    private GameObject _kingB;
    [SerializeField]
    private GameObject _queenW;
    [SerializeField]
    private GameObject _queenB;
    [SerializeField]
    private GameObject _pawnW;
    [SerializeField]
    private GameObject _pawnB;
    [SerializeField]
    private GameObject _knightW;
    [SerializeField]
    private GameObject _knightB;
    [SerializeField]
    private GameObject _bishopW;
    [SerializeField]
    private GameObject _bishopB;
    [SerializeField]
    private GameObject _rookW;
    [SerializeField]
    private GameObject _rookB;

    [Header("Board")]
    public string StartingFen;

    [Header("Timers")]
    [SerializeField]
    private string _generatePseudoLegalMovesForLegalMoves;
    [SerializeField]
    private string _generatePseudoLegalMovesForAttacks;
    [SerializeField]
    private string _printPerft;
    [SerializeField]
    private string _generateLegalMovesForList;
    [SerializeField]
    private string _generateAllLegalMoves;
    [SerializeField]
    private string _makeMove;
    [SerializeField]
    private string _unmakeMove;
    [SerializeField]
    private string _findPins;
    [SerializeField]
    private string _calculatePins;
    [SerializeField]
    private string _updateAttackedSquares;
    [SerializeField]
    private string _updatePreviousAttacks;
    [SerializeField]
    private string _findPreviousAttacks;
    [SerializeField]
    private string _search;
    [SerializeField]
    private string _generateDiagonalMoves;
    [SerializeField]
    private string _generateDiagonalMovesStep1;
    [SerializeField]
    private string _generateDiagonalMovesStep2;
    [SerializeField]
    private string _generateDiagonalMovesStep3;
    [SerializeField]
    private string _generateDiagonalMovesStep4;
    [SerializeField]
    private string _getOccupiedSquares;
    [SerializeField]
    private string _quiescenceSearch;
    [SerializeField]
    private string _getType;
    [SerializeField]
    private string _evaluation;
    [SerializeField]
    private string _orderMoves;
    [SerializeField]
    private string _generateLegalMoves;
    [SerializeField]
    private string _getStoredMoveTimer;
    [SerializeField]
    private string _storeEvaluationTimer;
    [SerializeField]
    private string _lookupEvaluationTimer;
    [SerializeField]
    private string _detectDrawByRepetitionTimer;
    [SerializeField]
    private string _evaluatePieceSquareTablesTimer;


    [Header("Counters")]
    [SerializeField]
    private string _generatePseudoLegalMovesForAttacksCounter;
    [SerializeField]
    private string _makeMoveCounter;
    [SerializeField]
    private string _evaluationCounter;
    [SerializeField]
    private string _transpositionCounter;

    [Header("Search Data")]
    [SerializeField]
    private int _depthReached;


    [HideInInspector]
    public int SelectedPiece = -1;

    private ulong _legalMoves;

    private int _promotingPiece = -1;

    public Stack<Move> _movesHistory;
    public Stack<Move> _undoneMovesHistory;

    public List<ulong> _positionHistory;


    private void Awake()
    {
        Instance = this;

        _movesHistory = new();
        _undoneMovesHistory = new();

        MoveData.ComputeMoveData();
        MoveData.GenerateDirectionalMasks();
    }

    private void Start()
    {
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++) 
            {
                var square = Instantiate(_squarePrefab, new(file - 3.5f, rank - 3.5f), Quaternion.identity, _squaresParent);

                if ((file + rank) % 2 == 0) square.GetComponent<SpriteRenderer>().color = _blackColour;
                else square.GetComponent<SpriteRenderer>().color = _whiteColour;
            }
        }

        Fen.ConvertFromFen(StartingFen);
        UpdateBoard();
    }

    private void Update()
    {
        _zobristKeyText.text = Convert.ToString((long)Board.ZobristKey, 2);

        _generatePseudoLegalMovesForLegalMoves      = Board.GeneratePseudoLegalMovesForLegalMovesTimer.Elapsed.ToString();
        _generatePseudoLegalMovesForAttacks         = Board.GeneratePseudoLegalMovesForAttacksTimer.Elapsed.ToString();
        _printPerft                                 = Board.PrintPerftTimer.Elapsed.ToString();
        _generateLegalMovesForList                  = Board.GenerateLegalMovesForListTimer.Elapsed.ToString();
        _generateAllLegalMoves                      = Board.GenerateAllLegalMovesTimer.Elapsed.ToString();
        _makeMove                                   = Board.MakeMoveTimer.Elapsed.ToString();
        _unmakeMove                                 = Board.UnmakeMoveTimer.Elapsed.ToString();
        _findPins                                   = Board.FindPinsTimer.Elapsed.ToString();
        _calculatePins                              = Board.CalculatePinsTimer.Elapsed.ToString();
        _updateAttackedSquares                      = Board.UpdateAttackedSquaresTimer.Elapsed.ToString();
        _updatePreviousAttacks                      = Board.UpdatePreviousAttackersTimer.Elapsed.ToString();
        _findPreviousAttacks                        = Board.FindPreviousAttackersTimer.Elapsed.ToString();
        _search                                     = Board.SearchTimer.Elapsed.ToString();
        _generateDiagonalMoves                      = Board.GenerateDiagonalMovesTimer.Elapsed.ToString();
        _generateDiagonalMovesStep1                 = Board.GenerateDiagonalMovesStep1Timer.Elapsed.ToString();
        _generateDiagonalMovesStep2                 = Board.GenerateDiagonalMovesStep2Timer.Elapsed.ToString();
        _generateDiagonalMovesStep3                 = Board.GenerateDiagonalMovesStep3Timer.Elapsed.ToString();
        _generateDiagonalMovesStep4                 = Board.GenerateDiagonalMovesStep4Timer.Elapsed.ToString();
        _getOccupiedSquares                         = Board.GetOccupiedSquaresTimer.Elapsed.ToString();
        _quiescenceSearch                           = Board.QuiescenceSearchTimer.Elapsed.ToString();
        _getType                                    = Board.GetTypeTimer.Elapsed.ToString();
        _evaluation                                 = Board.EvaluationTimer.Elapsed.ToString();
        _orderMoves                                 = Board.OrderMovesTimer.Elapsed.ToString();
        _generateLegalMoves                         = Board.GenerateLegalMovesTimer.Elapsed.ToString();
        _getStoredMoveTimer                         = Board.GetStoredMoveTimer.Elapsed.ToString();
        _storeEvaluationTimer                       = Board.StoreEvaluationTimer.Elapsed.ToString();
        _lookupEvaluationTimer                      = Board.LookupEvaluationTimer.Elapsed.ToString();
        _detectDrawByRepetitionTimer                = Board.DetectDrawByRepetitionTimer.Elapsed.ToString();
        _evaluatePieceSquareTablesTimer             = Board.EvaluatePieceSquareTablesTimer.Elapsed.ToString();

        _generatePseudoLegalMovesForAttacksCounter  = Board.GeneratePseudoLegalMovesForAttacksCounter.ToString();
        _makeMoveCounter                            = Board.MakeMoveCounter.ToString();
        _evaluationCounter                          = Board.EvaluationCounter.ToString();
        _transpositionCounter                       = Board.TranspositionCounter.ToString();

        _depthReached                               = Board.DepthReached;

        if (_promotingPiece != -1)
        {
            int promotion = Piece.None;
            if (Input.GetKeyDown(KeyCode.Alpha1)) promotion = Piece.Queen;
            else if (true) promotion = Piece.Bishop;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) promotion = Piece.Rook;
            else if (Input.GetKeyDown(KeyCode.Alpha4)) promotion = Piece.Knight;
            if (promotion != Piece.None)
            {
                Board.Squares[_promotingPiece] = promotion | (Board.CurrentTurn == 0 ? (int)Colour.Black : (int)Colour.White);
                Board.UpdateBitboards();
                UpdateBoard();
                _promotingPiece = -1;
            }

            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            var mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            if (mousePosition.x > 4 || mousePosition.y > 4 || mousePosition.x < -4 || mousePosition.y < -4)
            {
                SelectedPiece = -1;
                ResetBoardColors();
                return;
            }

            Vector2 selectedSquarePosition = new((int)(mousePosition.x - 4.0f), Mathf.Floor(mousePosition.y + 1.0f));

            if (SelectedPiece != -1)
            {
                int targetSquare = (int)((selectedSquarePosition.x + 3.5f) + (selectedSquarePosition.y + 3.5f) * 8);

                if ((_legalMoves & (1UL << targetSquare)) != 0)
                {
                    if (((Board.Pawns[Board.CurrentTurn] & (1UL << SelectedPiece)) != 0) &&
                        (Board.CurrentTurn == 0 ? (Board.GetRank(targetSquare) == 7) : (Board.GetRank(targetSquare) == 0))) _promotingPiece = targetSquare;
                    Move move = new(Board.Squares[SelectedPiece].PieceType(), 1UL << SelectedPiece, 1UL << targetSquare, Board.PieceType(targetSquare, Board.OpponentTurn));
                    _movesHistory.Push(move);
                    Board.MakeMove(move);
                    _positionHistory.Add(Board.ZobristKey);
                    Board.UpdateSquares();
                    UpdateBoard();
                }

                SelectedPiece = -1;
                ResetBoardColors();
            }

            SelectedPiece = (int)((selectedSquarePosition.x + 3.5f) + (selectedSquarePosition.y + 3.5f) * 8);

            // Display selected piece
            _squaresParent.GetChild(SelectedPiece).GetComponent<SpriteRenderer>().color =
                        ((Board.GetFile(SelectedPiece) + Board.GetRank(SelectedPiece)) % 2 == 0) ? _blackSelectedColour : _whiteSelectedColour;

            _legalMoves = Board.GenerateLegalMoves(1UL << SelectedPiece);
            ulong bit = 1;
            int index = 0;

            while (bit != 0)
            {
                // Display legal moves
                if ((_legalMoves & bit) != 0) 
                    _squaresParent.GetChild(index).GetComponent<SpriteRenderer>().color = 
                        ((Board.GetFile(index) + Board.GetRank(index)) % 2 == 0) ? _blackHighlightedColour : _whiteHighlightedColour;

                bit <<= 1;
                index++;
            }

        }
    }

    private void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.Space))
        {
            //AI.Instance.PlayBestMove();
        }
    }

    public void UpdateBoard()
    {
        foreach (Transform piece in _piecesParent) Destroy(piece.gameObject);

        for (int i = 0; i < 64; i++)
        {
            Vector3 position = new(Board.GetFile(i) - 3.5f, Board.GetRank(i) - 3.5f);

            switch (Board.Squares[i].PieceType())
            {
                case Piece.None:
                    continue;

                case Piece.King:
                    if (Board.Squares[i].PieceColour() == Colour.White) 
                        Instantiate(_kingW, position, Quaternion.identity, _piecesParent);
                    else Instantiate(_kingB, position, Quaternion.identity, _piecesParent);
                    break;

                case Piece.Pawn:
                    if (Board.Squares[i].PieceColour() == Colour.White)
                        Instantiate(_pawnW, position, Quaternion.identity, _piecesParent);
                    else Instantiate(_pawnB, position, Quaternion.identity, _piecesParent);
                    break;

                case Piece.Knight:
                    if (Board.Squares[i].PieceColour() == Colour.White)
                        Instantiate(_knightW, position, Quaternion.identity, _piecesParent);
                    else Instantiate(_knightB, position, Quaternion.identity, _piecesParent);
                    break;

                case Piece.Bishop:
                    if (Board.Squares[i].PieceColour() == Colour.White)
                        Instantiate(_bishopW, position, Quaternion.identity, _piecesParent);
                    else Instantiate(_bishopB, position, Quaternion.identity, _piecesParent);
                    break;

                case Piece.Rook:
                    if (Board.Squares[i].PieceColour() == Colour.White)
                        Instantiate(_rookW, position, Quaternion.identity, _piecesParent);
                    else Instantiate(_rookB, position, Quaternion.identity, _piecesParent);
                    break;

                case Piece.Queen:
                    if (Board.Squares[i].PieceColour() == Colour.White)
                        Instantiate(_queenW, position, Quaternion.identity, _piecesParent);
                    else Instantiate(_queenB, position, Quaternion.identity, _piecesParent);
                    break;
            }
        }
    }

    public void ResetBoardColors()
    {
        int index = 0;
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                var square = _squaresParent.GetChild(index);

                if ((file + rank) % 2 == 0) square.GetComponent<SpriteRenderer>().color = _blackColour;
                else square.GetComponent<SpriteRenderer>().color = _whiteColour;

                index++;
            }
        }
    }

    public void Undo()
    {
        var move = _movesHistory.Pop();
        _undoneMovesHistory.Push(move);
        Board.UnmakeMove(move);
        _positionHistory.RemoveAt(_positionHistory.Count - 1);
        Board.UpdateSquares();
        UpdateBoard();
    }

    public void Redo()
    {
        var move = _undoneMovesHistory.Pop();
        _movesHistory.Push(move);
        Board.MakeMove(move);
        _positionHistory.Add(Board.ZobristKey);
        Board.UpdateSquares();
        UpdateBoard();
    }
}
