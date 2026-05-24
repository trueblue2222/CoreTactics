using UnityEngine;

// LLM의 JSON 응답을 파싱하고 유효성을 검증합니다.
// 검증 실패 시 null 반환 → TurnManager가 Fallback AI로 전환합니다.
public class LLMActionParser : MonoBehaviour
{
    public static LLMActionParser Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public EnemyActionData ParseAndValidate(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[LLMParser] 빈 응답");
            return null;
        }

        json = StripMarkdownFence(json);

        EnemyActionData action;
        try
        {
            action = JsonUtility.FromJson<EnemyActionData>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LLMParser] JSON 파싱 실패: {e.Message}\n원본: {json}");
            return null;
        }

        if (action == null || string.IsNullOrEmpty(action.unitId) || string.IsNullOrEmpty(action.actionType))
        {
            Debug.LogWarning("[LLMParser] unitId 또는 actionType 누락");
            return null;
        }

        Unit unit = GameStateSerializer.Instance.FindUnitById(action.unitId);
        if (unit == null || !unit.gameObject.activeInHierarchy || unit.currentHp <= 0 || unit.team != "Enemy")
        {
            Debug.LogWarning($"[LLMParser] 유효하지 않은 unitId: '{action.unitId}'");
            return null;
        }

        action.actionType = action.actionType.ToLower().Trim();

        return action.actionType switch
        {
            "move"   => ValidateMove(action, unit),
            "attack" => ValidateAttack(action, unit),
            "skill"  => ValidateSkill(action, unit),
            "skip"   => action,
            _        => LogAndReturn(null, $"[LLMParser] 알 수 없는 actionType: '{action.actionType}'")
        };
    }

    // ─── 이동 검증 ──────────────────────────────────────────────────────
    private EnemyActionData ValidateMove(EnemyActionData action, Unit unit)
    {
        if (action.moveTarget == null)
            return LogAndReturn(null, "[LLMParser] move: moveTarget 누락");

        Vector3Int unitCell = BattleManager.Instance.gridTilemap.WorldToCell(unit.transform.position);
        int dist = Mathf.Abs(action.moveTarget.x - unitCell.x) + Mathf.Abs(action.moveTarget.y - unitCell.y);

        if (dist == 0)
        {
            // 제자리 이동 → skip으로 강등
            action.actionType = "skip";
            return action;
        }

        if (dist > unit.moveRange)
            return LogAndReturn(null, $"[LLMParser] move: 이동 범위 초과 (요청 {dist}, 최대 {unit.moveRange})");

        return action;
    }

    // ─── 공격 검증 ──────────────────────────────────────────────────────
    private EnemyActionData ValidateAttack(EnemyActionData action, Unit unit)
    {
        if (string.IsNullOrEmpty(action.attackTargetId))
            return LogAndReturn(null, "[LLMParser] attack: attackTargetId 누락");

        Transform targetTf = GetTargetTransform(action.attackTargetId);
        if (targetTf == null)
            return LogAndReturn(null, $"[LLMParser] attack: 타겟 없음 '{action.attackTargetId}'");

        Vector3Int unitCell   = BattleManager.Instance.gridTilemap.WorldToCell(unit.transform.position);
        Vector3Int targetCell = BattleManager.Instance.gridTilemap.WorldToCell(targetTf.position);
        int dist = Mathf.Abs(targetCell.x - unitCell.x) + Mathf.Abs(targetCell.y - unitCell.y);

        if (dist > unit.attackRange)
            return LogAndReturn(null, $"[LLMParser] attack: 공격 범위 초과 (거리 {dist}, 사거리 {unit.attackRange})");

        return action;
    }

    // ─── 스킬 검증 ──────────────────────────────────────────────────────
    private EnemyActionData ValidateSkill(EnemyActionData action, Unit unit)
    {
        if (unit.skillCooldown > 0)
            return LogAndReturn(null, $"[LLMParser] skill: 쿨타임 {unit.skillCooldown}턴 남음");

        switch (unit.unitClass)
        {
            case Unit.UnitClass.Warrior:
                if (action.dashDestination == null)
                    return LogAndReturn(null, "[LLMParser] Warrior skill: dashDestination 누락");

                Vector3Int wCell = BattleManager.Instance.gridTilemap.WorldToCell(unit.transform.position);
                bool sameAxis = (action.dashDestination.x == wCell.x) || (action.dashDestination.y == wCell.y);
                if (!sameAxis)
                    return LogAndReturn(null, "[LLMParser] Warrior skill: dashDestination이 같은 행/열이 아님");
                break;

            case Unit.UnitClass.Archer:
                if (unit.isSniperMode)
                    return LogAndReturn(null, "[LLMParser] Archer skill: 이미 저격 모드 중");
                break;

            case Unit.UnitClass.Magician:
                if (string.IsNullOrEmpty(action.skillTargetId) || action.skillDestination == null)
                    return LogAndReturn(null, "[LLMParser] Magician skill: skillTargetId 또는 skillDestination 누락");

                Unit teleTarget = GameStateSerializer.Instance.FindUnitById(action.skillTargetId);
                if (teleTarget == null || !teleTarget.gameObject.activeInHierarchy || teleTarget.currentHp <= 0)
                    return LogAndReturn(null, $"[LLMParser] Magician skill: 유효하지 않은 skillTargetId '{action.skillTargetId}'");
                break;
        }

        return action;
    }

    // ─── 헬퍼 ──────────────────────────────────────────────────────────
    private Transform GetTargetTransform(string targetId)
    {
        Unit u = GameStateSerializer.Instance.FindUnitById(targetId);
        if (u != null && u.gameObject.activeInHierarchy && u.currentHp > 0) return u.transform;

        Core c = GameStateSerializer.Instance.FindCoreById(targetId);
        return c != null ? c.transform : null;
    }

    // LLM이 코드펜스(```json ... ```)로 감싸는 경우 제거
    private string StripMarkdownFence(string json)
    {
        json = json.Trim();
        if (!json.StartsWith("```")) return json;

        int nl = json.IndexOf('\n');
        int end = json.LastIndexOf("```");
        if (nl >= 0 && end > nl)
            return json.Substring(nl + 1, end - nl - 1).Trim();
        return json;
    }

    private EnemyActionData LogAndReturn(EnemyActionData val, string msg)
    {
        Debug.LogWarning(msg);
        return val;
    }
}
