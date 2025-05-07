
using UnityEngine;
using static CaptureTheFlagAgent;

public class Base : MonoBehaviour
{
    public Team team;
    
    void Start()
    {
        // Make sure this object has the "Base" tag
        gameObject.tag = "Base";
        
        // Make sure it has a collider set to trigger
        Collider collider = GetComponent<Collider>();
        if (collider == null)
        {
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(5, 1, 5); // Adjust size as needed
        }
        else if (!collider.isTrigger)
        {
            collider.isTrigger = true;
        }
    }
}