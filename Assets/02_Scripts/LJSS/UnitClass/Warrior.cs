using UnityEngine;

public class Warrior : Unit
{
    [Header("Dash Skill")]
    public float dashSpeed = 10f;

    public override void OnSkillButtonPressed()
    {
        if (rootedTurns > 0)
        {
            Debug.Log("점액으로 인해 돌진 스킬 사용 불가");
            return;
        }
        
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

            Collider2D[] hitsTarget = Physics2D.OverlapPointAll(pathWorldPos);
            
            // 💡 [핵심 추가] 데미지를 주기 전에, 이 칸에 바리케이드가 있는지 먼저 확인합니다.
            bool hitBarricade = false;
            foreach (Collider2D hitTarget in hitsTarget)
            {
                Obstacle obs = hitTarget.GetComponent<Obstacle>();
                if (obs != null && !obs.IsPassable())
                {
                    hitBarricade = true;
                    break;
                }
            }

            // 🛑 바리케이드가 막고 있다면? 즉시 for문을 탈출(break)하여 뒤쪽 경로의 적을 보호합니다!
            if (hitBarricade)
            {
                Debug.Log("경로 상에 바리케이드가 있어 그 너머로는 데미지가 들어가지 않습니다!");
                break; 
            }

            // 바리케이드가 없는 안전한 칸이라면 정상적으로 데미지를 줍니다.
            foreach (Collider2D hitTarget in hitsTarget)
            {
                Unit targetUnit = hitTarget.GetComponent<Unit>();
                if (targetUnit != null && targetUnit.team != team) targetUnit.TakeDamage(20);

                Core targetCore = hitTarget.GetComponent<Core>();
                if (targetCore != null && targetCore.team != team) targetCore.TakeDamage(20);

                Obstacle targetObs = hitTarget.GetComponent<Obstacle>();
                if (targetObs != null && targetObs.obstacleType == Obstacle.ObstacleType.Bomb) 
                {
                    targetObs.TriggerBomb();
                }
            }

            
        }

        BattleManager.Instance.ClearHighlights();
        BattleManager.Instance.currentState = BattleManager.BattleState.Idle;

        Vector3 targetWorldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(cellPos);
        targetWorldPos.z = 0;

        TriggerSkillAnim();

        StartCoroutine(MoveSmoothly(targetWorldPos, dashSpeed, () =>
        {
            Debug.Log("전사 돌진 이동 완료!");

            // 💡 도착 후 쿨타임 적용 및 턴 종료만 깔끔하게 실행합니다.
            skillCooldown = 2; // 전사 쿨타임

            if (TurnManager.Instance.IsPlayerTurn)
            {
                TurnManager.Instance.ChangeState(GameState.PlayerTurnEnd);
            }
        }));
    }
}
