using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MainMenu : MonoBehaviour
{
    [SerializeField] string gameScene = "Game";

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        root.Q<Button>("buttonStart").clicked += () =>
        {
            SaveSystem.PendingLoad = null;
            SceneManager.LoadSceneAsync(gameScene);
        };
        var load = root.Q<Button>("buttonLoad");
        load.SetEnabled(SaveSystem.HasSave);
        load.clicked += () =>
        {
            SaveSystem.PendingLoad = SaveSystem.Read();
            if (SaveSystem.PendingLoad != null) SceneManager.LoadSceneAsync(gameScene);
        };
        root.Q<Button>("buttonQuit").clicked += Quit;
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
