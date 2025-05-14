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
    public Transform ownBase;  // Reference to our team's base
    
    [Header("Raycast Settings")]
    public float rayLength = 20f;
    public int numRays = 5;
    public float rayAngle = 120f;
    
    [Header("Reward Settings")]
    public float getFlagReward = 0.5f;
    public float returnFlagReward = 1.0f;
    public float tagOpponentReward = 0.1f;
    public float tagFlagCarrierReward = 0.3f;
    public float jailedPenalty = -0.2f;
    public float idlePenalty = -0.01f;
    public float wallProximityPenalty = -0.01f;
    
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
            sensorComponent.DetectableTags = new List<string> { "Obstacle", "Agent", "Flag", "Base" };
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
            CapsuleCollider capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
            capsuleCollider.height = 2f;
            capsuleCollider.radius = 0.5f;
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
        
        // Get reference to own base if not set
        if (ownBase == null && gameManager != null)
        {
            ownBase = team == Team.Red ? gameManager.redBase : gameManager.blueBase;
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
        
        // Check if we would hit a wall before moving
        if (moveValue != 0 && !WouldHitWall(transform.forward * moveValue, moveSpeed * Time.fixedDeltaTime * 1.1f))
        {
            // Apply movement if we won't hit a wall
            transform.Translate(0, 0, moveValue * moveSpeed * Time.fixedDeltaTime);
        }
        
        // Apply rotation
        transform.Rotate(0, turnValue * turnSpeed * Time.fixedDeltaTime, 0);
        
        // Small negative reward for standing still
        if (moveAction == 1 && turnAction == 1)
        {
            AddReward(idlePenalty);
        }
        
        // Apply positional rewards
        PositionalRewards();
    }
    
    bool WouldHitWall(Vector3 moveDirection, float distance)
    {
        // Cast a ray to check if we would hit a wall
        if (Physics.Raycast(transform.position, moveDirection, out RaycastHit hit, distance))
        {
            if (hit.collider.CompareTag("Obstacle"))
            {
                return true; // Would hit a wall
            }
        }
        return false; // Safe to move
    }
    
    private void PositionalRewards()
    {
        // Skip if in jail
        if (inJail) return;
        
        // Small wall avoidance reward
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 1.0f))
        {
            if (hit.collider.CompareTag("Obstacle"))
            {
                // Penalty for being too close to walls
                AddReward(wallProximityPenalty);
            }
        }
        
        // Reward for moving toward enemy flag when on offense
        if (!hasFlag && enemyFlag.gameObject.activeSelf && IsOnEnemySide())
        {
            // Calculate direction to enemy flag
            Vector3 dirToFlag = (enemyFlag.position - transform.position).normalized;
            float movingTowardFlag = Vector3.Dot(transform.forward, dirToFlag);
            
            // Reward if moving toward flag
            if (movingTowardFlag > 0.5f)
            {
                AddReward(0.005f);
            }
            
            // Extra reward for getting closer to the flag
            float distToFlag = Vector3.Distance(transform.position, enemyFlag.position);
            if (distToFlag < 5f)
            {
                AddReward(0.01f * (1f - distToFlag/5f));
            }
        }
        
        // Reward for moving back to own base when carrying flag
        if (hasFlag)
        {
            // Calculate direction to own base
            Vector3 dirToBase = (ownBase.position - transform.position).normalized;
            float movingTowardBase = Vector3.Dot(transform.forward, dirToBase);
            
            // Reward if moving toward own base
            if (movingTowardBase > 0.5f)
            {
                AddReward(0.01f);
            }
            
            // Extra reward for getting closer to base
            float distToBase = Vector3.Distance(transform.position, ownBase.position);
            if (distToBase < 10f)
            {
                AddReward(0.02f * (1f - distToBase/10f));
            }
        }
        
        // Defensive positioning reward
        if (!hasFlag && !IsOnEnemySide())
        {
            // If enemy has our flag, reward for chasing the flag carrier
            CaptureTheFlagAgent flagCarrier = FindFlagCarrier(team == Team.Red ? Team.Blue : Team.Red);
            if (flagCarrier != null)
            {
                Vector3 dirToCarrier = (flagCarrier.transform.position - transform.position).normalized;
                float movingTowardCarrier = Vector3.Dot(transform.forward, dirToCarrier);
                
                if (movingTowardCarrier > 0.5f)
                {
                    AddReward(0.008f);
                }
                
                // Extra reward for getting closer to flag carrier
                float distToCarrier = Vector3.Distance(transform.position, flagCarrier.transform.position);
                if (distToCarrier < 5f)
                {
                    AddReward(0.015f * (1f - distToCarrier/5f));
                }
            }
            // Otherwise reward for guarding own flag
            else
            {
                float distToOwnFlag = Vector3.Distance(transform.position, ownFlag.position);
                if (distToOwnFlag < 10f)
                {
                    // Higher reward the closer you are to flag, up to a point
                    AddReward(0.003f * Mathf.Clamp(1f - (distToOwnFlag / 10f), 0.2f, 1f));
                }
            }
        }
    }
    
    // Helper method to find the agent carrying the flag
    private CaptureTheFlagAgent FindFlagCarrier(Team targetTeam)
    {
        CaptureTheFlagAgent[] agents = FindObjectsOfType<CaptureTheFlagAgent>();
        foreach (var agent in agents)
        {
            if (agent.team == targetTeam && agent.hasFlag)
            {
                return agent;
            }
        }
        return null;
    }
    
    private void RewardForTagging(CaptureTheFlagAgent taggedAgent)
    {
        // Base reward for tagging
        AddReward(tagOpponentReward);
        
        // Extra reward if they were carrying our flag
        if (taggedAgent.hasFlag)
        {
            AddReward(tagFlagCarrierReward);
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
    
    // Direct movement for testing - use only when behavior type is Heuristic
    void Update()
    {
        // Only use direct controls if in Heuristic mode
        var behaviorParams = GetComponent<BehaviorParameters>();
        if (behaviorParams != null && behaviorParams.BehaviorType == BehaviorType.HeuristicOnly)
        {
            // Request a decision manually to make sure the agent is updated
            RequestDecision();
        }
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
                    // Reward for successful tagging
                    RewardForTagging(otherAgent);
                }
            }
        }
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(wallProximityPenalty);
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Check for scoring
        if (hasFlag && other.CompareTag("Base"))
        {
            Base baseObj = other.GetComponent<Base>();
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
        
        // BIG reward for getting the flag
        AddReward(getFlagReward);
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
        AddReward(returnFlagReward);
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
        AddReward(jailedPenalty);
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
            return transform.position.x >0;
        else
            return transform.position.x <0;
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