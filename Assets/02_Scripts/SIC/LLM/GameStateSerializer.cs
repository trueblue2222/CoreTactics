using System.Collections.Generic;
using UnityEngine;

// 게임 상태를 JSON으로 직렬화하고, 유닛/코어에 안정적인 LLM ID를 부여·조회합니다.
// GameInit 시 InitializeUnitIds()를 반드시 호출해야 합니다.
public class GameStateSerializer : MonoBehaviour
{
    public static GameStateSerializer Instance { get; private set; }

    private Dictionary<Unit, string> unitToId = new Dictionary<Unit, string>();
    private Dictionary<string, Unit> idToUnit = new Dictionary<string, Unit>();
    private Dictionary<string, Core> idToCore = new Dictionary<string, Core>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─── ID 초기화 (TurnManager.OnGameInit에서 호출) ────────────────────
    public void InitializeUnitIds()
    {
        unitToId.Clear();
        idToUnit.Clear();
        idToCore.Clear();

        var counters = new Dictionary<string, int>();
        foreach (Unit unit in FindObjectsOfType<Unit>())
        {
            string key = $"{unit.team.ToLower()}_{unit.unitClass.ToString().ToLower()}";
            if (!counters.ContainsKey(key)) counters[key] = 0;
            string id = $"{key}_{counters[key]++}";
            unitToId[unit] = id;
            idToUnit[id] = unit;
        }

        foreach (Core core in FindObjectsOfType<Core>())
        {
            string id = $"{core.team.ToLower()}_core";
            idToCore[id] = core;
        }

        Debug.Log($"[GameStateSerializer] ID 초기화: 유닛 {unitToId.Count}개, 코어 {idToCore.Count}개");
    }

    public string GetUnitId(Unit unit) =>
        unitToId.TryGetValue(unit, out string id) ? id : null;

    public Unit FindUnitById(string id) =>
        idToUnit.TryGetValue(id, out Unit unit) ? unit : null;

    public Core FindCoreById(string id) =>
        idToCore.TryGetValue(id, out Core core) ? core : null;

    // ─── 게임 상태 직렬화 ────────────────────────────────────────────────
    public string SerializeCurrentGameState()
    {
        var snapshot = new GameStateSnapshot
        {
            turn = TurnManager.Instance.TurnCount,
            playerUnits = new List<UnitSnapshot>(),
            enemyUnits = new List<UnitSnapshot>(),
            obstacles = new List<ObstacleSnapshot>()
        };

        foreach (var pair in unitToId)
        {
            Unit unit = pair.Key;
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.currentHp <= 0) continue;

            UnitSnapshot us = BuildUnitSnapshot(unit, pair.Value);
            if (unit.team == "Player") snapshot.playerUnits.Add(us);
            else snapshot.enemyUnits.Add(us);
        }

        foreach (var pair in idToCore)
        {
            Core core = pair.Value;
            if (core == null) continue;
            var cs = new CoreSnapshot
            {
                id = pair.Key,
                team = core.team,
                position = ToCell(core.transform.position),
                currentHp = core.currentHp,
                maxHp = core.maxHp
            };
            if (core.team == "Player") snapshot.playerCore = cs;
            else snapshot.enemyCore = cs;
        }

        foreach (Obstacle obs in FindObjectsOfType<Obstacle>())
        {
            if (!obs.gameObject.activeInHierarchy) continue;
            snapshot.obstacles.Add(new ObstacleSnapshot
            {
                position = ToCell(obs.transform.position),
                type = obs.IsPassable() ? "Spike" : "Barricade"
            });
        }

        return JsonUtility.ToJson(snapshot);
    }

    private UnitSnapshot BuildUnitSnapshot(Unit unit, string id)
    {
        Vector3Int cell = BattleManager.Instance.gridTilemap.WorldToCell(unit.transform.position);
        var us = new UnitSnapshot
        {
            id = id,
            unitClass = unit.unitClass.ToString(),
            team = unit.team,
            position = new CellPos(cell.x, cell.y),
            currentHp = unit.currentHp,
            maxHp = unit.maxHp,
            atk = unit.atk,
            def = unit.def,
            moveRange = unit.moveRange,
            attackRange = unit.attackRange,
            skillCooldown = unit.skillCooldown,
            isSniperMode = unit.isSniperMode
        };

        if (unit.team == "Enemy")
        {
            us.reachableCells = ComputeReachableCells(unit, cell);
            us.attackableTargetIds = ComputeAttackableTargetIds(cell, unit.attackRange);
        }

        return us;
    }

    // BFS로 이동 가능한 셀 목록 계산
    private List<CellPos> ComputeReachableCells(Unit unit, Vector3Int start)
    {
        var result = new List<CellPos> { new CellPos(start.x, start.y) };
        if (unit.moveRange <= 0) return result;

        var queue = new Queue<Vector3Int>();
        var visited = new Dictionary<Vector3Int, int>();
        queue.Enqueue(start);
        visited[start] = 0;

        Vector3Int[] dirs = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        while (queue.Count > 0)
        {
            Vector3Int cur = queue.Dequeue();
            int dist = visited[cur];
            if (dist >= unit.moveRange) continue;

            foreach (Vector3Int dir in dirs)
            {
                Vector3Int next = cur + dir;
                if (visited.ContainsKey(next)) continue;

                Vector3 wp = BattleManager.Instance.gridTilemap.GetCellCenterWorld(next);
                if (IsCellPassable(wp))
                {
                    visited[next] = dist + 1;
                    queue.Enqueue(next);
                    result.Add(new CellPos(next.x, next.y));
                }
            }
        }
        return result;
    }

    // 현재 위치에서 공격 사거리 내에 있는 플레이어 유닛/코어의 ID만 반환합니다.
    // LLM이 거리 계산 없이 이 목록에서 바로 고르도록 유도합니다.
    private List<string> ComputeAttackableTargetIds(Vector3Int unitCell, int range)
    {
        var result = new List<string>();

        foreach (var pair in unitToId)
        {
            Unit target = pair.Key;
            if (target == null || !target.gameObject.activeInHierarchy || target.currentHp <= 0) continue;
            if (target.team != "Player") continue;

            Vector3Int targetCell = BattleManager.Instance.gridTilemap.WorldToCell(target.transform.position);
            int dist = Mathf.Abs(targetCell.x - unitCell.x) + Mathf.Abs(targetCell.y - unitCell.y);
            if (dist <= range) result.Add(pair.Value);
        }

        foreach (var pair in idToCore)
        {
            Core core = pair.Value;
            if (core == null || core.currentHp <= 0) continue;
            if (core.team != "Player") continue;

            Vector3Int coreCell = BattleManager.Instance.gridTilemap.WorldToCell(core.transform.position);
            int dist = Mathf.Abs(coreCell.x - unitCell.x) + Mathf.Abs(coreCell.y - unitCell.y);
            if (dist <= range) result.Add(pair.Key);
        }

        return result;
    }

    private bool IsCellPassable(Vector3 worldPos)
    {
        foreach (Collider2D hit in Physics2D.OverlapPointAll(worldPos))
        {
            Obstacle obs = hit.GetComponent<Obstacle>();
            if (obs != null && !obs.IsPassable()) return false;
            if (hit.GetComponent<Unit>() != null) return false;
            if (hit.GetComponent<Core>() != null) return false;
        }
        return true;
    }

    private CellPos ToCell(Vector3 worldPos)
    {
        Vector3Int c = BattleManager.Instance.gridTilemap.WorldToCell(worldPos);
        return new CellPos(c.x, c.y);
    }
}
