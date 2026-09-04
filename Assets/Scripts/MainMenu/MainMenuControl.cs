using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class MainMenuControl : MonoBehaviour
{
    [SerializeField] private GameObject OptionScreen;
    [SerializeField] private GameObject ExitButton;
    [SerializeField] private GameObject StartButton;
    [SerializeField] private AudioSource NormalAudioSource;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    public void StartGame()
    {
        SceneManager.LoadScene("Cutscene");
    }

    public void OpenOption()
    {
        OptionScreen.SetActive(true);
    }
    public void CloseOption()
    {
        OptionScreen.SetActive(false);
    }

    public void CloseGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
