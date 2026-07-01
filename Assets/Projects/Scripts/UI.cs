using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Projects.Scripts
{
    public class UI : MonoBehaviour
    {
        [SerializeField] private UIDocument document;

        private void Start()
        {
            var button = document.rootVisualElement.Q<Button>("QuitButton");
            button.clicked += () =>
            {
                Debug.Log("Quit");
#if UNITY_EDITOR
                EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            };
        }
    }
}