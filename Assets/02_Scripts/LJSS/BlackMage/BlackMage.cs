using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlackMage : MonoBehaviour
{
    public static BlackMage Instance { get; private set; }

    [Header("Map Settings")]
    public int minX = -7;
    public int maxX = 6;
    public int minY = -3;
    public int maxY = 2;

    [Header("Teleport Settings")]
    public int teleportRange = 4;

    [Header("Highlight Prefabs")]
    public GameObject whiteHighlightPrefab; // 플레이어 진영 마커
    public GameObject blackHighlightPrefab; // 적 진영 마커

    private int roundCounter = 0;

    private Unit targetedPlayer;
    private Unit targetedEnemy;
    private Vector3 destPlayer;
    private Vector3 destEnemy;

    private GameObject playerTargetObj;
    private GameObject playerDestObj;
    private GameObject enemyTargetObj;
    private GameObject enemyDestObj;

    private List<GameObject> activePortals = new List<GameObject>();

    void Awake()
    {
        Instance = this;
    }

    public void OnRoundPassed()
    {
        roundCounter++;

        if (roundCounter == 2)
        {
            Debug.Log("blackMage가 다음 턴에 텔레포트 될 유닛과 목적지를 지정합니다");
            PrepareTeleport();
        }
        else if (roundCounter >= 3)
        {
            Debug.Log("blackMage가 강제 텔레포트 발동");
            StartCoroutine(ExecuteTeleportRoutine());
            roundCounter = 0;
        }
    }

    private void PrepareTeleport()
    {
        List<Unit> players = new List<Unit>();
        List<Unit> enemies = new List<Unit>();

        Unit[] allUnits = FindObjectsOfType<Unit>();
        foreach (Unit u in allUnits)
        {
            if (u.gameObject.activeInHierarchy)
            {
                if (u.team == "Player") players.Add(u);
                else enemies.Add(u);
            }
        }

        if (players.Count > 0)
        {
            targetedPlayer = players[Random.Range(0, players.Count)];
            destPlayer = GetRandomEmptyTileInRange(targetedPlayer);

            playerTargetObj = Instantiate(whiteHighlightPrefab, targetedPlayer.transform.position, Quaternion.identity);
            playerTargetObj.transform.SetParent(targetedPlayer.transform);

            playerDestObj = Instantiate(whiteHighlightPrefab, destPlayer, Quaternion.identity);
            MakeObstacle(playerDestObj);

            activePortals.Add(playerTargetObj);
            activePortals.Add(playerDestObj);
        }

        if (enemies.Count > 0)
        {
            targetedEnemy = enemies[Random.Range(0, enemies.Count)];
            destEnemy = GetRandomEmptyTileInRange(targetedEnemy, destPlayer);

            enemyTargetObj = Instantiate(blackHighlightPrefab, targetedEnemy.transform.position, Quaternion.identity);
            enemyTargetObj.transform.SetParent(targetedEnemy.transform);

            enemyDestObj = Instantiate(blackHighlightPrefab, destEnemy, Quaternion.identity);
            MakeObstacle(enemyDestObj);

            activePortals.Add(enemyTargetObj);
            activePortals.Add(enemyDestObj);
        }
    }

    private IEnumerator ExecuteTeleportRoutine()
    {
        TurnManager.Instance.SetInputBlocked(true);
        yield return new WaitForSeconds(0.6f);

        if (targetedPlayer != null && targetedPlayer.gameObject.activeInHierarchy)
        {
            targetedPlayer.transform.position = destPlayer;
        }

        if (targetedEnemy != null && targetedEnemy.gameObject.activeInHierarchy)
        {
            targetedEnemy.transform.position = destEnemy;
        }

        Cleanup();

        TurnManager.Instance.SetInputBlocked(false);
        Debug.Log("blackMage 텔레포트 완료");
    }

    private void Cleanup()
    {
        foreach (GameObject portal in activePortals)
        {
            if (portal != null) Destroy(portal);
        }
        activePortals.Clear();
        
        targetedPlayer = null;
        targetedEnemy = null;
    }

    private void MakeObstacle(GameObject obj)
    {
        if (obj.GetComponent<BoxCollider2D>() == null)
        {
            BoxCollider2D box = obj.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
        }
        if (obj.GetComponent<Obstacle>() == null)
        {
            obj.AddComponent<Obstacle>();
        }
    }

    private Vector3 GetRandomEmptyTileInRange(Unit targetUnit, Vector3 ignorePos = default)
    {
        Vector3Int centerCell = BattleManager.Instance.gridTilemap.WorldToCell(targetUnit.transform.position);

        // 중심 유닛 기준 -4칸 ~ +4칸의 정사각형 영역 설정 (맵 전체 크기를 벗어나지 않도록 강제 조정)
        int localMinX = Mathf.Max(minX, centerCell.x - teleportRange);
        int localMaxX = Mathf.Min(maxX, centerCell.x + teleportRange);
        int localMinY = Mathf.Max(minY, centerCell.y - teleportRange);
        int localMaxY = Mathf.Min(maxY, centerCell.y + teleportRange);

        int maxAttempts = 100;
        while (maxAttempts > 0)
        {
            maxAttempts--;
            
            // 제한된 사각형 범위 내에서만 랜덤 좌표 추출 (맨해튼이 아닌 완전한 사각형 범위)
            int randomX = Random.Range(localMinX, localMaxX + 1);
            int randomY = Random.Range(localMinY, localMaxY + 1);
            
            Vector3Int randomCell = new Vector3Int(randomX, randomY, 0);
            Vector3 worldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(randomCell);
            worldPos.z = 0;

            if (worldPos == ignorePos) continue;

            Collider2D[] hits = Physics2D.OverlapPointAll(worldPos);
            bool hasObstacleOrUnit = false;
            foreach (Collider2D hit in hits)
            {
                if (hit.GetComponent<Obstacle>() != null || 
                    hit.GetComponent<Unit>() != null || 
                    hit.GetComponent<Core>() != null)
                {
                    hasObstacleOrUnit = true;
                    break;
                }
            }

            if (!hasObstacleOrUnit) return worldPos; 
        }
        
        return Vector3.zero;
    }
}
