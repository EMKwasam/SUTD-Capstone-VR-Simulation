using UnityEngine;

public class EndZone : MonoBehaviour
{
    public GameManager gameManager;
    
    void Start()
    {
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
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
        if (!other.CompareTag("Lesion"))
        {
            return;
        }

        GameObject lesionObject = GetLesionRootObject(other);
        if (lesionObject == null)
        {
            return;
        }

        Debug.Log("Lesion removed!");

        // Award the point
        if (gameManager != null)
        {
            gameManager.ScorePoint();
        }

        // Destroy the lesion root object
        Destroy(lesionObject);
    }

    private GameObject GetLesionRootObject(Collider other)
    {
        if (other.attachedRigidbody != null)
        {
            return other.attachedRigidbody.gameObject;
        }

        return other.gameObject;
    }
}
