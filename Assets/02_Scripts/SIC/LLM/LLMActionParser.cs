using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// LLM의 JSON 응답을 파싱하고 유효성을 검증합니다.
// LLM은 적 유닛 전체의 행동을 JSON 배열로 반환합니다.
// 검증 실패 시 null 반환 → TurnManager가 Fallback AI로 전환합니다.
public class LLMActionParser : MonoBehaviour
{
    public static LLMActionParser Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public List<EnemyActionData> ParseAndValidate(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[LLMParser] 빈 응답");
            return null;
        }

        json = StripMarkdownFence(json);

        JToken parsed;
        try { parsed = JToken.Parse(json); }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LLMParser] JSON 파싱 실패: {e.Message}\n원본: {json}");
            return null;
        }

        var rawActions = new List<JObject>();
        if (parsed is JArray arr)
        {
            foreach (JToken item in arr)
                if (item is JObject obj) rawActions.Add(obj);
        }
        else if (parsed is JObject single)
        {
            // LLM이 단일 오브젝트로 응답한 경우 폴백
            rawActions.Add(single);
        }
        else
        {
            Debug.LogWarning("[LLMParser] JSON이 배열 또는 오브젝트가 아님");
            return null;
        }

        if (rawActions.Count == 0)
        {
            Debug.LogWarning("[LLMParser] 파싱된 행동이 없음");
            return null;
        }

        var validatedActions = new List<EnemyActionData>();
        foreach (JObject item in rawActions)
        {
            EnemyActionData action;
            try { action = item.ToObject<EnemyActionData>(); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LLMParser] 행동 역직렬화 실패: {e.Message}");
                continue;
            }

            if (action == null || string.IsNullOrEmpty(action.unitId) || string.IsNullOrEmpty(action.actionType))
            {
                Debug.LogWarning("[LLMParser] unitId 또는 actionType 누락 — 스킵");
                continue;
            }

            Unit unit = GameStateSerializer.Instance.FindUnitById(action.unitId);
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.currentHp <= 0 || unit.team != "Enemy")
            {
                Debug.LogWarning($"[LLMParser] 유효하지 않은 unitId: '{action.unitId}' — 스킵");
                continue;
            }

            action.actionType = action.actionType.ToLower().Trim();

            EnemyActionData validated = action.actionType switch
            {
                "move"   => ValidateMove(action, unit),
                "attack" => ValidateAttack(action, unit),
                "skill"  => ValidateSkill(action, unit),
                "skill2" => ValidateSkill2(action, unit),
                "skip"   => action,
                _ => LogAndSkip($"[LLMParser] 알 수 없는 actionType: '{action.actionType}'")
            };

            if (validated != null)
                validatedActions.Add(validated);
            else
            {
                // 검증 실패한 유닛은 skip으로 대체
                validatedActions.Add(new EnemyActionData { unitId = action.unitId, actionType = "skip" });
            }
        }

        return validatedActions.Count > 0 ? validatedActions : null;
    }

    // ─── 이동 검증 ──────────────────────────────────────────────────────
    private EnemyActionData ValidateMove(EnemyActionData action, Unit unit)
    {
        if (unit.rootedTurns > 0)
            return LogAndSkip($"[LLMParser] move: 점액 구속 중 (rootedTurns={unit.rootedTurns}) — skip으로 대체");

        if (action.moveTarget == null)
            return LogAndSkip("[LLMParser] move: moveTarget 누락");

        Vector3Int unitCell = BattleManager.Instance.gridTilemap.WorldToCell(unit.transform.position);
        int dist = Mathf.Abs(action.moveTarget.x - unitCell.x) + Mathf.Abs(action.moveTarget.y - unitCell.y);

        if (dist == 0) { action.actionType = "skip"; return action; }

        if (dist > unit.moveRange)
            return LogAndSkip($"[LLMParser] move: 이동 범위 초과 (요청 {dist}, 최대 {unit.moveRange})");

        return action;
    }

    // ─── 공격 검증 ──────────────────────────────────────────────────────
    private EnemyActionData ValidateAttack(EnemyActionData action, Unit unit)
    {
        if (string.IsNullOrEmpty(action.attackTargetId))
            return LogAndSkip("[LLMParser] attack: attackTargetId 누락");

        Transform targetTf = GetTargetTransform(action.attackTargetId);
        if (targetTf == null)
            return LogAndSkip($"[LLMParser] attack: 타겟 없음 '{action.attackTargetId}'");

        Vector3Int unitCell   = BattleManager.Instance.gridTilemap.WorldToCell(unit.transform.position);
        Vector3Int targetCell = BattleManager.Instance.gridTilemap.WorldToCell(targetTf.position);
        int dist = Mathf.Abs(targetCell.x - unitCell.x) + Mathf.Abs(targetCell.y - unitCell.y);

        if (dist > unit.attackRange)
            return LogAndSkip($"[LLMParser] attack: 공격 범위 초과 (거리 {dist}, 사거리 {unit.attackRange})");

        return action;
    }

    // ─── 1스킬 검증 ─────────────────────────────────────────────────────
    private EnemyActionData ValidateSkill(EnemyActionData action, Unit unit)
    {
        if (unit.skillCooldown > 0)
            return LogAndSkip($"[LLMParser] skill: 쿨타임 {unit.skillCooldown}턴 남음");

        switch (unit.unitClass)
        {
            case Unit.UnitClass.Warrior:
                if (unit.rootedTurns > 0)
                    return LogAndSkip("[LLMParser] Warrior skill: 점액 구속 중 — 돌진 불가");
                if (action.dashDestination == null)
                    return LogAndSkip("[LLMParser] Warrior skill: dashDestination 누락");
                Vector3Int wCell = BattleManager.Instance.gridTilemap.WorldToCell(unit.transform.position);
                bool sameAxis = (action.dashDestination.x == wCell.x) || (action.dashDestination.y == wCell.y);
                if (!sameAxis)
                    return LogAndSkip("[LLMParser] Warrior skill: dashDestination이 같은 행/열이 아님");
                break;

            case Unit.UnitClass.Archer:
                if (unit.isSniperMode)
                    return LogAndSkip("[LLMParser] Archer skill: 이미 저격 모드 중");
                break;

            case Unit.UnitClass.Magician:
                if (string.IsNullOrEmpty(action.skillTargetId) || action.skillDestination == null)
                    return LogAndSkip("[LLMParser] Magician skill: skillTargetId 또는 skillDestination 누락");
                Unit teleTarget = GameStateSerializer.Instance.FindUnitById(action.skillTargetId);
                if (teleTarget == null || !teleTarget.gameObject.activeInHierarchy || teleTarget.currentHp <= 0)
                    return LogAndSkip($"[LLMParser] Magician skill: 유효하지 않은 skillTargetId '{action.skillTargetId}'");
                break;
        }

        return action;
    }

    // ─── 2스킬 검증 ─────────────────────────────────────────────────────
    private EnemyActionData ValidateSkill2(EnemyActionData action, Unit unit)
    {
        if (unit.skillCooldown > 0)
            return LogAndSkip($"[LLMParser] skill2: 쿨타임 {unit.skillCooldown}턴 남음");

        Vector3Int unitCell = BattleManager.Instance.gridTilemap.WorldToCell(unit.transform.position);

        switch (unit.unitClass)
        {
            case Unit.UnitClass.Warrior:
                // AoE는 타겟 없이 즉시 발동
                break;

            case Unit.UnitClass.Archer:
                if (action.skill2Target == null)
                    return LogAndSkip("[LLMParser] Archer skill2: skill2Target 누락");
                int archerDist = Mathf.Abs(action.skill2Target.x - unitCell.x)
                               + Mathf.Abs(action.skill2Target.y - unitCell.y);
                if (archerDist > 4)
                    return LogAndSkip($"[LLMParser] Archer skill2: 범위 초과 (맨해튼 {archerDist}, 최대 4)");
                break;

            case Unit.UnitClass.Magician:
                if (action.skill2Target == null)
                    return LogAndSkip("[LLMParser] Magician skill2: skill2Target 누락");
                int dx = Mathf.Abs(action.skill2Target.x - unitCell.x);
                int dy = Mathf.Abs(action.skill2Target.y - unitCell.y);
                if (dx > 2 || dy > 2)
                    return LogAndSkip($"[LLMParser] Magician skill2: 범위 초과 (dx={dx}, dy={dy}, 최대 2)");
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

    private string StripMarkdownFence(string json)
    {
        json = json.Trim();
        if (!json.StartsWith("```")) return json;
        int nl = json.IndexOf('\n');
        int end = json.LastIndexOf("```");
        if (nl >= 0 && end > nl) return json.Substring(nl + 1, end - nl - 1).Trim();
        return json;
    }

    private EnemyActionData LogAndSkip(string msg)
    {
        Debug.LogWarning(msg);
        return null;
    }
}
