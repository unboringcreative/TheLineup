using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class HelveticaFontTools
{
    private const string HelveticaFontPath = "Assets/Resources/Fonts/HelveticaNeueLTCom-Roman.ttf";

    [MenuItem("Tools/The Lineup/Typography/Apply Helvetica Across Project")]
    public static void ApplyHelveticaAcrossProject()
    {
        Font helvetica = AssetDatabase.LoadAssetAtPath<Font>(HelveticaFontPath);
        if (helvetica == null)
        {
            Debug.LogError($"Helvetica font not found at {HelveticaFontPath}");
            return;
        }

        int updatedTexts = 0;
        int touchedPrefabs = 0;
        int touchedScenes = 0;

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                continue;

            bool changed = false;
            Text[] texts = prefab.GetComponentsInChildren<Text>(true);
            for (int j = 0; j < texts.Length; j++)
            {
                Text t = texts[j];
                if (t == null || t.font == helvetica)
                    continue;

                t.font = helvetica;
                EditorUtility.SetDirty(t);
                updatedTexts++;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(prefab);
                touchedPrefabs++;
            }
        }

        string currentScenePath = SceneManager.GetActiveScene().path;
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
                continue;

            bool changed = false;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Text[] texts = roots[r].GetComponentsInChildren<Text>(true);
                for (int j = 0; j < texts.Length; j++)
                {
                    Text t = texts[j];
                    if (t == null || t.font == helvetica)
                        continue;

                    t.font = helvetica;
                    EditorUtility.SetDirty(t);
                    updatedTexts++;
                    changed = true;
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                touchedScenes++;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentScenePath))
            EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Helvetica apply complete. Updated {updatedTexts} Text component(s) across {touchedPrefabs} prefab(s) and {touchedScenes} scene(s).");
    }
}
