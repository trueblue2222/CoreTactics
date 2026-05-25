using System;
using System.Collections.Generic;

// LLM이 응답하는 행동 데이터
[Serializable]
public class EnemyActionData
{
    public string unitId;
    public string actionType;        // "move" | "attack" | "skill" | "skip"
    public CellPos moveTarget;
    public string attackTargetId;
    public string skillTargetId;     // Magician 텔레포트 대상
    public CellPos skillDestination; // Magician 텔레포트 목적지
    public CellPos dashDestination;  // Warrior 돌진 착지 지점
}

// 그리드 좌표
[Serializable]
public class CellPos
{
    public int x;
    public int y;
    public CellPos() { }
    public CellPos(int x, int y) { this.x = x; this.y = y; }
    public override string ToString() => $"({x},{y})";
}

// ── 게임 상태 스냅샷 DTO ─────────────────────────────────────────────────

[Serializable]
public class GameStateSnapshot
{
    public int turn;
    public List<UnitSnapshot> playerUnits;
    public List<UnitSnapshot> enemyUnits;
    public CoreSnapshot playerCore;
    public CoreSnapshot enemyCore;
    public List<ObstacleSnapshot> obstacles;
}

[Serializable]
public class UnitSnapshot
{
    public string id;
    public string unitClass;
    public string team;
    public CellPos position;
    public int currentHp;
    public int maxHp;
    public int atk;
    public int def;
    public int moveRange;
    public int attackRange;
    public int skillCooldown;
    public bool isSniperMode;
    public List<CellPos> reachableCells;         // 적 유닛에만 포함: 이동 가능한 셀 목록
    public List<string> attackableTargetIds;     // 적 유닛에만 포함: 지금 바로 공격 가능한 ID 목록
}

[Serializable]
public class CoreSnapshot
{
    public string id;
    public string team;
    public CellPos position;
    public int currentHp;
    public int maxHp;
}

[Serializable]
public class ObstacleSnapshot
{
    public CellPos position;
    public string type; // "Barricade" | "Spike"
}
