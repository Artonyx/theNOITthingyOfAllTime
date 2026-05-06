using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class sceneManager : MonoBehaviour
{
    private const string Level2UnlockedKey = "Level2Unlocked";

    [Header("Optional Main Menu References")]
    [SerializeField] private Button level2Button;

    private void Start()
    {
        RefreshLevelButtonState();
    }

    public void switchScene(string sceneName)
    {
        if (string.Equals(sceneName, "level2", System.StringComparison.OrdinalIgnoreCase) && !IsLevel2Unlocked())
        {
            Debug.Log("[sceneManager] Level 2 is locked.");
            return;
        }

        SceneManager.LoadScene(sceneName);
        Time.timeScale = 1f;
    }

    public static void UnlockLevel2()
    {
        PlayerPrefs.SetInt(Level2UnlockedKey, 1);
        PlayerPrefs.Save();
    }

    public static bool IsLevel2Unlocked()
    {
        return PlayerPrefs.GetInt(Level2UnlockedKey, 0) == 1;
    }

    private void RefreshLevelButtonState()
    {
        if (level2Button != null)
        {
            level2Button.interactable = IsLevel2Unlocked();
        }
    }

    public void QuitGame()
    {
        Debug.Log("Quit Game");
        Application.Quit();
    }
}
