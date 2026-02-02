using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class EndZone : MonoBehaviour
{
    public GameManager gameManager;
    
    void Start()
    {
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
            if (gameManager == null)
            {
                Debug.LogError("GameManager not found in scene!");
            }
        }
        
        // Ensure this collider is set as trigger
        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
        else
        {
            Debug.LogError("EndZone requires a Collider component!");
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Lesion"))
        {
            XRGrabInteractable grab = other.GetComponent<XRGrabInteractable>();
            
            // Check if ball is being held (grabbed)
            if (grab != null && grab.isSelected)
            {
                Debug.Log("Lesion removed!");
                
                // Award the point
                if (gameManager != null)
                {
                    gameManager.ScorePoint();
                }
                
                // Destroy the ball
                Destroy(other.gameObject);
            }
        }
    }
}
