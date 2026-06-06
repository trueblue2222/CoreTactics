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
        Vector3 worldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(cellPos);
        worldPos.z = 0;

        // 💡 [추가] 클릭한 위치에 폭탄이 있는지 검사합니다.
        Obstacle clickedBomb = null;
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPos);
        foreach (Collider2D hit in hits)
        {
            Obstacle obs = hit.GetComponent<Obstacle>();
            if (obs != null && obs.obstacleType == Obstacle.ObstacleType.Bomb)
            {
                clickedBomb = obs;
                break;
            }
        }

        if (clickedUnit != null)
        {
            Debug.Log($"대상 유닛 {clickedUnit.unitClass} 선택");
            BattleManager.Instance.skillTargetUnit = clickedUnit;
            BattleManager.Instance.currentState = BattleManager.BattleState.SelectingSkillDestination;
            ShowDestinationTiles(clickedUnit);
        }
        else if (clickedBomb != null)
        {
            BattleManager.Instance.skillTargetObstacle = clickedBomb;
            BattleManager.Instance.skillTargetUnit = null; // 유닛 비우기
        }
        else
        {
            Debug.Log("스킬 대상(유닛 또는 폭탄)이 없습니다.");
            return; // 아무것도 없으면 취소
        }

        Debug.Log("이동시킬 목적지를 클릭하세요.");
        BattleManager.Instance.currentState = BattleManager.BattleState.SelectingSkillDestination;
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

                    // 텔레포트 목적지 검사도 OverlapPointAll 로 교체!
                    Collider2D[] hits = Physics2D.OverlapPointAll(worldPos);
                    bool isPassable = true;

                    foreach (Collider2D hit in hits)
                    {
                        Obstacle obs = hit.GetComponent<Obstacle>();
                        if (obs != null && !obs.IsPassable()) isPassable = false;

                        if (hit.GetComponent<Unit>() != null || hit.GetComponent<Core>() != null)
                        {
                            isPassable = false; // 목적지에 다른 유닛/코어가 있다면 텔레포트 불가
                        }
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
        if (TurnManager.Instance.IsPlayerTurn)
            TurnManager.Instance.ChangeState(GameState.PlayerActionExecute);

        Vector3 targetWorldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(cellPos);
        targetWorldPos.z = 0;

        BattleManager.Instance.ClearHighlights();
        BattleManager.Instance.currentState = BattleManager.BattleState.Idle;

        TriggerSkillAnim();

        skillCooldown = 3; // 마법사 쿨타임

        Unit targetUnit = BattleManager.Instance.skillTargetUnit;
        Obstacle targetBomb = BattleManager.Instance.skillTargetObstacle;

        if (targetUnit != null)
        {
            TeleportTargetUnit(targetUnit, targetWorldPos, () => 
            {
                Debug.Log("마법사 유닛 공간 이동 완료");
                BattleManager.Instance.skillTargetUnit = null;

                if (TurnManager.Instance.IsPlayerTurn)
                    TurnManager.Instance.ChangeState(GameState.PlayerTurnEnd);
            });
        }
        else if (targetBomb != null)
        {
            // 폭탄은 유닛이 아니므로 점액(Slime)이나 가시 함정(Spike)의 효과를 받지 않습니다.
            // 따라서 복잡한 함수를 거칠 필요 없이 위치만 즉시 변경해 주면 완벽합니다!
            targetBomb.transform.position = targetWorldPos;
            
            Debug.Log("마법사 폭탄 텔레포트 완료!");
            BattleManager.Instance.skillTargetObstacle = null;

            if (TurnManager.Instance.IsPlayerTurn)
                TurnManager.Instance.ChangeState(GameState.PlayerTurnEnd);
        }
    }

    private void TeleportTargetUnit(Unit target, Vector3 targetPos, System.Action onComplete)
    {
        // 1. 마법사 본인(transform)이 아니라 대상(target)의 위치를 즉시 이동시킵니다.
        target.transform.position = targetPos;

        // 2. 타겟이 도착한 위치의 바닥에 점액이 있는지 검사합니다.
        Collider2D[] hits = Physics2D.OverlapPointAll(targetPos);
        foreach (Collider2D hit in hits)
        {
            SlimePuddle puddle = hit.GetComponent<SlimePuddle>();
            if (puddle != null) 
            {
                // 나(this)가 아니라, 내가 던진 대상(target)에게 디버프를 걸라고 지시해야 합니다!
                puddle.ApplyDebuff(target); 
            }

            Obstacle obstacle = hit.GetComponent<Obstacle>();
            if (obstacle != null)
            {
                obstacle.OnUnitStepped(target);
            }
        }

        // 3. 콜백(턴 종료 등) 실행
        onComplete?.Invoke();
    }
}