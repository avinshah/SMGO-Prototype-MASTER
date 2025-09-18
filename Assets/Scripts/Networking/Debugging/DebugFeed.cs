using System.Collections.Generic;
using UnityEngine;

public static class DebugFeed
{
    const int Max = 30;
    static readonly Queue<string> _lines = new Queue<string>(Max + 1);

    public static void Log(string msg)
    {
        var line = $"{Time.timeSinceLevelLoad:F1}s  {msg}";
        if (_lines.Count >= Max) _lines.Dequeue();
        _lines.Enqueue(line);
        Debug.Log(line);
    }



    public static IEnumerable<string> Lines => _lines;
}
