using UnityEngine;

public class Magician : Unit
{
    public override void OnSkillButtonPressed()
    {
        Debug.Log("공간 이동시킬 대상을 클릭하세요.");
        BattleManager.Instance.currentState = BattleManager.BattleState.SelectingSkill;
        BattleManager.Instance.ClearHighlights();
        BattleManager.Instance.validSkillCells.Clear();

        Vector3Int startCell = BattleManager.Instance.gridTilemap.WorldToCell(transform.position);
        int range = 4;

        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) <= range)
                {
                    Vector3Int targetCell = startCell + new Vector3Int(x, y, 0);
                    BattleManager.Instance.validSkillCells.Add(targetCell);
                    BattleManager.Instance.SpawnHighlight(targetCell);
                }
            }
        }
    }

    public override void OnSkillTargetClicked(Vector3Int cellPos, Unit clickedUnit, Core clickedCore)
    {
        if (clickedUnit != null)
        {
            Debug.Log($"대상 유닛 {clickedUnit.unitClass} 선택");
            BattleManager.Instance.skillTargetUnit = clickedUnit;
            BattleManager.Instance.currentState = BattleManager.BattleState.SelectingSkillDestination;
            ShowDestinationTiles(clickedUnit);
        }
    }

    private void ShowDestinationTiles(Unit targetUnit)
    {
        BattleManager.Instance.ClearHighlights();
        BattleManager.Instance.validSkillCells.Clear();

        Vector3Int startCell = BattleManager.Instance.gridTilemap.WorldToCell(targetUnit.transform.position);
        int range = 3;

        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) <= range)
                {
                    Vector3Int targetCell = startCell + new Vector3Int(x, y, 0);
                    if (targetCell == startCell) continue;

                    Vector3 worldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(targetCell);
                    Collider2D hit = Physics2D.OverlapPoint(worldPos);
                    bool isPassable = true;

                    if (hit != null)
                    {
                        Obstacle obs = hit.GetComponent<Obstacle>();
                        if (obs != null && !obs.IsPassable()) isPassable = false;
                        if (hit.GetComponent<Unit>() != null || hit.GetComponent<Core>() != null) isPassable = false;
                    }

                    if (isPassable)
                    {
                        BattleManager.Instance.validSkillCells.Add(targetCell);
                        BattleManager.Instance.SpawnHighlight(targetCell);
                    }
                }
            }
        }
    }

    public override void OnSkillDestinationClicked(Vector3Int cellPos)
    {
        TurnManager.Instance.ChangeState(GameState.PlayerActionExecute);

        Vector3 targetWorldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(cellPos);
        targetWorldPos.z = 0;

        BattleManager.Instance.ClearHighlights();
        BattleManager.Instance.currentState = BattleManager.BattleState.Idle;

        skillCooldown = 3; // 마법사 쿨타임

        StartCoroutine(BattleManager.Instance.skillTargetUnit.MoveSmoothly(targetWorldPos, () =>
        {
            Debug.Log("마법사 공간 이동 스킬 완료");
            BattleManager.Instance.skillTargetUnit = null;
            TurnManager.Instance.ChangeState(GameState.PlayerTurnEnd);
        }));
    }
}