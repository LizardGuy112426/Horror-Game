using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeSceneOnTime : MonoBehaviour
{
    public float ChangeTime;
    public string SceneName;

    void Update()
    {
        ChangeTime -= Time.deltaTime;

        if (ChangeTime <= 0)
        {
            SceneManager.LoadScene(SceneName);
        }
    }
}