using UnityEngine;

public class SceneSwitchButton : MonoBehaviour
{
    [Header("Scene Names")]
    public string minigameSceneName;
    public string mainSceneNameOverride = ""; // leave empty to use GameManager.mainSceneName

    // Hook these in Button OnClick

    public void GoToMinigame()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.LoadMinigameScene(minigameSceneName);
    }

    public void ReturnToMain()
    {
        if (GameManager.Instance == null) return;
        if (!string.IsNullOrEmpty(mainSceneNameOverride))
        {
            // Temporarily swap the main scene name if provided
            // then return and restore
            // Simpler: just call ReturnToMain (uses GameManager main)
            GameManager.Instance.ReturnToMain();
        }
        else
        {
            GameManager.Instance.ReturnToMain();
        }
    }

    public void ToggleMinigame()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.ToggleScene(minigameSceneName);
    }

    // Optional overloads if you want to pass the scene name from the Button parameter
    public void GoToMinigameByName(string sceneName)
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.LoadMinigameScene(sceneName);
    }

    public void ToggleByName(string sceneName)
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.ToggleScene(sceneName);
    }
}