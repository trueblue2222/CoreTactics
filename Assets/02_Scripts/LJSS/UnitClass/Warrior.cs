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

                // 💡 OverlapPointAll 로 교체
                Collider2D[] hits = Physics2D.OverlapPointAll(nextWorldPos);
                bool canLand = true;

                foreach (Collider2D hit in hits)
                {
                    Obstacle obstacle = hit.GetComponent<Obstacle>();
                    if (obstacle != null && !obstacle.IsPassable())
                    {
                        canLand = false;
                        break; // 벽을 만나면 더 이상 전진 방향 하이라이트 생성 중단
                    }

                    if (hit.GetComponent<Unit>() != null || hit.GetComponent<Core>() != null)
                    {
                        canLand = false; // 유닛이나 코어가 서 있으면 그 칸을 착지 목적지로 클릭할 수는 없음
                    }
                }

                if (canLand)
                {
                    BattleManager.Instance.validSkillCells.Add(nextCell);
                    BattleManager.Instance.SpawnHighlight(nextCell);
                }
            }
        }
    }

    public override void OnSkillTargetClicked(Vector3Int cellPos, Unit clickedUnit, Core clickedCore)
    {
        if (TurnManager.Instance.IsPlayerTurn)
        {
            TurnManager.Instance.ChangeState(GameState.PlayerActionExecute);
        }
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

            // 경로상에 있는 모든 유닛/코어에게 대미지를 주기 위해 OverlapPointAll 사용
            Collider2D[] hitsTarget = Physics2D.OverlapPointAll(pathWorldPos);

            foreach (Collider2D hitTarget in hitsTarget)
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

            if (TurnManager.Instance.IsPlayerTurn)
            {
                TurnManager.Instance.ChangeState(GameState.PlayerTurnEnd);
            }
        }));
    }
}
