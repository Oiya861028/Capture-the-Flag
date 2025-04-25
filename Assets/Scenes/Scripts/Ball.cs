using UnityEngine;

public class Volleyball : MonoBehaviour
{
    [Header("Ball Properties")]
    [Tooltip("Mass of the volleyball in kg")]
    public float mass = 0.28f;
    
    [Tooltip("How bouncy the ball is (0-1)")]
    [Range(0, 1)]
    public float bounciness = 0.8f;
    
    [Tooltip("Air resistance factor")]
    public float drag = 0.5f;
    
    [Tooltip("Rotation drag factor")]
    public float angularDrag = 0.05f;
    
    // Private variables
    private Rigidbody rb;
    private SphereCollider ballCollider;
    private PhysicsMaterial ballMaterial;
    
    void Awake()
    {
        // Initialize components
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
            
        ballCollider = GetComponent<SphereCollider>();
        if (ballCollider == null)
            ballCollider = gameObject.AddComponent<SphereCollider>();
        
        // Create and apply physics material - CRITICAL for bouncing
        SetupPhysicsMaterial();
        
        // Configure rigidbody
        ConfigureRigidbody();
    }
    
    void SetupPhysicsMaterial()
    {
        // Create a new physics material
        ballMaterial = new PhysicsMaterial();
        ballMaterial.bounciness = bounciness;
        ballMaterial.frictionCombine = PhysicsMaterialCombine.Average;
        ballMaterial.bounceCombine = PhysicsMaterialCombine.Maximum; // This is key for good bouncing
        
        // Apply to collider - this is essential for bouncing
        ballCollider.material = ballMaterial;
        
        // Debug verification
        Debug.Log("Physics material applied with bounciness: " + bounciness);
    }
    
    void ConfigureRigidbody()
    {
        rb.mass = mass;
        rb.linearDamping = drag;
        rb.angularDamping = angularDrag;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.None; // Make sure no constraints are limiting movement
    }
    
    // Method to be called by agents/players when hitting the ball
    public void Hit(Vector3 direction, float force)
    {
        rb.AddForce(direction.normalized * force, ForceMode.Impulse);
        rb.AddForce(Vector3.up * force * 0.4f, ForceMode.Impulse);
    }
    
    // For debugging - validate that physics material is applied properly
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Ball collided with: " + collision.gameObject.name);
        Debug.Log("Current bounciness: " + (ballCollider.material != null ? ballCollider.material.bounciness.ToString() : "No material"));
    }
}