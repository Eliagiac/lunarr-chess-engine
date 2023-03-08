# Chess Engine (unrated*)

A Unity chess engine written in C#. It will soon support the [UCI protocol](https://wbec-ridderkerk.nl/html/UCIProtocol.html) as well.

## Board Representation and Move Generation

The chess board is stored as 12 [bitboards](https://www.chessprogramming.org/Bitboards), one for each piece, where each positive bit signifies an occupied square.

Move generation uses standard algorithms for sliding pieces and pre-computed move data for the others.

Move legality is checked using data such as attack maps that the program updates every time a move is made.

## Search

The Search function is based on the [Negamax](https://www.chessprogramming.org/Negamax) algorithm, and it uses many popular techniques to improve speed and playing strength, including the following:
- [Alpha-Beta Pruning](https://www.chessprogramming.org/Alpha-Beta)
- [Transposition Table](https://www.chessprogramming.org/Transposition_Table)
- [Iterative Deepening](https://www.chessprogramming.org/Iterative_Deepening)
- [Aspiration Windows](https://www.chessprogramming.org/Aspiration_Windows)
- [Quiescence Search](https://www.chessprogramming.org/Quiescence_Search)
- [Depth Extensions](https://www.chessprogramming.org/Extensions):
  - [Checkmate Threat Extensions](https://www.chessprogramming.org/Mate_Threat_Extensions)
  - [Check Extensions](https://www.chessprogramming.org/Check_Extensions)
  - [One Reply Extensions](https://www.chessprogramming.org/One_Reply_Extensions)
  - [Passed Pawn Extensions](https://www.chessprogramming.org/Passed_Pawn_Extensions)
  - Currently supported extension, disabled because of poor performance, include:
    - [Capture Extensions](https://www.chessprogramming.org/Capture_Extensions)
    - [Recapture Extensions](https://www.chessprogramming.org/Recapture_Extensions)
    - Promotion Extensions
- [Pruning Techniques](https://www.chessprogramming.org/Pruning):
  - [Null Move Pruning](https://www.chessprogramming.org/Null_Move_Pruning)
  - [Futility Pruning](https://www.chessprogramming.org/Futility_Pruning)
  - [Standing Pat in Quiescence Search](https://www.chessprogramming.org/Quiescence_Search#StandPat)
  - [Delta Pruning in Quiescence Search](https://www.chessprogramming.org/Delta_Pruning)
  - [Razoring](https://www.chessprogramming.org/Razoring)
  - [Late Move Reductions (LMR)](https://www.chessprogramming.org/Late_Move_Reductions)
  - Currently supported pruning techniques, disabled because of poor performance, include:
    - [Multi-Cut](https://www.chessprogramming.org/Multi-Cut)

## Evaluation

Most of the evaluation constants currently used are from [Stockfish](https://github.com/official-stockfish/Stockfish). This, however, was only a recent change: before the project was made public, values from [WukongJS](https://github.com/maksimKorzh/wukongJS) were used.

Each score used is separated into an opening value and an endgame value, which are interpolated based on the game phase.

Thorough testing is still necessary on most of the evaluation parameters.

Some of the evaluation features include:
- [Material](https://www.chessprogramming.org/Material)
- [Piece-Square Tables](https://www.chessprogramming.org/Piece-Square_Tables)
- [Pawn Structure](https://www.chessprogramming.org/Pawn_Structure)
- [Outposts](https://www.chessprogramming.org/Outposts)
- [Bishop and Knight Pair](https://www.chessprogramming.org/Bishop_Pair)
- [Color Weakness](https://www.chessprogramming.org/Color_Weakness)
- [Basic King Safety](https://www.chessprogramming.org/King_Safety)
- [Basic Mobility](https://www.chessprogramming.org/Mobility)

##

*The engine currently has serious speed limitations, and it doesn't compare to most other engines in terms of playing strength. It's estimated to be between 1000 and 1400 Elo.
