using System.Collections;
using UnityEngine;

// LLMActionParser가 검증한 EnemyActionData를 실제 게임 행동으로 실행합니다.
public class LLMActionExecutor : MonoBehaviour
{
    public static LLMActionExecutor Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
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
            case "skip":
                Debug.Log($"[LLMExecutor] {unit.unitClass} 행동 스킵");
                yield return new WaitForSeconds(0.5f);
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

    // ─── 스킬 ──────────────────────────────────────────────────────────
    private IEnumerator ExecuteSkill(Unit unit, EnemyActionData action)
    {
        switch (unit.unitClass)
        {
            case Unit.UnitClass.Warrior:
                yield return StartCoroutine(ExecuteWarriorDash(unit, action));
                break;
            case Unit.UnitClass.Archer:
                yield return StartCoroutine(ExecuteArcherSniper(unit));
                break;
            case Unit.UnitClass.Magician:
                yield return StartCoroutine(ExecuteMagicianTeleport(unit, action));
                break;
        }
    }

    // Warrior 돌진: EnemyAIManager와 동일하게 OnSkillTargetClicked 직접 호출
    // Warrior.OnSkillTargetClicked는 IsPlayerTurn 가드로 적 턴에 안전합니다.
    private IEnumerator ExecuteWarriorDash(Unit unit, EnemyActionData action)
    {
        Vector3Int dashCell = new Vector3Int(action.dashDestination.x, action.dashDestination.y, 0);
        Vector3 dashWorld = BattleManager.Instance.gridTilemap.GetCellCenterWorld(dashCell);
        dashWorld.z = 0;

        unit.OnSkillTargetClicked(dashCell, null, null);

        float timeout = 3f;
        while (Vector3.Distance(unit.transform.position, dashWorld) > 0.01f && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        yield return new WaitForSeconds(0.3f);
    }

    // Archer 저격 모드: OnSkillButtonPressed 직접 호출.
    // IsPlayerTurn 가드 덕분에 적 턴에서도 필드만 바꾸고 StateChange는 발생하지 않습니다.
    private IEnumerator ExecuteArcherSniper(Unit unit)
    {
        unit.OnSkillButtonPressed();
        yield return new WaitForSeconds(0.3f);
    }

    // Magician 텔레포트: 플레이어 입력 없이 직접 이동 처리.
    // OnSkillButtonPressed는 BattleState를 변경하므로 사용하지 않습니다.
    private IEnumerator ExecuteMagicianTeleport(Unit unit, EnemyActionData action)
    {
        Unit teleTarget = GameStateSerializer.Instance.FindUnitById(action.skillTargetId);
        if (teleTarget == null)
        {
            Debug.LogWarning("[LLMExecutor] Magician: 텔레포트 대상 없음");
            yield break;
        }

        Vector3Int destCell = new Vector3Int(action.skillDestination.x, action.skillDestination.y, 0);
        Vector3 destWorld = BattleManager.Instance.gridTilemap.GetCellCenterWorld(destCell);
        destWorld.z = 0;

        Debug.Log($"[LLMExecutor] 마법사: {teleTarget.unitClass} → {destCell} 텔레포트");
        unit.skillCooldown = 3;

        bool done = false;
        StartCoroutine(teleTarget.MoveSmoothly(destWorld, () => done = true));

        float timeout = 3f;
        while (!done && timeout > 0) { timeout -= Time.deltaTime; yield return null; }
        yield return new WaitForSeconds(0.3f);
    }
}
