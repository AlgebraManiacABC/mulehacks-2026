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
        root.Q<Button>("buttonStart").clicked += () => SceneManager.LoadSceneAsync(gameScene);
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
