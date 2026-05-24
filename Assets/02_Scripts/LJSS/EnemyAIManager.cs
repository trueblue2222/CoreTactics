using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAIManager : MonoBehaviour
{
    public static EnemyAIManager Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    public void ExecuteFallbackAI()
    {
        StartCoroutine(AITurnRoutine());
    }

    private IEnumerator AITurnRoutine()
    {
        Debug.Log("[EnemyAI] Fallback AI 시작");

        // 맵에 있는 모든 유닛 탐색
        Unit[] allUnits = FindObjectsOfType<Unit>();
        List<Unit> enemyUnits = new List<Unit>();
        List<Unit> playerUnits = new List<Unit>();
        Core playerCore = null;

        foreach (Unit u in allUnits)
        {
            if (u.team == "Enemy" && u.currentHp > 0 && u.gameObject.activeInHierarchy) enemyUnits.Add(u);
            else if (u.team == "Player" && u.currentHp > 0 && u.gameObject.activeInHierarchy) playerUnits.Add(u);
        }

        // Player 코어 탐색
        Core[] allCores = FindObjectsOfType<Core>();
        foreach (Core c in allCores)
        {
            if (c.team == "Player") playerCore = c;
        }

        if (enemyUnits.Count == 0)
        {
            // 남은 적이 없다면 즉시 턴 종료
            yield return new WaitForSeconds(0.5f);
            TurnManager.Instance.ChangeState(GameState.PlayerTurnStart);
            yield break;
        }

        // 2. 행동할 단 1명의 적 유닛 선정 (가장 가까운 타겟을 가진 적)
        Unit bestEnemy = null;
        Transform bestTarget = null;
        int globalMinDistance = int.MaxValue;

        foreach (Unit enemy in enemyUnits)
        {
            Vector3Int enemyCell = BattleManager.Instance.gridTilemap.WorldToCell(enemy.transform.position);

            // 플레이어 유닛들과 거리 비교
            foreach (Unit pUnit in playerUnits)
            {
                Vector3Int pCell = BattleManager.Instance.gridTilemap.WorldToCell(pUnit.transform.position);
                int dist = GetManhattanDistance(enemyCell, pCell);

                if (enemy.moveRange == 0 && dist > enemy.attackRange) continue;

                if (dist < globalMinDistance)
                {
                    globalMinDistance = dist;
                    bestEnemy = enemy;
                    bestTarget = pUnit.transform;
                }
            }

            // 플레이어 코어와 거리 비교
            if (playerCore != null)
            {
                Vector3Int coreCell = BattleManager.Instance.gridTilemap.WorldToCell(playerCore.transform.position);
                int dist = GetManhattanDistance(enemyCell, coreCell);

                if (enemy.moveRange == 0 && dist > enemy.attackRange) continue;

                if (dist < globalMinDistance)
                {
                    globalMinDistance = dist;
                    bestEnemy = enemy;
                    bestTarget = playerCore.transform;
                }
            }
        }

        if (bestEnemy == null || bestTarget == null)
        {
            Debug.Log("[EnemyAI] 행동 가능한 적이 없어 턴을 스킵합니다.");
            yield return new WaitForSeconds(0.5f);
            TurnManager.Instance.ChangeState(GameState.PlayerTurnStart);
            yield break;
        }

        // 💡 3. 선정된 1명만 행동 실행 (스킬 -> 이동 -> 공격)
        Vector3Int enemyCellPos = BattleManager.Instance.gridTilemap.WorldToCell(bestEnemy.transform.position);
        Vector3Int targetCellPos = BattleManager.Instance.gridTilemap.WorldToCell(bestTarget.position);
        bool isSkillUsed = false;

        // --- 스킬 사용 판단 ---
        if (bestEnemy.skillCooldown <= 0)
        {
            if (bestEnemy.unitClass == Unit.UnitClass.Archer && !bestEnemy.isSniperMode)
            {
                if (globalMinDistance == 5)
                {
                    bestEnemy.OnSkillButtonPressed();
                    isSkillUsed = true;
                    yield return new WaitForSeconds(0.5f);
                }
            }
            else if (bestEnemy.unitClass == Unit.UnitClass.Warrior)
            {
                if (enemyCellPos.x == targetCellPos.x || enemyCellPos.y == targetCellPos.y)
                {
                    // 타겟이 3칸 이내에 있어야, 관통하고도 4칸(사거리) 이내의 빈칸에 착지할 수 있음
                    if (globalMinDistance > 0 && globalMinDistance <= 4)
                    {
                        // 타겟을 향하는 방향 계산
                        Vector3Int dir = new Vector3Int(
                            Mathf.Clamp(targetCellPos.x - enemyCellPos.x, -1, 1),
                            Mathf.Clamp(targetCellPos.y - enemyCellPos.y, -1, 1),
                            0
                        );

                        // 타겟을 관통한 '바로 뒷칸'을 착지 지점으로 설정
                        Vector3Int landingCell = targetCellPos + dir;
                        Vector3 landingWorldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(landingCell);
                        landingWorldPos.z = 0;

                        // OverlapPointAll을 사용하여 착지할 칸에 뭐가 있는지 전부 검사!
                        Collider2D[] hits = Physics2D.OverlapPointAll(landingWorldPos);
                        bool canLand = true;

                        foreach (Collider2D hit in hits)
                        {
                            if (hit.GetComponent<Unit>() != null || hit.GetComponent<Core>() != null) canLand = false;
                            Obstacle obs = hit.GetComponent<Obstacle>();
                            if (obs != null && !obs.IsPassable()) canLand = false;
                        }

                        // 착지 지점이 완벽한 빈칸일 때만 돌진 발동!
                        if (canLand)
                        {
                            Debug.Log($"[EnemyAI] {bestEnemy.unitClass} 돌진 발동");
                            bestEnemy.OnSkillTargetClicked(landingCell, null, null);
                            isSkillUsed = true;

                            // 타임아웃이 적용된 돌진 대기
                            float dashTimeout = 2.0f;
                            while (Vector3.Distance(bestEnemy.transform.position, landingWorldPos) > 0.01f && dashTimeout > 0f)
                            {
                                dashTimeout -= Time.deltaTime;
                                yield return null;
                            }
                            yield return new WaitForSeconds(0.2f);
                        }
                    }
                }
            }
        }
        // --- 이동 및 공격 실행 (스킬을 안 썼을 때만) ---
        if (!isSkillUsed)
        {
            int currentDistToTarget = GetManhattanDistance(enemyCellPos, targetCellPos);

            //이번 턴에 행동(이동)을 했는지 추적하는 변수 추가
            bool hasMoved = false;

            // 이동 판단 (사거리 밖이고 이동력이 있을 때)
            if (currentDistToTarget > bestEnemy.attackRange && bestEnemy.moveRange > 0)
            {
                List<Vector3Int> reachableCells = GetReachableCells(bestEnemy);
                Vector3Int bestMoveCell = enemyCellPos;
                int shortestDistToTarget = currentDistToTarget;

                foreach (Vector3Int cell in reachableCells)
                {
                    int distFromNext = GetManhattanDistance(cell, targetCellPos);

                    if (distFromNext < shortestDistToTarget)
                    {
                        shortestDistToTarget = distFromNext;
                        bestMoveCell = cell;
                    }
                    else if (distFromNext == shortestDistToTarget && cell != enemyCellPos && bestMoveCell == enemyCellPos)
                    {
                        bestMoveCell = cell; // 장애물 우회용 옆걸음 허용
                    }
                }

                if (bestMoveCell != enemyCellPos)
                {
                    Vector3 targetWorldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(bestMoveCell);
                    targetWorldPos.z = 0;

                    bool isMoving = true;
                    StartCoroutine(bestEnemy.MoveSmoothly(targetWorldPos, () => isMoving = false));

                    // 타임아웃이 적용된 이동 대기
                    float moveTimeout = 2.0f;
                    while (isMoving && moveTimeout > 0f)
                    {
                        moveTimeout -= Time.deltaTime;
                        yield return null;
                    }

                    enemyCellPos = bestMoveCell; // 이동 후 내 위치 갱신

                    // 이동을 완료했으므로 플래그를 true로 변경
                    hasMoved = true;
                }
            }

            // 공격 판단 (이동 후 다시 거리 계산)
            if (!hasMoved)
            {
                currentDistToTarget = GetManhattanDistance(enemyCellPos, targetCellPos);
                if (currentDistToTarget <= bestEnemy.attackRange)
                {
                    Debug.Log($"[EnemyAI] {bestEnemy.unitClass}가 대상을 기본 공격합니다!");
                    Unit pUnit = bestTarget.GetComponent<Unit>();
                    if (pUnit != null) pUnit.TakeDamage(bestEnemy.atk);

                    Core pCore = bestTarget.GetComponent<Core>();
                    if (pCore != null) pCore.TakeDamage(bestEnemy.atk);

                    yield return new WaitForSeconds(0.5f);
                }
                else
                {
                    // 이동도 못하고 공격도 못하는 상황 (갇혔거나 타겟이 너무 멂)
                    // 너무 순식간에 턴이 넘어가서 무한 대기 버그처럼 느껴지는 현상을 방지
                    Debug.Log($"[EnemyAI] {bestEnemy.unitClass}가 행동할 수 없어 대기합니다.");
                    yield return new WaitForSeconds(0.5f);
                }
            }
        }
        // 확실한 턴 전환 ───
        Debug.Log("[EnemyAI] 적 유닛 행동 완료. 플레이어 턴으로 넘어갑니다.");
        TurnManager.Instance.ChangeState(GameState.PlayerTurnStart);
    }

    private List<Vector3Int> GetReachableCells(Unit unit)
    {
        List<Vector3Int> reachable = new List<Vector3Int>();
        Vector3Int startCell = BattleManager.Instance.gridTilemap.WorldToCell(unit.transform.position);
        int range = unit.moveRange;

        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        Dictionary<Vector3Int, int> visited = new Dictionary<Vector3Int, int>();

        queue.Enqueue(startCell);
        visited.Add(startCell, 0);
        reachable.Add(startCell);

        Vector3Int[] directions = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };

        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            int currentDist = visited[current];

            if (currentDist >= range) continue;

            foreach (Vector3Int dir in directions)
            {
                Vector3Int next = current + dir;

                if (visited.ContainsKey(next)) continue;

                Vector3 nextWorldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(next);
                Collider2D[] hits = Physics2D.OverlapPointAll(nextWorldPos);
                bool isPassable = true;

                foreach (Collider2D hit in hits)
                {
                    Obstacle obstacle = hit.GetComponent<Obstacle>();
                    if (obstacle != null && !obstacle.IsPassable()) isPassable = false;

                    if (hit.GetComponent<Unit>() != null) isPassable = false;
                    if (hit.GetComponent<Core>() != null) isPassable = false;
                }

                if (isPassable)
                {
                    visited.Add(next, currentDist + 1);
                    queue.Enqueue(next);
                    reachable.Add(next);
                }
            }
        }
        return reachable;
    }

    private int GetManhattanDistance(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
