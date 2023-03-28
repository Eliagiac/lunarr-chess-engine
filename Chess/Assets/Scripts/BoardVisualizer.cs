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

    [Header("Colors")]
    [SerializeField]
    private Color _whiteColor;
    [SerializeField]
    private Color _blackColor;
    [SerializeField]
    private Color _whiteHighlightedColor;
    [SerializeField]
    private Color _blackHighlightedColor;
    [SerializeField]
    private Color _whiteSelectedColor;
    [SerializeField]
    private Color _blackSelectedColor;

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

    public bool updateBoard;


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

                if ((file + rank) % 2 == 0) square.GetComponent<SpriteRenderer>().color = _blackColor;
                else square.GetComponent<SpriteRenderer>().color = _whiteColor;
            }
        }

        Fen.ConvertFromFen(StartingFen);
        UpdateBoard();
    }

    private void Update()
    {
        _zobristKeyText.text = Convert.ToString((long)Board.ZobristKey, 2);

        if (_promotingPiece != -1)
        {
            int promotion = Piece.None;
            if (Input.GetKeyDown(KeyCode.Alpha1)) promotion = Piece.Queen;
            else if (true) promotion = Piece.Bishop;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) promotion = Piece.Rook;
            else if (Input.GetKeyDown(KeyCode.Alpha4)) promotion = Piece.Knight;
            if (promotion != Piece.None)
            {
                Board.Squares[_promotingPiece] = promotion | (Board.CurrentTurn == 0 ? (int)Piece.Black : (int)Piece.White);
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
                    Move move = new(Board.PieceType(SelectedPiece), 1UL << SelectedPiece, 1UL << targetSquare, Board.PieceType(targetSquare));
                    _movesHistory.Push(move);
                    Board.MakeMove(move);
                    GameHandler.MakeMove(move.ToString());
                    Board.UpdateSquares();
                    UpdateBoard();
                }

                SelectedPiece = -1;
                ResetBoardColors();
            }

            SelectedPiece = (int)((selectedSquarePosition.x + 3.5f) + (selectedSquarePosition.y + 3.5f) * 8);

            // Display selected piece
            _squaresParent.GetChild(SelectedPiece).GetComponent<SpriteRenderer>().color =
                        ((Board.GetFile(SelectedPiece) + Board.GetRank(SelectedPiece)) % 2 == 0) ? _blackSelectedColor : _whiteSelectedColor;

            _legalMoves = Board.GenerateLegalMovesBitboard(1UL << SelectedPiece);
            ulong bit = 1;
            int index = 0;

            while (bit != 0)
            {
                // Display legal moves
                if ((_legalMoves & bit) != 0) 
                    _squaresParent.GetChild(index).GetComponent<SpriteRenderer>().color = 
                        ((Board.GetFile(index) + Board.GetRank(index)) % 2 == 0) ? _blackHighlightedColor : _whiteHighlightedColor;

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

        if (updateBoard) UpdateBoard();
    }

    public void UpdateBoard()
    {
        while (_piecesParent.childCount > 0)
        {
            DestroyImmediate(_piecesParent.GetChild(0).gameObject);
        }

        for (int i = 0; i < 64; i++)
        {
            Vector3 position = new(Board.GetFile(i) - 3.5f, Board.GetRank(i) - 3.5f);

            switch (Board.PieceType(i))
            {
                case Piece.None:
                    continue;

                case Piece.King:
                    if (Board.Squares[i].PieceColor() == Piece.White) 
                        Instantiate(_kingW, position, Quaternion.identity, _piecesParent);
                    else Instantiate(_kingB, position, Quaternion.identity, _piecesParent);
                    break;

                case Piece.Pawn:
                    if (Board.Squares[i].PieceColor() == Piece.White)
                        Instantiate(_pawnW, position, Quaternion.identity, _piecesParent);
                    else Instantiate(_pawnB, position, Quaternion.identity, _piecesParent);
                    break;

                case Piece.Knight:
                    if (Board.Squares[i].PieceColor() == Piece.White)
                        Instantiate(_knightW, position, Quaternion.identity, _piecesParent);
                    else Instantiate(_knightB, position, Quaternion.identity, _piecesParent);
                    break;

                case Piece.Bishop:
                    if (Board.Squares[i].PieceColor() == Piece.White)
                        Instantiate(_bishopW, position, Quaternion.identity, _piecesParent);
                    else Instantiate(_bishopB, position, Quaternion.identity, _piecesParent);
                    break;

                case Piece.Rook:
                    if (Board.Squares[i].PieceColor() == Piece.White)
                        Instantiate(_rookW, position, Quaternion.identity, _piecesParent);
                    else Instantiate(_rookB, position, Quaternion.identity, _piecesParent);
                    break;

                case Piece.Queen:
                    if (Board.Squares[i].PieceColor() == Piece.White)
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

                if ((file + rank) % 2 == 0) square.GetComponent<SpriteRenderer>().color = _blackColor;
                else square.GetComponent<SpriteRenderer>().color = _whiteColor;

                index++;
            }
        }
    }

    public void Undo()
    {
        var move = _movesHistory.Pop();
        _undoneMovesHistory.Push(move);
        Board.UnmakeMove(move);
        Board.UpdateSquares();
        UpdateBoard();
    }

    public void Redo()
    {
        var move = _undoneMovesHistory.Pop();
        _movesHistory.Push(move);
        Board.MakeMove(move);
        Board.UpdateSquares();
        UpdateBoard();
    }
}
