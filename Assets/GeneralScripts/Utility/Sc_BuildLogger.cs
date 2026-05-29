// Mb_BuildLogger.cs

using System;
using System.IO;
using UnityEngine;

public static class Sc_BuildLogger
{
    private static string _path;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        _path = Path.Combine(Application.persistentDataPath, "build_log.txt");

        File.WriteAllText(_path,
            $"=== BUILD LOG START {DateTime.Now} ===\n\n");

        Application.logMessageReceived += HandleLog;
    }

    private static void HandleLog(string condition, string stackTrace, LogType type)
    {
        string log =
            $"[{DateTime.Now:HH:mm:ss.fff}] [{type}]\n" +
            $"{condition}\n" +
            $"{stackTrace}\n\n";

        File.AppendAllText(_path, log);
    }

    public static void Trace(string message)
    {
        string log =
            $"[{DateTime.Now:HH:mm:ss.fff}] [TRACE] {message}\n";

        File.AppendAllText(_path, log);
    }
}