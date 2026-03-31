using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public static class InputSystemEventSystemFix
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        EnsureInputSystemModules();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInputSystemModules();
    }

    private static void EnsureInputSystemModules()
    {
        EventSystem[] systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
        for (int i = 0; i < systems.Length; i++)
        {
            EventSystem es = systems[i];
            if (es == null)
                continue;

            StandaloneInputModule oldModule = es.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
                Object.Destroy(oldModule);

            if (es.GetComponent<InputSystemUIInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}
