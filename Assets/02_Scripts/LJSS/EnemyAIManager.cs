using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAIManager : MonoBehaviour
{
    public static EnemyAIManager Instance { get; private set; }

    void Awake() { Instance = this; }

    public void ExecuteFallbackAI()
    {
        StartCoroutine(AITurnRoutine());
    }

    private IEnumerator AITurnRoutine()
    {
        Debug.Log("[EnemyAI] Fallback AI 시작");

        Unit[] allUnits = FindObjectsOfType<Unit>();
        var enemyUnits  = new List<Unit>();
        var playerUnits = new List<Unit>();
        Core playerCore = null;

        foreach (Unit u in allUnits)
        {
            if (u.team == "Enemy" && u.currentHp > 0 && u.gameObject.activeInHierarchy)
                enemyUnits.Add(u);
            else if (u.team == "Player" && u.currentHp > 0 && u.gameObject.activeInHierarchy)
                playerUnits.Add(u);
        }
        foreach (Core c in FindObjectsOfType<Core>())
            if (c.team == "Player") playerCore = c;

        if (enemyUnits.Count == 0)
        {
            yield return new WaitForSeconds(0.5f);
            TurnManager.Instance.ChangeState(GameState.PlayerTurnStart);
            yield break;
        }

        // 모든 적 유닛이 한 번씩 행동
        foreach (Unit enemy in enemyUnits)
            yield return StartCoroutine(ExecuteUnitAI(enemy, playerUnits, playerCore));

        Debug.Log("[EnemyAI] 모든 적 유닛 행동 완료. 플레이어 턴으로 전환.");
        TurnManager.Instance.ChangeState(GameState.PlayerTurnStart);
    }

    // ─── 개별 유닛 AI ────────────────────────────────────────────────────
    private IEnumerator ExecuteUnitAI(Unit enemy, List<Unit> playerUnits, Core playerCore)
    {
        // 이 유닛의 최근접 타겟 탐색
        Vector3Int enemyCellPos = BattleManager.Instance.gridTilemap.WorldToCell(enemy.transform.position);
        Transform bestTarget   = null;
        int minDist            = int.MaxValue;

        foreach (Unit pUnit in playerUnits)
        {
            Vector3Int pCell = BattleManager.Instance.gridTilemap.WorldToCell(pUnit.transform.position);
            int dist = GetManhattanDistance(enemyCellPos, pCell);
            if (enemy.moveRange == 0 && dist > enemy.attackRange) continue;
            if (dist < minDist) { minDist = dist; bestTarget = pUnit.transform; }
        }
        if (playerCore != null)
        {
            Vector3Int coreCell = BattleManager.Instance.gridTilemap.WorldToCell(playerCore.transform.position);
            int dist = GetManhattanDistance(enemyCellPos, coreCell);
            if (!(enemy.moveRange == 0 && dist > enemy.attackRange) && dist < minDist)
            { minDist = dist; bestTarget = playerCore.transform; }
        }

        if (bestTarget == null)
        {
            yield return new WaitForSeconds(0.3f);
            yield break;
        }

        Vector3Int targetCellPos = BattleManager.Instance.gridTilemap.WorldToCell(bestTarget.position);
        Unit   targetUnit = bestTarget.GetComponent<Unit>();
        Core   targetCore = bestTarget.GetComponent<Core>();
        int    distX      = Mathf.Abs(enemyCellPos.x - targetCellPos.x);
        int    distY      = Mathf.Abs(enemyCellPos.y - targetCellPos.y);
        bool   skillUsed  = false;

        // ─── 스킬 판단 ───────────────────────────────────────────────────
        if (enemy.skillCooldown <= 0)
        {
            // 🏹 궁수
            if (enemy.unitClass == Unit.UnitClass.Archer)
            {
                if (minDist > 0 && minDist <= 4)
                {
                    Debug.Log($"[EnemyAI] {enemy.unitClass} 2스킬(화살비) 발동");
                    enemy.OnSecondSkillButtonPressed();
                    enemy.OnSkillTargetClicked(targetCellPos, targetUnit, targetCore);
                    skillUsed = true;
                    yield return new WaitForSeconds(0.5f);
                }
                else if (!enemy.isSniperMode && minDist == 5)
                {
                    Debug.Log($"[EnemyAI] {enemy.unitClass} 1스킬(저격 모드) 발동");
                    enemy.OnSkillButtonPressed();
                    skillUsed = true;
                    yield return new WaitForSeconds(0.5f);
                }
            }
            // 🔮 마법사
            else if (enemy.unitClass == Unit.UnitClass.Magician)
            {
                // 아군 구출 (체력 50% 이하)
                Unit allyToSave = null;
                foreach (Unit ally in FindObjectsOfType<Unit>())
                {
                    if (ally.team != "Enemy" || ally.currentHp <= 0 || !ally.gameObject.activeInHierarchy) continue;
                    if (ally.currentHp > ally.maxHp / 2f) continue;
                    Vector3Int allyCell = BattleManager.Instance.gridTilemap.WorldToCell(ally.transform.position);
                    if (GetManhattanDistance(enemyCellPos, allyCell) <= 4) { allyToSave = ally; break; }
                }

                bool usedTeleport = false;
                if (allyToSave != null)
                {
                    Vector3Int allyCell  = BattleManager.Instance.gridTilemap.WorldToCell(allyToSave.transform.position);
                    Vector3Int backDir   = new Vector3Int(1, 0, 0);
                    Vector3Int landCell  = allyCell;
                    bool foundLanding    = false;

                    for (int i = 3; i >= 1; i--)
                    {
                        Vector3Int check = allyCell + backDir * i;
                        Vector3 checkPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(check);
                        checkPos.z = 0;
                        bool canLand = true;
                        foreach (Collider2D hit in Physics2D.OverlapPointAll(checkPos))
                        {
                            if (hit.GetComponent<Unit>() != null || hit.GetComponent<Core>() != null) canLand = false;
                            if (hit.GetComponent<Obstacle>() != null) canLand = false;
                        }
                        if (canLand) { landCell = check; foundLanding = true; break; }
                    }

                    if (foundLanding)
                    {
                        Debug.Log($"[EnemyAI] 마법사 1스킬(텔레포트): {allyToSave.unitClass} 구출");
                        enemy.OnSkillButtonPressed();
                        enemy.OnSkillTargetClicked(allyCell, allyToSave, null);
                        yield return new WaitForSeconds(0.2f);
                        enemy.OnSkillDestinationClicked(landCell);
                        skillUsed = true; usedTeleport = true;
                        yield return new WaitForSeconds(0.5f);
                    }
                }

                if (!usedTeleport && distX <= 2 && distY <= 2 && minDist > 0)
                {
                    Debug.Log($"[EnemyAI] {enemy.unitClass} 2스킬(번개) 발동");
                    enemy.OnSecondSkillButtonPressed();
                    enemy.OnSkillTargetClicked(targetCellPos, targetUnit, targetCore);
                    skillUsed = true;
                    yield return new WaitForSeconds(0.5f);
                }
            }
            // ⚔️ 전사
            else if (enemy.unitClass == Unit.UnitClass.Warrior && enemy.rootedTurns <= 0)
            {
                if (distX <= 1 && distY <= 1 && minDist > 0)
                {
                    Debug.Log($"[EnemyAI] {enemy.unitClass} 2스킬(대검 소환) 발동");
                    enemy.OnSecondSkillButtonPressed();
                    skillUsed = true;
                    yield return new WaitForSeconds(0.5f);
                }
                else if ((enemyCellPos.x == targetCellPos.x || enemyCellPos.y == targetCellPos.y)
                         && minDist > 0 && minDist <= 4)
                {
                    Vector3Int dir = new Vector3Int(
                        Mathf.Clamp(targetCellPos.x - enemyCellPos.x, -1, 1),
                        Mathf.Clamp(targetCellPos.y - enemyCellPos.y, -1, 1), 0);
                    Vector3Int landCell = targetCellPos + dir;
                    Vector3 landWorld   = BattleManager.Instance.gridTilemap.GetCellCenterWorld(landCell);
                    landWorld.z = 0;

                    bool canLand = true;
                    foreach (Collider2D hit in Physics2D.OverlapPointAll(landWorld))
                    {
                        if (hit.GetComponent<Unit>() != null || hit.GetComponent<Core>() != null) canLand = false;
                        Obstacle obs = hit.GetComponent<Obstacle>();
                        if (obs != null && (!obs.IsPassable() || obs.obstacleType == Obstacle.ObstacleType.Spike)) canLand = false;
                    }

                    if (canLand)
                    {
                        Debug.Log($"[EnemyAI] {enemy.unitClass} 1스킬(돌진) 발동");
                        enemy.OnSkillTargetClicked(landCell, null, null);
                        skillUsed = true;

                        float dashTimeout = 2f;
                        while (Vector3.Distance(enemy.transform.position, landWorld) > 0.01f && dashTimeout > 0f)
                        { dashTimeout -= Time.deltaTime; yield return null; }
                        yield return new WaitForSeconds(0.2f);
                    }
                }
            }
        }

        // ─── 이동 / 공격 (스킬 미사용 시) ───────────────────────────────
        if (!skillUsed)
        {
            int curDist = GetManhattanDistance(enemyCellPos, targetCellPos);
            bool hasMoved = false;

            if (curDist > enemy.attackRange && enemy.moveRange > 0 && enemy.rootedTurns <= 0)
            {
                List<Vector3Int> reachable = GetReachableCells(enemy);
                Vector3Int bestCell = enemyCellPos;
                int shortest = curDist;

                foreach (Vector3Int cell in reachable)
                {
                    int d = GetManhattanDistance(cell, targetCellPos);
                    if (d < shortest) { shortest = d; bestCell = cell; }
                    else if (d == shortest && cell != enemyCellPos && bestCell == enemyCellPos) bestCell = cell;
                }

                if (bestCell != enemyCellPos)
                {
                    Vector3 moveWorld = BattleManager.Instance.gridTilemap.GetCellCenterWorld(bestCell);
                    moveWorld.z = 0;
                    bool isMoving = true;
                    StartCoroutine(enemy.MoveSmoothly(moveWorld, () => isMoving = false));
                    float timeout = 2f;
                    while (isMoving && timeout > 0f) { timeout -= Time.deltaTime; yield return null; }
                    enemyCellPos = bestCell;
                    hasMoved = true;
                }
            }

            if (!hasMoved)
            {
                curDist = GetManhattanDistance(enemyCellPos, targetCellPos);
                if (curDist <= enemy.attackRange)
                {
                    Debug.Log($"[EnemyAI] {enemy.unitClass} 기본 공격");
                    enemy.TriggerAttackAnim();
                    if (targetUnit != null) targetUnit.TakeDamage(enemy.atk);
                    else if (targetCore != null) targetCore.TakeDamage(enemy.atk);
                    yield return new WaitForSeconds(0.5f);
                }
                else
                {
                    Debug.Log($"[EnemyAI] {enemy.unitClass} 행동 불가 — 대기");
                    yield return new WaitForSeconds(0.3f);
                }
            }
        }
    }

    // ─── BFS 이동 가능 셀 ─────────────────────────────────────────────────
    private List<Vector3Int> GetReachableCells(Unit unit)
    {
        var reachable = new List<Vector3Int>();
        Vector3Int start = BattleManager.Instance.gridTilemap.WorldToCell(unit.transform.position);

        var queue   = new Queue<Vector3Int>();
        var visited = new Dictionary<Vector3Int, int>();
        queue.Enqueue(start);
        visited[start] = 0;
        reachable.Add(start);

        Vector3Int[] dirs = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        while (queue.Count > 0)
        {
            Vector3Int cur  = queue.Dequeue();
            int        dist = visited[cur];
            if (dist >= unit.moveRange) continue;

            foreach (Vector3Int dir in dirs)
            {
                Vector3Int next = cur + dir;
                if (visited.ContainsKey(next)) continue;

                Vector3 wp = BattleManager.Instance.gridTilemap.GetCellCenterWorld(next);
                bool passable = true;
                foreach (Collider2D hit in Physics2D.OverlapPointAll(wp))
                {
                    Obstacle obs = hit.GetComponent<Obstacle>();
                    if (obs != null && (!obs.IsPassable() || obs.obstacleType == Obstacle.ObstacleType.Spike)) passable = false;
                    if (hit.GetComponent<Unit>() != null) passable = false;
                    if (hit.GetComponent<Core>() != null) passable = false;
                }

                if (passable) { visited[next] = dist + 1; queue.Enqueue(next); reachable.Add(next); }
            }
        }
        return reachable;
    }

    private int GetManhattanDistance(Vector3Int a, Vector3Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
}
