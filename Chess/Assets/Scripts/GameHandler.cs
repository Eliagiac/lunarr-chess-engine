using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using System.IO;
using System;
using System.Linq;

public class GameHandler : MonoBehaviour
{
    public static string FenString = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    public static bool IsSearching;

    public static List<string> MovesHistory = new();


    private static string Path;
    private static Process Engine;
    private static StreamWriter Input;


    private void Start()
    {
        Path = Application.dataPath;
        Path = Path.Substring(0, Path.IndexOf("chess") + 5);
        Path += "/Chess Engine UCI/Chess Engine UCI/bin/Debug/net7.0/Chess Engine UCI.exe";

        Engine = new Process();
        Engine.StartInfo.FileName = Path;
        Engine.StartInfo.UseShellExecute = false;
        Engine.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        Engine.StartInfo.RedirectStandardInput = true;
        Engine.StartInfo.RedirectStandardOutput = true;
        Engine.OutputDataReceived += new((s, e) => { ProcessOutput(e.Data); });

        Engine.Start();
        Engine.BeginOutputReadLine();

        Input = Engine.StandardInput;

        Engine.PriorityClass = ProcessPriorityClass.High;


        // We can safely start searching now because BoardVisualizer.Start()
        // excecutes first (see script excecution order settings).
        Input.WriteLine($"position {FenString}");
        StartSearching();
    }

    private void OnApplicationQuit()
    {
        Engine.Kill();
    }


    public static void MakeMove(string move)
    {
        MovesHistory.Add(move);
        string test = $"position {FenString} moves {MovesHistory.Aggregate((a, b) => $"{a} {b}")}";
        Input.WriteLine($"position {FenString} moves {MovesHistory.Aggregate((a, b) => $"{a} {b}")}");
        StartSearching();
    }

    public static void StartSearching()
    {
        Input.WriteLine("go infinite");
        IsSearching = true;
    }

    public static void StartSearching(int timeLimit)
    {
        Input.WriteLine($"go movetime {timeLimit * 1000}");
        IsSearching = true;
    }

    // Must be called only if IsSearching is currently true.
    public static void PlayBestMove()
    {
        Input.WriteLine("stop");
    }

    private static void ProcessOutput(string output)
    {
        UnityEngine.Debug.Log(output);

        string[] outputs = output.Split(' ');

        switch (outputs[0])
        {
            case "info":
                return;

            case "bestmove":

                int startSquare = (int)Enum.Parse(typeof(Square), outputs[1].Substring(0, 2));
                int targetSquare = (int)Enum.Parse(typeof(Square), outputs[1].Substring(2));

                Move move = new(Board.PieceType(startSquare), 1UL << startSquare, 1UL << targetSquare, Board.PieceType(targetSquare));
                BoardVisualizer.Instance._movesHistory.Push(move);
                Board.MakeMove(move);
                MakeMove(move.ToString());
                Board.UpdateSquares();
                BoardVisualizer.Instance.updateBoard = true;

                break;
        }
    }
}
