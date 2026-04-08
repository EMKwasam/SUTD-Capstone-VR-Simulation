using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    private int lesionsRemaining = 0;
    private int totalLesions = 0;
    [SerializeField] private bool enableKeyboardReset = true;
    [SerializeField] private Key resetKey = Key.R;
    
    public TextMeshProUGUI lesionCountText;
    
    private static GameManager instance;
    
    void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        if (lesionCountText == null)
        {
            Debug.LogWarning("Lesion Count Text UI not assigned to GameManager!");
        }
        
        // Count initial lesions in scene
        CountLesions();
        totalLesions = lesionsRemaining;
        UpdateDisplay();
    }
    
    void Update()
    {
        // Update lesion count every frame to catch any changes
        CountLesions();

        Keyboard keyboard = Keyboard.current;
        if (enableKeyboardReset && keyboard != null && keyboard[resetKey].wasPressedThisFrame)
        {
            ResetGame();
        }
    }
    
    public void ScorePoint()
    {
        lesionsRemaining--;
        Debug.Log("Lesions Remaining: " + lesionsRemaining);
        UpdateDisplay();
    }
    
    private void CountLesions()
    {
        GameObject[] lesions = GameObject.FindGameObjectsWithTag("Lesion");
        lesionsRemaining = lesions.Length;
    }
    
    private void UpdateDisplay()
    {
        if (lesionCountText != null)
        {
            lesionCountText.text = "Lesions Left: " + lesionsRemaining;
        }
    }
    
    public int GetLesionsRemaining()
    {
        return lesionsRemaining;
    }
    
    public int GetTotalLesions()
    {
        return totalLesions;
    }

    public void ResetGame()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }
}
