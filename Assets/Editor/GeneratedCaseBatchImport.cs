using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class GeneratedCaseBatchImport
{
    private const string ImportRootEnvVar = "THE_LINEUP_IMPORT_ROOT";
    private const string DefaultImportRoot = "Assets/GeneratedCases/to_import";

    [MenuItem("Tools/The Lineup/Import Staged Cases")]
    public static void ImportStagedCasesMenu()
    {
        ImportStagedCases();
    }

    public static void ImportStagedCases()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string configured = Environment.GetEnvironmentVariable(ImportRootEnvVar);
        string absoluteRoot = string.IsNullOrWhiteSpace(configured)
            ? Path.GetFullPath(Path.Combine(projectRoot, DefaultImportRoot))
            : Path.GetFullPath(configured);

        if (!Directory.Exists(absoluteRoot))
            throw new DirectoryNotFoundException($"Generated case import root not found: {absoluteRoot}");

        MethodInfo importMethod = typeof(GeneratedCaseImporter).GetMethod(
            "ImportFromRootFolder",
            BindingFlags.NonPublic | BindingFlags.Static
        );

        if (importMethod == null)
            throw new MissingMethodException("GeneratedCaseImporter.ImportFromRootFolder was not found.");

        Debug.Log($"Batch importing generated cases from: {absoluteRoot}");
        importMethod.Invoke(null, new object[] { absoluteRoot });
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }
}
