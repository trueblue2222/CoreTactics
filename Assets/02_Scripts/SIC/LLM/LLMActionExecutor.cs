using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// LLMActionParser가 검증한 EnemyActionData 목록을 순서대로 실행합니다.
public class LLMActionExecutor : MonoBehaviour
{
    public static LLMActionExecutor Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // 모든 적 유닛 행동을 순서대로 실행
    public IEnumerator ExecuteActions(List<EnemyActionData> actions)
    {
        foreach (EnemyActionData action in actions)
            yield return StartCoroutine(ExecuteAction(action));
    }

    public IEnumerator ExecuteAction(EnemyActionData action)
    {
        Unit unit = GameStateSerializer.Instance.FindUnitById(action.unitId);
        if (unit == null)
        {
            Debug.LogError($"[LLMExecutor] 유닛을 찾을 수 없음: {action.unitId}");
            yield break;
        }

        Debug.Log($"[LLMExecutor] '{action.unitId}' → {action.actionType}");

        switch (action.actionType)
        {
            case "move":   yield return StartCoroutine(ExecuteMove(unit, action));   break;
            case "attack": yield return StartCoroutine(ExecuteAttack(unit, action)); break;
            case "skill":  yield return StartCoroutine(ExecuteSkill(unit, action));  break;
            case "skill2": yield return StartCoroutine(ExecuteSkill2(unit, action)); break;
            case "skip":
                Debug.Log($"[LLMExecutor] {unit.unitClass} 행동 스킵");
                yield return new WaitForSeconds(0.3f);
                break;
        }
    }

    // ─── 이동 ──────────────────────────────────────────────────────────
    private IEnumerator ExecuteMove(Unit unit, EnemyActionData action)
    {
        Vector3Int targetCell = new Vector3Int(action.moveTarget.x, action.moveTarget.y, 0);
        Vector3 targetWorld = BattleManager.Instance.gridTilemap.GetCellCenterWorld(targetCell);
        targetWorld.z = 0;

        bool done = false;
        StartCoroutine(unit.MoveSmoothly(targetWorld, () => done = true));

        float timeout = 3f;
        while (!done && timeout > 0) { timeout -= Time.deltaTime; yield return null; }
        yield return new WaitForSeconds(0.2f);
    }

    // ─── 공격 ──────────────────────────────────────────────────────────
    private IEnumerator ExecuteAttack(Unit unit, EnemyActionData action)
    {
        unit.TriggerAttackAnim();
        Unit targetUnit = GameStateSerializer.Instance.FindUnitById(action.attackTargetId);
        if (targetUnit != null)
        {
            Debug.Log($"[LLMExecutor] {unit.unitClass}가 {targetUnit.unitClass} 공격");
            targetUnit.TakeDamage(unit.atk);
        }
        else
        {
            Core targetCore = GameStateSerializer.Instance.FindCoreById(action.attackTargetId);
            if (targetCore != null)
            {
                Debug.Log($"[LLMExecutor] {unit.unitClass}가 {targetCore.team} 코어 공격");
                targetCore.TakeDamage(unit.atk);
            }
        }
        yield return new WaitForSeconds(0.5f);
    }

    // ─── 1스킬 ─────────────────────────────────────────────────────────
    private IEnumerator ExecuteSkill(Unit unit, EnemyActionData action)
    {
        switch (unit.unitClass)
        {
            case Unit.UnitClass.Warrior:  yield return StartCoroutine(ExecuteWarriorDash(unit, action));    break;
            case Unit.UnitClass.Archer:   yield return StartCoroutine(ExecuteArcherSniper(unit));           break;
            case Unit.UnitClass.Magician: yield return StartCoroutine(ExecuteMagicianTeleport(unit, action)); break;
        }
    }

    // ─── 2스킬 ─────────────────────────────────────────────────────────
    private IEnumerator ExecuteSkill2(Unit unit, EnemyActionData action)
    {
        switch (unit.unitClass)
        {
            case Unit.UnitClass.Warrior:  yield return StartCoroutine(ExecuteWarriorAoE(unit));              break;
            case Unit.UnitClass.Archer:   yield return StartCoroutine(ExecuteArcherArrowShower(unit, action)); break;
            case Unit.UnitClass.Magician: yield return StartCoroutine(ExecuteMagicianLightning(unit, action)); break;
        }
    }

    // Warrior 1스킬: 돌진
    private IEnumerator ExecuteWarriorDash(Unit unit, EnemyActionData action)
    {
        Vector3Int dashCell = new Vector3Int(action.dashDestination.x, action.dashDestination.y, 0);
        Vector3 dashWorld = BattleManager.Instance.gridTilemap.GetCellCenterWorld(dashCell);
        dashWorld.z = 0;

        unit.OnSkillTargetClicked(dashCell, null, null);

        float timeout = 3f;
        while (Vector3.Distance(unit.transform.position, dashWorld) > 0.01f && timeout > 0)
        { timeout -= Time.deltaTime; yield return null; }
        yield return new WaitForSeconds(0.3f);
    }

    // Archer 1스킬: 저격 모드
    private IEnumerator ExecuteArcherSniper(Unit unit)
    {
        unit.OnSkillButtonPressed();
        yield return new WaitForSeconds(0.3f);
    }

    // Magician 1스킬: 텔레포트
    private IEnumerator ExecuteMagicianTeleport(Unit unit, EnemyActionData action)
    {
        Unit teleTarget = GameStateSerializer.Instance.FindUnitById(action.skillTargetId);
        if (teleTarget == null)
        {
            Debug.LogWarning("[LLMExecutor] Magician skill: 텔레포트 대상 없음");
            yield break;
        }

        Vector3Int destCell = new Vector3Int(action.skillDestination.x, action.skillDestination.y, 0);
        Vector3 destWorld = BattleManager.Instance.gridTilemap.GetCellCenterWorld(destCell);
        destWorld.z = 0;

        Debug.Log($"[LLMExecutor] 마법사 텔레포트: {teleTarget.unitClass} → {destCell}");
        unit.skillCooldown = 3;

        bool done = false;
        StartCoroutine(teleTarget.MoveSmoothly(destWorld, () => done = true));

        float timeout = 3f;
        while (!done && timeout > 0) { timeout -= Time.deltaTime; yield return null; }
        yield return new WaitForSeconds(0.3f);
    }

    // Warrior 2스킬: 대검 소환 AoE
    private IEnumerator ExecuteWarriorAoE(Unit unit)
    {
        Debug.Log($"[LLMExecutor] {unit.unitClass} 2스킬(대검 소환) 발동");
        unit.OnSecondSkillButtonPressed();
        yield return new WaitForSeconds(0.5f);
    }

    // Archer 2스킬: 화살비 (세로 3칸 범위)
    private IEnumerator ExecuteArcherArrowShower(Unit unit, EnemyActionData action)
    {
        Vector3Int targetCell = new Vector3Int(action.skill2Target.x, action.skill2Target.y, 0);
        Vector3 worldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(targetCell);
        worldPos.z = 0;

        // 타겟 셀의 유닛/코어 검색
        Unit clickedUnit = null;
        Core clickedCore = null;
        foreach (Collider2D hit in Physics2D.OverlapPointAll(worldPos))
        {
            if (clickedUnit == null) clickedUnit = hit.GetComponent<Unit>();
            if (clickedCore == null) clickedCore = hit.GetComponent<Core>();
        }

        Debug.Log($"[LLMExecutor] {unit.unitClass} 2스킬(화살비) → {targetCell}");
        unit.OnSecondSkillButtonPressed();
        unit.OnSkillTargetClicked(targetCell, clickedUnit, clickedCore);
        yield return new WaitForSeconds(0.5f);
    }

    // Magician 2스킬: 번개 (십자 5칸)
    private IEnumerator ExecuteMagicianLightning(Unit unit, EnemyActionData action)
    {
        Vector3Int targetCell = new Vector3Int(action.skill2Target.x, action.skill2Target.y, 0);
        Vector3 worldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(targetCell);
        worldPos.z = 0;

        Unit clickedUnit = null;
        Core clickedCore = null;
        foreach (Collider2D hit in Physics2D.OverlapPointAll(worldPos))
        {
            if (clickedUnit == null) clickedUnit = hit.GetComponent<Unit>();
            if (clickedCore == null) clickedCore = hit.GetComponent<Core>();
        }

        Debug.Log($"[LLMExecutor] {unit.unitClass} 2스킬(번개) → {targetCell}");
        unit.OnSecondSkillButtonPressed();
        unit.OnSkillTargetClicked(targetCell, clickedUnit, clickedCore);
        yield return new WaitForSeconds(0.5f);
    }
}
