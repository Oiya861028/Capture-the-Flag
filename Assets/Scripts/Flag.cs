using UnityEngine;
using static CaptureTheFlagAgent;

public class Flag : MonoBehaviour
{
    public Team team;
    public bool isCarried = false;
    
    private Vector3 startPosition;
    
    void Start()
    {
        // Save starting position
        startPosition = transform.position;
        
        // Set tag
        gameObject.tag = "Flag";
    }
    
    public void Reset()
    {
        // Reset the flag to its starting state
        transform.position = startPosition;
        isCarried = false;
        gameObject.SetActive(true);
    }
}