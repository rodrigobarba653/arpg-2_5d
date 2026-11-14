#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

public static class ValcyrDependencyAudit
{
    private const string ScenePath =
        "Assets/Game/Scenes/Valcyr_B1_Final.unity";

    [MenuItem("Tools/Valcyr/Export Dependency Audit")]
    public static void ExportAudit()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError("Valcyr scene not found.");
            return;
        }

        string[] dependencies =
            AssetDatabase.GetDependencies(ScenePath, true);

        var records = new List<(string path, long size)>();

        foreach (string path in dependencies)
        {
            if (!path.StartsWith("Assets/"))
                continue;

            long size = 0;

            if (File.Exists(path))
                size = new FileInfo(path).Length;

            records.Add((path, size));
        }

        var report = new StringBuilder();

        report.AppendLine("VALCYR COMPLETE DEPENDENCY AUDIT");
        report.AppendLine("Scene: " + ScenePath);
        report.AppendLine("Generated: " + DateTime.Now);
        report.AppendLine();
        report.AppendLine("DEPENDENCY COUNT: " + records.Count);

        long totalSize = records.Sum(r => r.size);

        report.AppendLine(
            "TOTAL SOURCE SIZE: " +
            (totalSize / 1024.0 / 1024.0).ToString("F2") +
            " MB");

        report.AppendLine();
        report.AppendLine("========== ALL DEPENDENCIES ==========");

        foreach (var record in records.OrderByDescending(r => r.size))
        {
            report.AppendLine(
                (record.size / 1024.0 / 1024.0).ToString("F2") +
                " MB | " + record.path);
        }

        report.AppendLine();
        report.AppendLine("========== GLB DEPENDENCIES ==========");

        foreach (var record in records
            .Where(r => r.path.EndsWith(
                ".glb", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.size))
        {
            report.AppendLine(
                (record.size / 1024.0 / 1024.0).ToString("F2") +
                " MB | " + record.path);
        }

        string output = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "Valcyr_Dependency_Audit.txt");

        File.WriteAllText(output, report.ToString());

        Debug.Log(
            "Valcyr dependency audit exported:\n" + output +
            "\nDependencies: " + records.Count);
    }
}
#endif
