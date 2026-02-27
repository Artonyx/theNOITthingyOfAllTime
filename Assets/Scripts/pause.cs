using UnityEngine;
using UnityEngine.UI;

public class pause : MonoBehaviour
{
    public static bool gameIsPaused      = false;
    public static bool gameIsFrozen      = false;
    public static bool cannotChangeState = false;

    public GameObject pauseMenu;
    public GameObject tutorialScreen;
    public GameObject winScreen;
    public GameObject lossScreen;

    [SerializeField] private Button resumeButton = null;
    [SerializeField] private Button pauseButton  = null;

    // Track whether a special screen was open last frame
    private bool _wasBlocked = false;

    // -------------------------------------------------------------------------

    private void Awake()
    {
        resumeButton?.onClick.AddListener(Resume);
        pauseButton?.onClick.AddListener(Pause);
    }

    private void Update()
    {
        bool blocked = tutorialScreen.activeSelf
                    || winScreen.activeSelf
                    || lossScreen.activeSelf;

        // Only change timeScale when the blocked state actually CHANGES,
        // not every single frame — this is what was breaking freeze/pause.
        if (blocked && !_wasBlocked)
        {
            Time.timeScale   = 0f;
            cannotChangeState = true;
        }
        else if (!blocked && _wasBlocked)
        {
            Time.timeScale    = 1f;
            cannotChangeState = false;
            gameIsPaused      = false;
            gameIsFrozen      = false;
        }

        _wasBlocked = blocked;

        if (cannotChangeState) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (gameIsPaused) Resume();
            else              Pause();
        }

        if (Input.GetKeyDown(KeyCode.Space))
            FreezeTime();
    }

    // -------------------------------------------------------------------------

    void Resume()
    {
        pauseMenu.SetActive(false);
        Time.timeScale = gameIsFrozen ? 0f : 1f; // respect freeze if active
        gameIsPaused   = false;
        Debug.Log("Resumed");
    }

    void Pause()
    {
        pauseMenu.SetActive(true);
        Time.timeScale = 0f;
        gameIsPaused   = true;
        Debug.Log("Paused");
    }

    void FreezeTime()
    {
        if (gameIsPaused) return; // can't freeze while paused

        if (gameIsFrozen)
        {
            Time.timeScale = 1f;
            gameIsFrozen   = false;
            Debug.Log("Unfrozen!");
        }
        else
        {
            Time.timeScale = 0f;
            gameIsFrozen   = true;
            Debug.Log("Frozen!");
        }
    }
}