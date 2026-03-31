using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CaseDefinitionSO))]
public class CaseDefinitionSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Normalize Slot Counts (5 suspects / 3 evidence)"))
        {
            CaseDefinitionSO caseDef = (CaseDefinitionSO)target;

            if (caseDef.suspects == null || caseDef.suspects.Length != 5)
            {
                SuspectProfileSO[] resized = new SuspectProfileSO[5];
                if (caseDef.suspects != null)
                {
                    for (int i = 0; i < Mathf.Min(caseDef.suspects.Length, resized.Length); i++)
                        resized[i] = caseDef.suspects[i];
                }
                caseDef.suspects = resized;
            }

            if (caseDef.evidence == null || caseDef.evidence.Length != 3)
            {
                EvidenceProfileSO[] resized = new EvidenceProfileSO[3];
                if (caseDef.evidence != null)
                {
                    for (int i = 0; i < Mathf.Min(caseDef.evidence.Length, resized.Length); i++)
                        resized[i] = caseDef.evidence[i];
                }
                caseDef.evidence = resized;
            }

            EditorUtility.SetDirty(caseDef);
            AssetDatabase.SaveAssets();
        }
    }
}

[CustomEditor(typeof(CaseLibrarySO))]
public class CaseLibrarySOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CaseLibrarySO library = (CaseLibrarySO)target;

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Create New Case Asset + Add To Library"))
        {
            string libraryPath = AssetDatabase.GetAssetPath(library);
            string folder = string.IsNullOrEmpty(libraryPath) ? "Assets" : Path.GetDirectoryName(libraryPath).Replace("\\", "/");
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/CaseDefinition_New.asset");

            CaseDefinitionSO newCase = CreateInstance<CaseDefinitionSO>();
            AssetDatabase.CreateAsset(newCase, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (library.cases == null)
                library.cases = new System.Collections.Generic.List<CaseDefinitionSO>();

            library.cases.Add(newCase);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            Selection.activeObject = newCase;
            EditorGUIUtility.PingObject(newCase);
        }

        if (GUILayout.Button("Create Case Library Asset"))
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Case Library", "CaseLibrary", "asset", "Choose save location");
            if (!string.IsNullOrEmpty(path))
            {
                CaseLibrarySO newLibrary = CreateInstance<CaseLibrarySO>();
                AssetDatabase.CreateAsset(newLibrary, path);
                AssetDatabase.SaveAssets();
                Selection.activeObject = newLibrary;
                EditorGUIUtility.PingObject(newLibrary);
            }
        }
    }
}
