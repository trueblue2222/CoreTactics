using System;
using System.Collections.Generic;

// LLM이 응답하는 행동 데이터
[Serializable]
public class EnemyActionData
{
    public string unitId;
    public string actionType;        // "move" | "attack" | "skill" | "skill2" | "skip"
    public CellPos moveTarget;
    public string attackTargetId;
    public string skillTargetId;     // Magician 텔레포트 대상
    public CellPos skillDestination; // Magician 텔레포트 목적지
    public CellPos dashDestination;  // Warrior 돌진 착지 지점
    public CellPos skill2Target;     // Archer/Magician 2스킬 타겟 셀
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
    public BigObjectSnapshot bigObject;
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
    public bool isRooted;                         // rootedTurns > 0: 이동 불가, Warrior 돌진 불가
    public List<CellPos> reachableCells;          // 적 유닛에만 포함: 이동 가능한 셀 목록
    public List<string> attackableTargetIds;      // 적 유닛에만 포함: 지금 바로 공격 가능한 ID 목록
    public int distanceToPlayerCore;              // 적 유닛에만 포함: 플레이어 코어까지 맨해튼 거리
    public CellPos bestMoveTarget;                // 적 유닛에만 포함: reachableCells 중 playerCore에 가장 가까운 셀
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
    public string type; // "Barricade" | "Spike" | "Bomb"
}

[Serializable]
public class BigObjectSnapshot
{
    public string type;           // "GiantSlime" | "BlackMage" | "None"
    public int cooldownRemaining; // 다음 발동까지 남은 라운드 수

    // GiantSlime 전용: 현재 맵에 존재하는 점액 위치 목록
    public List<CellPos> slimePuddles;

    // BlackMage 전용: 경고 단계(isWarningPhase=true)일 때만 유효
    public bool isWarningPhase;
    public string warnedPlayerUnitId;  // 다음 턴 텔레포트될 플레이어 유닛 ID
    public string warnedEnemyUnitId;   // 다음 턴 텔레포트될 적 유닛 ID
    public CellPos playerTeleportDest; // 해당 플레이어 유닛의 텔레포트 목적지
    public CellPos enemyTeleportDest;  // 해당 적 유닛의 텔레포트 목적지
}
