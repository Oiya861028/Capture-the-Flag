using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using static CaptureTheFlagAgent;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("Game Settings")]
    public int scoreToWin = 3;
    public int maxSteps = 10000;
    
    [Header("UI")]
    public TextMeshPro scoreText; 
    
    [Header("Team Setup")]
    public Transform redTeamParent;
    public Transform blueTeamParent;
    
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
    private List<CaptureTheFlagAgent> redAgents = new List<CaptureTheFlagAgent>();
    private List<CaptureTheFlagAgent> blueAgents = new List<CaptureTheFlagAgent>();
    
    void Awake()
    {
        if (redFlagObject != null) SetupFlag(redFlagObject.transform, Team.Red);
        if (blueFlagObject != null) SetupFlag(blueFlagObject.transform, Team.Blue);
        
        SetupBase(redBase, Team.Red);
        SetupBase(blueBase, Team.Blue);
        
        redTeamGroup = new SimpleMultiAgentGroup();
        blueTeamGroup = new SimpleMultiAgentGroup();
        
        FindAndSetupAgents();
        
        if (redFlagObject != null) redFlagStartPos = redFlagObject.transform.position;
        if (blueFlagObject != null) blueFlagStartPos = blueFlagObject.transform.position;
    }
    
    void Start()
    {
        ResetGame();
        UpdateScoreDisplay();
    }
    
    void FixedUpdate()
    {
        if (!gameActive) return;
        
        currentStep++;
        if (currentStep >= maxSteps)
        {
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
        
        Collider collider = baseTransform.GetComponent<Collider>();
        if (collider == null)
        {
            BoxCollider boxCollider = baseTransform.gameObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(5, 1, 5); 
        }
        else
        {
            collider.isTrigger = true;
        }
    }
    
    private void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            scoreText.text = $"Red: {redScore}  |  Blue: {blueScore}";
        }
    }
    
    public void ResetGame()
    {
        redScore = 0;
        blueScore = 0;
        currentStep = 0;
        gameActive = true;
        
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
        
        UpdateScoreDisplay();
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
        }
        
        // Reset blue flag
        if (blueFlagObject != null)
        {
            blueFlagObject.transform.position = blueFlagStartPos;
            blueFlagObject.transform.rotation = Quaternion.identity;
            blueFlagObject.SetActive(true);
            
            Flag blueFlagComponent = blueFlagObject.GetComponent<Flag>();
            if (blueFlagComponent != null) blueFlagComponent.isCarried = false;
        }
    }
    
    public void FlagPickedUp(Team flagTeam)
    {
        if (flagTeam == Team.Red)
        {
            // Individual penalty for team whose flag was stolen
            foreach (var agent in redAgents)
            {
                agent.AddReward(-0.5f);
            }
            
            // Group reward: blue team gets group reward for coordinated flag capture
            blueTeamGroup.AddGroupReward(0.3f);
            
            // Group reward: Red team gets group penalty for losing flag
            redTeamGroup.AddGroupReward(-0.3f);
        }
        else
        {
            // Individual penalty for team whose flag was stolen
            foreach (var agent in blueAgents)
            {
                agent.AddReward(-0.5f);
            }
            
            // Group reward: Red team gets group reward for coordinated flag capture
            redTeamGroup.AddGroupReward(0.3f);
            
            // Group reward: Blue team gets group penalty for losing flag
            blueTeamGroup.AddGroupReward(-0.3f);
        }
    }
    
    public void ScoreFlag(Team scoringTeam)
    {
        if (scoringTeam == Team.Red)
        {
            redScore++;
            
            redTeamGroup.AddGroupReward(1.0f);
            blueTeamGroup.AddGroupReward(-0.5f);
            
            if (blueFlagObject != null)
            {
                blueFlagObject.transform.position = blueFlagStartPos;
                blueFlagObject.transform.rotation = Quaternion.identity;
                blueFlagObject.SetActive(true); 
                
                Flag blueFlagComponent = blueFlagObject.GetComponent<Flag>();
                if (blueFlagComponent != null) 
                {
                    blueFlagComponent.isCarried = false;
                }
            }
            
            // Check for win
            if (redScore >= scoreToWin)
            {
                redTeamGroup.AddGroupReward(2.0f);  // Winner bonus
                blueTeamGroup.AddGroupReward(-2.0f); // Loser penalty
                
                EndEpisode();
            }
        }
        else // Blue team scores
        {
            blueScore++;
            
            blueTeamGroup.AddGroupReward(1.0f);
            redTeamGroup.AddGroupReward(-0.5f);
            
            if (redFlagObject != null)
            {
                redFlagObject.transform.position = redFlagStartPos;
                redFlagObject.transform.rotation = Quaternion.identity;
                redFlagObject.SetActive(true); 
                
                Flag redFlagComponent = redFlagObject.GetComponent<Flag>();
                if (redFlagComponent != null) 
                {
                    redFlagComponent.isCarried = false;
                }
            }
            
            if (blueScore >= scoreToWin)
            {
                blueTeamGroup.AddGroupReward(2.0f);  // Winner bonus
                redTeamGroup.AddGroupReward(-2.0f); // Loser penalty
                
                EndEpisode();
            }
        }
        
        UpdateScoreDisplay();
    }

    public void ReturnFlag(Team flagTeam)
    {
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
    
    public void SuccessfulTag(Team taggingTeam, bool wasCarryingFlag)
    {
        if (wasCarryingFlag)
        {
            if (taggingTeam == Team.Red)
            {
                redTeamGroup.AddGroupReward(0.5f);
                blueTeamGroup.AddGroupReward(-0.2f);
            }
            else
            {
                blueTeamGroup.AddGroupReward(0.5f);
                redTeamGroup.AddGroupReward(-0.2f);
            }
        }
        else
        {
            if (taggingTeam == Team.Red)
            {
                redTeamGroup.AddGroupReward(0.1f);
            }
            else
            {
                blueTeamGroup.AddGroupReward(0.1f);
            }
        }
    }
    
    private void EndEpisode()
    {
        gameActive = false;
        
        redTeamGroup.EndGroupEpisode();
        blueTeamGroup.EndGroupEpisode();
        
        ResetGame();
    }
}