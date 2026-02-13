using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

public class pause : MonoBehaviour
{
    public static bool gameIsPaused = false;
    public static bool gameIsFrozen = false;

    public GameObject pauseMenu;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (gameIsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            FreezeTime();
        }
    }
    [SerializeField] private Button resumeButton = null;
    [SerializeField] private Button pauseButton = null;

    private void Awake()
    {
        resumeButton.onClick.AddListener(delegate
        {
            Resume();
        });
        pauseButton.onClick.AddListener(delegate
        {
            Pause();
        });
    }

    void Resume()
    {
        Debug.Log("Resumed");
        pauseMenu.SetActive(false);
        Time.timeScale = 1f;
        gameIsPaused = false;
    }

    void Pause()
    {
        Debug.Log("Paused");
        pauseMenu.SetActive(true);
        Time.timeScale = 0f;
        gameIsPaused = true;
        gameIsFrozen = true;
    }

    void FreezeTime()
    {
        if (gameIsFrozen && !gameIsPaused)
        {
            Time.timeScale = 1f;
            gameIsFrozen = false;
            Debug.Log("Unfrozen!");
        }
        else if (!gameIsFrozen)
        {
            Time.timeScale = 0f;
            gameIsFrozen = true;
            Debug.Log("Frozen!");
        }
    }
}
