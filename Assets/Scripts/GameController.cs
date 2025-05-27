using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using static CaptureTheFlagAgent;

public class GameManager : MonoBehaviour
{
    [Header("Game Settings")]
    public int scoreToWin = 3;
    public int maxSteps = 10000;
    
    [Header("Team Setup")]
    public Transform redTeamParent;
    public Transform blueTeamParent;
    
    // IMPORTANT: Use GameObject references instead of Transform
    public GameObject redFlagObject;
    public GameObject blueFlagObject;
    
    public Transform redJail;
    public Transform blueJail;
    public Transform redReleasePosition;
    public Transform blueReleasePosition;
    
    [Header("Base Areas")]
    public Transform redBase;
    public Transform blueBase;
    
    // Team Groups for MA-POCA
    private SimpleMultiAgentGroup redTeamGroup;
    private SimpleMultiAgentGroup blueTeamGroup;
    
    // Game state
    private int redScore = 0;
    private int blueScore = 0;
    private int currentStep = 0;
    private bool gameActive = true;
    
    // Flag states
    private Vector3 redFlagStartPos;
    private Vector3 blueFlagStartPos;
    
    // Cached references to agents
    private List<CaptureTheFlagAgent> redAgents = new List<CaptureTheFlagAgent>();
    private List<CaptureTheFlagAgent> blueAgents = new List<CaptureTheFlagAgent>();
    
    void Awake()
    {
        // Make sure flags have the Flag component
        if (redFlagObject != null) SetupFlag(redFlagObject.transform, Team.Red);
        if (blueFlagObject != null) SetupFlag(blueFlagObject.transform, Team.Blue);
        
        // Make sure base areas have the Base component
        SetupBase(redBase, Team.Red);
        SetupBase(blueBase, Team.Blue);
        
        // Initialize MA-POCA team groups
        redTeamGroup = new SimpleMultiAgentGroup();
        blueTeamGroup = new SimpleMultiAgentGroup();
        
        // Find and setup agents
        FindAndSetupAgents();
        
        // Save flag starting positions
        if (redFlagObject != null) redFlagStartPos = redFlagObject.transform.position;
        if (blueFlagObject != null) blueFlagStartPos = blueFlagObject.transform.position;
        
        // Make sure we have the correct tags in the project
        CheckAndCreateTags();
    }
    
    void CheckAndCreateTags()
    {
        // This won't actually create tags at runtime, but it's a reminder for you
        Debug.Log("Make sure you have these tags in your project: Agent, Flag, Base, Obstacle");
    }
    
    void Start()
    {
        ResetGame();
    }
    
    void FixedUpdate()
    {
        if (!gameActive) return;
        
        currentStep++;
        if (currentStep >= maxSteps)
        {
            // End episode due to max steps
            EndEpisode();
        }
    }
    
    private void FindAndSetupAgents()
    {
        // Find Red team agents
        if (redTeamParent != null)
        {
            foreach (Transform child in redTeamParent)
            {
                CaptureTheFlagAgent agent = child.GetComponent<CaptureTheFlagAgent>();
                if (agent != null)
                {
                    agent.team = Team.Red;
                    agent.ownFlag = redFlagObject?.transform;
                    agent.enemyFlag = blueFlagObject?.transform;
                    agent.jailPosition = redJail;
                    agent.releasePosition = redReleasePosition;
                    agent.ownBase = redBase;
                    
                    redAgents.Add(agent);
                    redTeamGroup.RegisterAgent(agent);
                }
            }
        }
        
        // Find Blue team agents
        if (blueTeamParent != null)
        {
            foreach (Transform child in blueTeamParent)
            {
                CaptureTheFlagAgent agent = child.GetComponent<CaptureTheFlagAgent>();
                if (agent != null)
                {
                    agent.team = Team.Blue;
                    agent.ownFlag = blueFlagObject?.transform;
                    agent.enemyFlag = redFlagObject?.transform;
                    agent.jailPosition = blueJail;
                    agent.releasePosition = blueReleasePosition;
                    agent.ownBase = blueBase;
                    
                    blueAgents.Add(agent);
                    blueTeamGroup.RegisterAgent(agent);
                }
            }
        }
    }
    
    private void SetupFlag(Transform flagTransform, Team team)
    {
        if (flagTransform == null) return;
        
        Flag flag = flagTransform.GetComponent<Flag>();
        if (flag == null)
        {
            flag = flagTransform.gameObject.AddComponent<Flag>();
        }
        
        flag.team = team;
        flagTransform.gameObject.tag = "Flag";
    }
    
    private void SetupBase(Transform baseTransform, Team team)
    {
        if (baseTransform == null) return;
        
        Base baseComponent = baseTransform.GetComponent<Base>();
        if (baseComponent == null)
        {
            baseComponent = baseTransform.gameObject.AddComponent<Base>();
        }
        
        baseComponent.team = team;
        baseTransform.gameObject.tag = "Base";
        
        // Make sure it has a trigger collider
        Collider collider = baseTransform.GetComponent<Collider>();
        if (collider == null)
        {
            BoxCollider boxCollider = baseTransform.gameObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(5, 1, 5); // Adjust size as needed
        }
        else
        {
            collider.isTrigger = true;
        }
    }
    
    public void ResetGame()
    {
        // Reset scores and state
        redScore = 0;
        blueScore = 0;
        currentStep = 0;
        gameActive = true;
        
        // Reset flags
        ResetFlags();
        
        // Reset all agents
        foreach (var agent in redAgents)
        {
            agent.OnEpisodeBegin();
        }
        
        foreach (var agent in blueAgents)
        {
            agent.OnEpisodeBegin();
        }
    }
    
    private void ResetFlags()
    {
        // Reset red flag
        if (redFlagObject != null)
        {
            redFlagObject.transform.position = redFlagStartPos;
            redFlagObject.transform.rotation = Quaternion.identity;
            redFlagObject.SetActive(true);
            
            Flag redFlagComponent = redFlagObject.GetComponent<Flag>();
            if (redFlagComponent != null) redFlagComponent.isCarried = false;
            
            Debug.Log($"Red flag reset - Active: {redFlagObject.activeSelf}, Position: {redFlagObject.transform.position}");
        }
        
        // Reset blue flag
        if (blueFlagObject != null)
        {
            blueFlagObject.transform.position = blueFlagStartPos;
            blueFlagObject.transform.rotation = Quaternion.identity;
            blueFlagObject.SetActive(true);
            
            Flag blueFlagComponent = blueFlagObject.GetComponent<Flag>();
            if (blueFlagComponent != null) blueFlagComponent.isCarried = false;
            
            Debug.Log($"Blue flag reset - Active: {blueFlagObject.activeSelf}, Position: {blueFlagObject.transform.position}");
        }
    }
    
    public void FlagPickedUp(Team flagTeam)
    {
        Debug.Log($"{flagTeam} flag was picked up!");
        
        if (flagTeam == Team.Red)
        {
            // Penalty for team whose flag was stolen
            foreach (var agent in redAgents)
            {
                agent.AddReward(-0.5f);
            }
        }
        else
        {
            // Penalty for team whose flag was stolen
            foreach (var agent in blueAgents)
            {
                agent.AddReward(-0.5f);
            }
        }
    }
    
    public void ScoreFlag(Team scoringTeam)
    {
        Debug.Log($"=== SCORE FLAG CALLED - {scoringTeam} team scores! ===");
        
        if (scoringTeam == Team.Red)
        {
            redScore++;
            Debug.Log($"RED TEAM SCORES! Score: {redScore}/{scoreToWin}");
            
            // Return blue flag
            if (blueFlagObject != null)
            {
                Debug.Log($"Attempting to return BLUE flag...");
                Debug.Log($"  Before: Active={blueFlagObject.activeSelf}, Position={blueFlagObject.transform.position}");
                
                blueFlagObject.transform.position = blueFlagStartPos;
                blueFlagObject.transform.rotation = Quaternion.identity;
                blueFlagObject.SetActive(true); // This is the key line!
                
                Flag blueFlagComponent = blueFlagObject.GetComponent<Flag>();
                if (blueFlagComponent != null) 
                {
                    blueFlagComponent.isCarried = false;
                }
                
                Debug.Log($"  After: Active={blueFlagObject.activeSelf}, Position={blueFlagObject.transform.position}");
                
                // Double-check it worked
                if (!blueFlagObject.activeSelf)
                {
                    Debug.LogError("BLUE FLAG FAILED TO ACTIVATE!");
                    // Force it active
                    blueFlagObject.SetActive(true);
                }
            }
            else
            {
                Debug.LogError("BLUE FLAG OBJECT IS NULL!");
            }
            
            // Check for win
            if (redScore >= scoreToWin)
            {
                Debug.Log("RED TEAM WINS!");
                redTeamGroup.AddGroupReward(1.0f);
                blueTeamGroup.AddGroupReward(-1.0f);
                EndEpisode();
            }
        }
        else // Blue team scores
        {
            blueScore++;
            Debug.Log($"BLUE TEAM SCORES! Score: {blueScore}/{scoreToWin}");
            
            // Return red flag
            if (redFlagObject != null)
            {
                Debug.Log($"Attempting to return RED flag...");
                Debug.Log($"  Before: Active={redFlagObject.activeSelf}, Position={redFlagObject.transform.position}");
                
                redFlagObject.transform.position = redFlagStartPos;
                redFlagObject.transform.rotation = Quaternion.identity;
                redFlagObject.SetActive(true); // This is the key line!
                
                Flag redFlagComponent = redFlagObject.GetComponent<Flag>();
                if (redFlagComponent != null) 
                {
                    redFlagComponent.isCarried = false;
                }
                
                Debug.Log($"  After: Active={redFlagObject.activeSelf}, Position={redFlagObject.transform.position}");
                
                // Double-check it worked
                if (!redFlagObject.activeSelf)
                {
                    Debug.LogError("RED FLAG FAILED TO ACTIVATE!");
                    // Force it active
                    redFlagObject.SetActive(true);
                }
            }
            else
            {
                Debug.LogError("RED FLAG OBJECT IS NULL!");
            }
            
            // Check for win
            if (blueScore >= scoreToWin)
            {
                Debug.Log("BLUE TEAM WINS!");
                blueTeamGroup.AddGroupReward(1.0f);
                redTeamGroup.AddGroupReward(-1.0f);
                EndEpisode();
            }
        }
    }
    
    public void ReturnFlag(Team flagTeam)
    {
        Debug.Log($"Returning {flagTeam} flag to base");
        
        if (flagTeam == Team.Red && redFlagObject != null)
        {
            redFlagObject.transform.position = redFlagStartPos;
            redFlagObject.SetActive(true);
            
            Flag redFlagComponent = redFlagObject.GetComponent<Flag>();
            if (redFlagComponent != null) redFlagComponent.isCarried = false;
        }
        else if (flagTeam == Team.Blue && blueFlagObject != null)
        {
            blueFlagObject.transform.position = blueFlagStartPos;
            blueFlagObject.SetActive(true);
            
            Flag blueFlagComponent = blueFlagObject.GetComponent<Flag>();
            if (blueFlagComponent != null) blueFlagComponent.isCarried = false;
        }
    }
    
    private void EndEpisode()
    {
        Debug.Log($"=== EPISODE ENDING - Final Score: Red {redScore} - Blue {blueScore} ===");
        gameActive = false;
        
        // End episode for both teams
        redTeamGroup.EndGroupEpisode();
        blueTeamGroup.EndGroupEpisode();
        
        // Reset the game
        ResetGame();
    }
    
    // Debug methods
    [ContextMenu("Debug Flag States")]
    public void ManualDebugFlagStates()
    {
        Debug.Log("=== FLAG STATES ===");
        if (redFlagObject != null)
        {
            Flag redFlagComp = redFlagObject.GetComponent<Flag>();
            Debug.Log($"RED FLAG: Active={redFlagObject.activeSelf}, Position={redFlagObject.transform.position}, isCarried={redFlagComp?.isCarried}");
        }
        else
        {
            Debug.Log("RED FLAG OBJECT IS NULL!");
        }
        
        if (blueFlagObject != null)
        {
            Flag blueFlagComp = blueFlagObject.GetComponent<Flag>();
            Debug.Log($"BLUE FLAG: Active={blueFlagObject.activeSelf}, Position={blueFlagObject.transform.position}, isCarried={blueFlagComp?.isCarried}");
        }
        else
        {
            Debug.Log("BLUE FLAG OBJECT IS NULL!");
        }
    }
    
    [ContextMenu("Force Activate Flags")]
    public void ForceActivateFlags()
    {
        if (redFlagObject != null)
        {
            redFlagObject.SetActive(true);
            Debug.Log($"Red flag forced active: {redFlagObject.activeSelf}");
        }
        
        if (blueFlagObject != null)
        {
            blueFlagObject.SetActive(true);
            Debug.Log($"Blue flag forced active: {blueFlagObject.activeSelf}");
        }
    }
}