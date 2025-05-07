using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;

public class CaptureTheFlagAgent : Agent
{
    public enum Team { Red, Blue }
    
    [Header("Team Settings")]
    public Team team;
    
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float turnSpeed = 100f;
    
    [Header("References")]
    public Transform ownFlag;
    public Transform enemyFlag;
    public Transform jailPosition;
    public Transform releasePosition;
    
    [Header("Raycast Settings")]
    public float rayLength = 20f;
    public int numRays = 5;
    public float rayAngle = 120f;
    
    // State variables
    private bool hasFlag = false;
    private bool inJail = false;
    private float jailTimer = 0f;
    private const float jailTime = 10f;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private GameManager gameManager;
    private Rigidbody rb;
    
    // Visual indicator for flag
    public GameObject flagIndicator;
    
    // Setup these components properly on Awake
    protected override void Awake()
    {
        base.Awake();
        
        // Set team ID matching our team
        var behaviorParams = GetComponent<BehaviorParameters>();
        if (behaviorParams != null)
        {
            behaviorParams.TeamId = (int)team;
        }
        
        // Configure the behavior parameters for MA-POCA
        if (behaviorParams != null)
        {
            behaviorParams.BehaviorName = "CaptureTheFlag";
            behaviorParams.BrainParameters.VectorObservationSize = 10; // 10 vector observations
            behaviorParams.BrainParameters.NumStackedVectorObservations = 1;
            
            // Set up discrete actions with a new ActionSpec (2 discrete actions with 3 options each)
            behaviorParams.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(3, 3);
        }
        
        // Setup raycast sensor component
        var sensorComponent = GetComponent<RayPerceptionSensorComponent3D>();
        if (sensorComponent == null)
        {
            sensorComponent = gameObject.AddComponent<RayPerceptionSensorComponent3D>();
            sensorComponent.RayLength = rayLength;
            sensorComponent.DetectableTags = new List<string> { "Wall", "Agent", "Flag", "Base" };
            sensorComponent.RaysPerDirection = numRays / 2;
            sensorComponent.MaxRayDegrees = rayAngle / 2;
            sensorComponent.SphereCastRadius = 0.5f;
            sensorComponent.StartVerticalOffset = 0.5f;
            sensorComponent.EndVerticalOffset = 0.5f;
            sensorComponent.SensorName = $"{team}Agent_RaySensor";
        }
        
        // Make sure we have a rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.FreezePositionY | 
                            RigidbodyConstraints.FreezeRotationX | 
                            RigidbodyConstraints.FreezeRotationZ;
        }
        
        // Make sure we have a collider
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<CapsuleCollider>();
        }
        
        // Set the tag
        gameObject.tag = "Agent";
    }
    
    public override void Initialize()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        
        // Initialize position and rotation
        startPosition = transform.position;
        startRotation = transform.rotation;
        
        if (flagIndicator != null)
        {
            flagIndicator.SetActive(false);
        }
    }
    
    public override void OnEpisodeBegin()
    {
        // Reset state
        hasFlag = false;
        inJail = false;
        jailTimer = 0f;
        
        // Reset position and rotation
        transform.position = startPosition;
        transform.rotation = startRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        if (flagIndicator != null)
        {
            flagIndicator.SetActive(false);
        }
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        // Position (x,z) - 2 values
        sensor.AddObservation(transform.position.x);
        sensor.AddObservation(transform.position.z);
        
        // Forward direction - 2 values
        sensor.AddObservation(transform.forward.x);
        sensor.AddObservation(transform.forward.z);
        
        // Has flag - 1 value
        sensor.AddObservation(hasFlag ? 1 : 0);
        
        // Team - 1 value
        sensor.AddObservation((int)team);
        
        // Own flag position - 2 values
        sensor.AddObservation(ownFlag.position.x);
        sensor.AddObservation(ownFlag.position.z);
        
        // Enemy flag position - 2 values
        sensor.AddObservation(enemyFlag.position.x);
        sensor.AddObservation(enemyFlag.position.z);
    }
    
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (inJail)
        {
            // Update jail timer
            jailTimer -= Time.fixedDeltaTime;
            if (jailTimer <= 0)
            {
                ReleaseFromJail();
            }
            return;
        }
        
        // Get discrete actions (0 = backward, 1 = still, 2 = forward)
        // and (0 = left, 1 = straight, 2 = right)
        var discreteActions = actionBuffers.DiscreteActions;
        int moveAction = discreteActions[0];
        int turnAction = discreteActions[1];
        
        // Convert to -1, 0, 1
        float moveValue = moveAction - 1;
        float turnValue = turnAction - 1;
        
        // Apply movement
        transform.Rotate(0, turnValue * turnSpeed * Time.fixedDeltaTime, 0);
        transform.Translate(0, 0, moveValue * moveSpeed * Time.fixedDeltaTime);
        
        // Small negative reward for standing still
        if (moveAction == 1 && turnAction == 1)
        {
            AddReward(-0.01f);
        }
    }
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // For manual control during testing
        var discreteActions = actionsOut.DiscreteActions;
        
        // Movement forward/backward
        if (Input.GetKey(KeyCode.W))
            discreteActions[0] = 2; // Forward
        else if (Input.GetKey(KeyCode.S))
            discreteActions[0] = 0; // Backward
        else
            discreteActions[0] = 1; // No movement
        
        // Turning left/right
        if (Input.GetKey(KeyCode.A))
            discreteActions[1] = 0; // Left
        else if (Input.GetKey(KeyCode.D))
            discreteActions[1] = 2; // Right
        else
            discreteActions[1] = 1; // No turning
    }
    
    void OnCollisionEnter(Collision collision)
    {
        // If in jail, do nothing
        if (inJail) return;
        
        // Check for flag pickup
        if (collision.gameObject.CompareTag("Flag"))
        {
            Flag flag = collision.gameObject.GetComponent<Flag>();
            if (flag != null && flag.team != team && !hasFlag && !flag.isCarried)
            {
                PickupFlag(flag);
            }
        }
        
        // Check for enemy tag
        if (collision.gameObject.CompareTag("Agent"))
        {
            CaptureTheFlagAgent otherAgent = collision.gameObject.GetComponent<CaptureTheFlagAgent>();
            if (otherAgent != null && otherAgent.team != team)
            {
                // If we're on the enemy side, we get sent to jail
                if (IsOnEnemySide())
                {
                    SendToJail();
                }
                // If they're on our side and have our flag, they get sent to jail
                else if (otherAgent.hasFlag && otherAgent.IsOnEnemySide())
                {
                    otherAgent.SendToJail();
                }
            }
        }
        
        // Check for scoring
        if (hasFlag && collision.gameObject.CompareTag("Base"))
        {
            Base baseObj = collision.gameObject.GetComponent<Base>();
            if (baseObj != null && baseObj.team == team)
            {
                ScoreFlag();
            }
        }
    }
    
    void PickupFlag(Flag flag)
    {
        hasFlag = true;
        flag.isCarried = true;
        flag.gameObject.SetActive(false);
        
        if (flagIndicator != null)
        {
            flagIndicator.SetActive(true);
        }
        
        if (gameManager != null)
        {
            gameManager.FlagPickedUp(team == Team.Red ? Team.Blue : Team.Red);
        }
    }
    
    void ScoreFlag()
    {
        if (!hasFlag) return;
        
        hasFlag = false;
        
        if (flagIndicator != null)
        {
            flagIndicator.SetActive(false);
        }
        
        if (gameManager != null)
        {
            gameManager.ScoreFlag(team);
        }
        
        // Reward for scoring a flag
        AddReward(1.0f);
    }
    
    public void SendToJail()
    {
        // Return the flag if we have it
        if (hasFlag)
        {
            hasFlag = false;
            
            if (gameManager != null)
            {
                gameManager.ReturnFlag(team == Team.Red ? Team.Blue : Team.Red);
            }
            
            if (flagIndicator != null)
            {
                flagIndicator.SetActive(false);
            }
        }
        
        // Go to jail
        inJail = true;
        jailTimer = jailTime;
        transform.position = jailPosition.position;
        rb.linearVelocity = Vector3.zero;
        
        // Penalty for getting caught
        AddReward(-0.2f);
    }
    
    public void ReleaseFromJail()
    {
        inJail = false;
        transform.position = releasePosition.position;
    }
    
    public bool IsOnOwnSide()
    {
        // Assuming field is divided at x=0
        if (team == Team.Red)
            return transform.position.x < 0;
        else
            return transform.position.x > 0;
    }
    
    public bool IsOnEnemySide()
    {
        return !IsOnOwnSide();
    }
    
    public bool HasFlag()
    {
        return hasFlag;
    }
    
    public bool InJail()
    {
        return inJail;
    }
}