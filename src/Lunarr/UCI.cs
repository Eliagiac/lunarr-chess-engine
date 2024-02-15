using System;
using System.Collections.Generic;
using static System.Math;
using static Engine;

class UCI
{
    /// <summary>
    /// Update the UI with new search data.
    /// </summary>
    public static void PV(int depth, int seldepth, int multipv, int evaluation, string evaluationType, int nodes, int nps, int hashfull, long time, string pv)
    {
        Console.WriteLine($"info " +
            $"depth {depth} " +
            $"seldepth {seldepth} " +
            $"multipv {multipv} " +
            $"score " +
                (!IsMateScore(evaluation) ?
                $"cp {evaluation} " :
                $"mate {((evaluation > 0) ? "+" : "-")}{Ceiling((Checkmate - Abs(evaluation)) / 2.0)} ") +
            (evaluationType != "" ? $"{evaluationType} " : "") + 
            $"nodes {nodes} " +
            $"nps {nps} " +
            $"hashfull {hashfull} " +
            $"time {time} " +
            $"pv {pv}");
    }

    /// <summary>
    /// Update the UI with new search data.
    /// </summary>
    public static void Bestmove(string bestmove)
    {
        Console.WriteLine($"bestmove {bestmove}");
    }

    /// <summary>
    /// Update the UI with the current search progress.
    /// </summary>
    public static void Currmove(int depth, string currmove, int currmovenumber)
    {
        Console.WriteLine($"info " +
            $"depth {depth} " +
            $"currmove {currmove} " +
            $"currmovenumber {currmovenumber} ");
    }
}
