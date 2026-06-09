using UnityEngine;

public class Warrior : Unit
{
    [Header("Dash Skill")]
    public float dashSpeed = 10f;

    [Header("Second Skill (AoE)")]
    public GameObject swordEruptionPrefab; // 💡 솟아오르는 대검 스프라이트 프리팹
    public int aoeDamage = 20;             // 광역기 데미지

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

    public override void OnSecondSkillButtonPressed()
    {
        if (rootedTurns > 0)
        {
            Debug.Log("점액으로 인해 스킬 사용 불가");
            return;
        }

        Debug.Log("전사: 주변 1칸 범위에 대검 소환!");
        TriggerSecondSkillAnim(); 
        // 내 유닛이 서 있는 현재 타일 위치
        Vector3Int centerCell = BattleManager.Instance.gridTilemap.WorldToCell(transform.position);

        // x는 -1부터 1까지, y도 -1부터 1까지 반복 (총 9칸 3x3 스캔)
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                
                Vector3Int targetCell = centerCell + new Vector3Int(x, y, 0);
                Vector3 targetWorldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(targetCell);
                targetWorldPos.z = 0;

                // 1. 해당 칸에 대검 이펙트 생성 (이전에 폭탄에서 썼던 1초 뒤 자동 삭제 기법 적용)
                if (swordEruptionPrefab != null)
                {
                    GameObject swordEffect = Instantiate(swordEruptionPrefab, targetWorldPos, Quaternion.identity);
                    Destroy(swordEffect, 1f); 
                }

                // 2. 해당 칸에 있는 대상 타격 판정
                Collider2D[] hits = Physics2D.OverlapPointAll(targetWorldPos);
                foreach (Collider2D hit in hits)
                {
                    Unit targetUnit = hit.GetComponent<Unit>();
                    
                    // 본인(this)은 맞지 않게 제외하고, 적군일 때만 데미지를 입힙니다.
                    if (targetUnit != null && targetUnit != this && targetUnit.team != this.team)
                    {
                        targetUnit.TakeDamage(aoeDamage);
                    }

                    Core targetCore = hit.GetComponent<Core>();
                    if (targetCore != null && targetCore.team != this.team)
                    {
                        targetCore.TakeDamage(aoeDamage);
                    }

                    // (선택) 주변에 폭탄이 있다면 덤으로 기폭시킵니다!
                    Obstacle targetObs = hit.GetComponent<Obstacle>();
                    if (targetObs != null && targetObs.obstacleType == Obstacle.ObstacleType.Bomb)
                    {
                        targetObs.TriggerBomb();
                    }
                }
            }
        }

        // 스킬 쿨타임 및 턴 종료 처리 (광역기는 성능이 좋으니 쿨타임을 3으로 예시 설정했습니다)
        skillCooldown = 3; 

        if (TurnManager.Instance.IsPlayerTurn)
        {
            TurnManager.Instance.ChangeState(GameState.PlayerTurnEnd);
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
