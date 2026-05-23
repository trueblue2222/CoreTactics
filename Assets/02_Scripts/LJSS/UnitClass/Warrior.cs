using UnityEngine;

public class Warrior : Unit
{
    public override void OnSkillButtonPressed()
    {
        Debug.Log("돌진할 방향의 타일을 클릭하세요.");
        BattleManager.Instance.currentState = BattleManager.BattleState.SelectingSkill;
        BattleManager.Instance.ClearHighlights();

        Vector3Int startCell = BattleManager.Instance.gridTilemap.WorldToCell(transform.position);
        Vector3Int[] directions = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };

        foreach (Vector3Int dir in directions)
        {
            for (int i = 1; i <= 4; i++)
            {
                Vector3Int nextCell = startCell + dir * i;
                Vector3 nextWorldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(nextCell);

                Collider2D hit = Physics2D.OverlapPoint(nextWorldPos);
                if (hit != null)
                {
                    Obstacle obstacle = hit.GetComponent<Obstacle>();
                    if (obstacle != null && !obstacle.IsPassable()) break;
                }

                BattleManager.Instance.validSkillCells.Add(nextCell);
                BattleManager.Instance.SpawnHighlight(nextCell);
            }
        }
    }

    public override void OnSkillTargetClicked(Vector3Int cellPos, Unit clickedUnit, Core clickedCore)
    {
        TurnManager.Instance.ChangeState(GameState.PlayerActionExecute);
        Vector3Int startCell = BattleManager.Instance.gridTilemap.WorldToCell(transform.position);

        Vector3Int dir = new Vector3Int(
            Mathf.Clamp((cellPos.x - startCell.x), -1, 1),
            Mathf.Clamp((cellPos.y - startCell.y), -1, 1),
            0
        );
        int dist = (int)Mathf.Max(Mathf.Abs(cellPos.x - startCell.x), Mathf.Abs(cellPos.y - startCell.y));

        for (int i = 1; i <= dist; i++)
        {
            Vector3Int pathCell = new Vector3Int(startCell.x + (dir.x * i), startCell.y + (dir.y * i), 0);
            Vector3 pathWorldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(pathCell);

            Collider2D hitTarget = Physics2D.OverlapPoint(pathWorldPos);
            if (hitTarget != null)
            {
                Unit targetUnit = hitTarget.GetComponent<Unit>();
                if (targetUnit != null && targetUnit.team != team) targetUnit.TakeDamage(20);

                Core targetCore = hitTarget.GetComponent<Core>();
                if (targetCore != null && targetCore.team != team) targetCore.TakeDamage(20);
            }
        }

        BattleManager.Instance.ClearHighlights();
        BattleManager.Instance.currentState = BattleManager.BattleState.Idle;

        Vector3 targetWorldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(cellPos);
        targetWorldPos.z = 0;

        StartCoroutine(MoveSmoothly(targetWorldPos, () =>
        {
            Debug.Log("돌진 스킬 완료");
            skillCooldown = 2; // 전사 쿨타임
            TurnManager.Instance.ChangeState(GameState.PlayerTurnEnd);
        }));
    }
}
