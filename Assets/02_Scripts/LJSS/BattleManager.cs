using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BattleManager : MonoBehaviour
{
   public enum BattleState
    {
        Idle,
        SelectingMove,
        SelectingAttack,
        SelectingSKill
    }

    [Header("System")]
    public Tilemap gridTilemap;
    public BattleState currentState = BattleState.Idle;

    [Header("Units")]
    public Unit activeUnit;
    public Unit inspectedUnit;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleMouseClick();
        }
    }

    void HandleMouseClick()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);
        Unit clickedUnit = hit.collider != null ? hit.collider.GetComponent<Unit>() : null;
        Vector3Int cellPos = gridTilemap.WorldToCell(mousePos);

        switch (currentState)
        {
            case BattleState.Idle:
                if (clickedUnit != null)
                {
                    inspectedUnit = clickedUnit;
                    Debug.Log($"{inspectedUnit.unitClass}의 상태 확인");
                }
                break;
            case BattleState.SelectingMove:
                if (clickedUnit == null)
                {
                    Vector3 centerPos = gridTilemap.GetCellCenterWorld(cellPos);
                    centerPos.z = 0;
                    activeUnit.transform.position = centerPos;
                    Debug.Log($"[이동] {activeUnit.unitClass}가 {cellPos}로 이동");

                    currentState = BattleState.Idle;
                }
                break;
            case BattleState.SelectingAttack:
                if (clickedUnit != null && clickedUnit != activeUnit)
                {
                    Debug.Log($"{activeUnit.unitClass}가 {clickedUnit.unitClass}를 공격");
                    currentState = BattleState.Idle;
                }
                break;
            case BattleState.SelectingSKill:
                if (clickedUnit != null)
                {
                    Debug.Log($"{activeUnit.unitClass}가 {clickedUnit.unitClass}에게 스킬 사용");
                    activeUnit.UseSkill();
                    currentState = BattleState.Idle;
                }
                break;
        }
    }

    public void OnMoveButtonClicked()
    {
        if (activeUnit == null) return;
        Debug.Log("이동할 타일을 클릭하세요");
        currentState = BattleState.SelectingMove;
    }

    public void OnAttackButtonClicked()
    {
        if (activeUnit == null) return;
        Debug.Log("공격할 대상을 클릭하세요");
        currentState = BattleState.SelectingAttack;
    }

    public void OnSkillButtonClicked()
    {
        if (activeUnit == null) return;
        Debug.Log("스킬을 사용할 대상을 클릭하세요");
        currentState = BattleState.SelectingSKill;
    }
}
