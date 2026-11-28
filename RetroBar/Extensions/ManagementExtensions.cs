using System;
using System.Collections.Generic;
using System.IO;
using RetroBar.Utilities;

internal static class ManagementExtensions
{
    public static string InLocalAppData(this string path1, string path2 = "")
    {
        string root = EvaluationSession.DataDirectory;
        string result = Path.GetFullPath(Path.Combine(root, path1, path2));
        EvaluationSession.RequireSafeProfilePath(result);
        return result;
    }

    public static void AddFrom(this List<string> list, string path, string extension, bool skipExisting = false)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(path, $"*.{extension}"))
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            if (skipExisting && list.Contains(fileName))
            {
                continue;
            }
            list.Add(fileName);
        }
    }
}