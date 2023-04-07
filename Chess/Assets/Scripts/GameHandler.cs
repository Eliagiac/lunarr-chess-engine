using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using System.IO;
using System;
using System.Linq;

public class GameHandler : MonoBehaviour
{
    public static bool IsSearching;

    public static List<string> MovesHistory = new();


    private void Start()
    {
        UCI.StartEngine();

        // We can safely start searching now because BoardVisualizer.Start()
        // excecutes first (see script excecution order settings).
        UCI.SendInput($"position {BoardVisualizer.Instance.StartingFen}");
        StartSearching();
    }


    public static void MakeMove(Move move)
    {
        MovesHistory.Add(move.ToString());
        BoardVisualizer.Instance._movesHistory.Push(move);

        UCI.SendInput($"position {BoardVisualizer.Instance.StartingFen} moves {MovesHistory.Aggregate((a, b) => $"{a} {b}")}");

        BoardVisualizer.Instance.IsBoardUpdateNeeded = true;

        // Can safely start searching because the displayed board has been saved on the previous line.
        StartSearching();
    }

    public static void StartSearching()
    {
        UCI.SendInput("go infinite");
        IsSearching = true;
    }

    public static void StartSearching(int timeLimit)
    {
        UCI.SendInput($"go movetime {timeLimit * 1000}");
        IsSearching = true;
    }

    // Must be called only if IsSearching is currently true.
    public static void PlayBestMove()
    {
        UCI.SendInput("stop");
    }
}
