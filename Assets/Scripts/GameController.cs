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
    public Transform redFlag;
    public Transform blueFlag;
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
        SetupFlag(redFlag, Team.Red);
        SetupFlag(blueFlag, Team.Blue);
        
        // Make sure base areas have the Base component
        SetupBase(redBase, Team.Red);
        SetupBase(blueBase, Team.Blue);
        
        // Initialize MA-POCA team groups
        redTeamGroup = new SimpleMultiAgentGroup();
        blueTeamGroup = new SimpleMultiAgentGroup();
        
        // Find and setup agents
        FindAndSetupAgents();
        
        // Save flag starting positions
        if (redFlag != null) redFlagStartPos = redFlag.position;
        if (blueFlag != null) blueFlagStartPos = blueFlag.position;
        
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
                    agent.ownFlag = redFlag;
                    agent.enemyFlag = blueFlag;
                    agent.jailPosition = redJail;
                    agent.releasePosition = redReleasePosition;
                    
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
                    agent.ownFlag = blueFlag;
                    agent.enemyFlag = redFlag;
                    agent.jailPosition = blueJail;
                    agent.releasePosition = blueReleasePosition;
                    
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
        // Reset flag positions
        if (redFlag != null)
        {
            redFlag.position = redFlagStartPos;
            redFlag.gameObject.SetActive(true);
            
            Flag redFlagComponent = redFlag.GetComponent<Flag>();
            if (redFlagComponent != null) redFlagComponent.isCarried = false;
        }
        
        if (blueFlag != null)
        {
            blueFlag.position = blueFlagStartPos;
            blueFlag.gameObject.SetActive(true);
            
            Flag blueFlagComponent = blueFlag.GetComponent<Flag>();
            if (blueFlagComponent != null) blueFlagComponent.isCarried = false;
        }
    }
    
    public void FlagPickedUp(Team flagTeam)
    {
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
        if (scoringTeam == Team.Red)
        {
            redScore++;
            
            // Return blue flag
            if (blueFlag != null)
            {
                blueFlag.position = blueFlagStartPos;
                blueFlag.gameObject.SetActive(true);
                
                Flag blueFlagComponent = blueFlag.GetComponent<Flag>();
                if (blueFlagComponent != null) blueFlagComponent.isCarried = false;
            }
            
            // Check for win
            if (redScore >= scoreToWin)
            {
                // Red team wins
                redTeamGroup.AddGroupReward(1.0f);
                blueTeamGroup.AddGroupReward(-1.0f);
                EndEpisode();
            }
        }
        else
        {
            blueScore++;
            
            // Return red flag
            if (redFlag != null)
            {
                redFlag.position = redFlagStartPos;
                redFlag.gameObject.SetActive(true);
                
                Flag redFlagComponent = redFlag.GetComponent<Flag>();
                if (redFlagComponent != null) redFlagComponent.isCarried = false;
            }
            
            // Check for win
            if (blueScore >= scoreToWin)
            {
                // Blue team wins
                blueTeamGroup.AddGroupReward(1.0f);
                redTeamGroup.AddGroupReward(-1.0f);
                EndEpisode();
            }
        }
    }
    
    public void ReturnFlag(Team flagTeam)
    {
        if (flagTeam == Team.Red)
        {
            if (redFlag != null)
            {
                redFlag.position = redFlagStartPos;
                redFlag.gameObject.SetActive(true);
                
                Flag redFlagComponent = redFlag.GetComponent<Flag>();
                if (redFlagComponent != null) redFlagComponent.isCarried = false;
            }
        }
        else
        {
            if (blueFlag != null)
            {
                blueFlag.position = blueFlagStartPos;
                blueFlag.gameObject.SetActive(true);
                
                Flag blueFlagComponent = blueFlag.GetComponent<Flag>();
                if (blueFlagComponent != null) blueFlagComponent.isCarried = false;
            }
        }
    }
    
    private void EndEpisode()
    {
        gameActive = false;
        
        // End episode for both teams
        redTeamGroup.EndGroupEpisode();
        blueTeamGroup.EndGroupEpisode();
        
        // Reset the game
        ResetGame();
    }
}