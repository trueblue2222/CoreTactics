using UnityEngine;

public class Magician : Unit
{
    [Header("Second Skill (Lightning AoE)")]
    public GameObject lightningEffectPrefab; // 번개 스프라이트 프리팹
    public int lightningDamage = 10;         // 번개 피해량

    private bool isUsingSecondSkill = false;

    public override void OnSkillButtonPressed()
    {
        isUsingSecondSkill = false;

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

    public override void OnSecondSkillButtonPressed()
    {
        if (rootedTurns > 0)
        {
            Debug.Log("점액으로 인해 스킬 사용 불가");
            return;
        }

        isUsingSecondSkill = true;
        Debug.Log("마법사: 번개를 떨어뜨릴 대상을 선택하세요. (2칸 이내 범위 전체 표시)");

        BattleManager.Instance.currentState = BattleManager.BattleState.SelectingSkill;
        BattleManager.Instance.ClearHighlights();

        Vector3Int centerCell = BattleManager.Instance.gridTilemap.WorldToCell(transform.position);

        // 💡 가로/세로/대각선 5x5 정사각형 사거리 스캔
        for (int x = -2; x <= 2; x++)
        {
            for (int y = -2; y <= 2; y++)
            {
                Vector3Int targetCell = centerCell + new Vector3Int(x, y, 0);

                // 💡 [수정] 유닛 유무와 상관없이 사거리(25칸) 전체를 클릭 가능한 범위로 보여줍니다.
                BattleManager.Instance.validSkillCells.Add(targetCell);
                BattleManager.Instance.SpawnHighlight(targetCell);
            }
        }
    }

    private void ExecuteLightningSkill(Vector3Int targetCellPos)
    {
        TriggerSecondSkillAnim();

        // 💡 대상 타일과 주변 1칸(맨해튼 거리 = 십자 모양) 좌표를 모아둡니다.
        Vector3Int[] aoeCells = {
            targetCellPos,                      // 중앙 (타겟)
            targetCellPos + Vector3Int.up,      // 위
            targetCellPos + Vector3Int.down,    // 아래
            targetCellPos + Vector3Int.left,    // 왼쪽
            targetCellPos + Vector3Int.right    // 오른쪽
        };

        foreach (Vector3Int cell in aoeCells)
        {
            Vector3 worldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(cell);
            worldPos.z = 0;

            Vector3 effectPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(cell);
            effectPos.z = 0;

            // Y축으로 0.5만큼 올려서 타일의 중심보다 약간 위에서 번개가 치도록 설정합니다.
            effectPos += new Vector3(0, 0.5f, 0);

            // 1. 번개 이펙트 소환 (1초 뒤 자동 파괴)
            if (lightningEffectPrefab != null)
            {
                GameObject effect = Instantiate(lightningEffectPrefab, effectPos, Quaternion.identity);
                Destroy(effect, 1f);
            }

            // 2. 데미지 판정
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPos);
            foreach (Collider2D hit in hits)
            {
                Unit unit = hit.GetComponent<Unit>();
                if (unit != null && unit.team != this.team) unit.TakeDamage(lightningDamage);

                Core core = hit.GetComponent<Core>();
                if (core != null && core.team != this.team) core.TakeDamage(lightningDamage);

                // 번개가 폭탄에 내리치면 폭탄도 기폭됩니다!
                Obstacle obs = hit.GetComponent<Obstacle>();
                if (obs != null && obs.obstacleType == Obstacle.ObstacleType.Bomb) obs.TriggerBomb();
            }
        }

        // 스킬 종료 및 뒷정리
        BattleManager.Instance.ClearHighlights();
        BattleManager.Instance.currentState = BattleManager.BattleState.Idle;
        isUsingSecondSkill = false; // 💡 다음 스킬 사용을 위해 스위치 초기화
        skillCooldown = 3;          // 💡 쿨타임 3턴 적용

        if (TurnManager.Instance.IsPlayerTurn)
        {
            TurnManager.Instance.ChangeState(GameState.PlayerTurnEnd);
        }
    }

    public override void OnSkillTargetClicked(Vector3Int cellPos, Unit clickedUnit, Core clickedCore)
    {
        Vector3 worldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(cellPos);
        worldPos.z = 0;

        // 클릭한 위치에 폭탄이 있는지 공통으로 검사
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

        // ⚡ [2스킬] 번개 마법 모드일 경우
        if (isUsingSecondSkill)
        {
            bool isValidTarget = false;

            Vector3Int centerCellForLightning = cellPos;

            if (clickedUnit != null && clickedUnit.team != this.team)
            {
                isValidTarget = true;
                centerCellForLightning = BattleManager.Instance.gridTilemap.WorldToCell(clickedUnit.transform.position);
            }
            else if (clickedCore != null && clickedCore.team != this.team)
            {
                isValidTarget = true;
                centerCellForLightning = BattleManager.Instance.gridTilemap.WorldToCell(clickedCore.transform.position);
            }
            else if (clickedBomb != null)
            {
                isValidTarget = true;
                centerCellForLightning = BattleManager.Instance.gridTilemap.WorldToCell(clickedBomb.transform.position);
            }

            if (isValidTarget)
            {
                ExecuteLightningSkill(centerCellForLightning);
            }
            else
            {
                Debug.Log("사거리 내의 적 유닛, 적 코어, 또는 폭탄만 선택할 수 있습니다!");
            }
            return; // 2스킬 로직 종료
        }

        if (clickedUnit != null)
        {
            BattleManager.Instance.skillTargetUnit = clickedUnit;
            BattleManager.Instance.skillTargetObstacle = null;
        }
        else if (clickedBomb != null)
        {
            BattleManager.Instance.skillTargetObstacle = clickedBomb;
            BattleManager.Instance.skillTargetUnit = null;
        }
        else
        {
            Debug.Log("스킬 대상(유닛 또는 폭탄)이 없습니다.");
            return;
        }

        Debug.Log("이동시킬 목적지를 클릭하세요.");
        BattleManager.Instance.currentState = BattleManager.BattleState.SelectingSkillDestination;

        ShowDestinationTiles();
    }

    private void ShowDestinationTiles()
    {
        BattleManager.Instance.ClearHighlights();
        BattleManager.Instance.validSkillCells.Clear();

        // 💡 [핵심 추가] 누가 타겟으로 지정되어 있는지 불러옵니다.
        Unit targetUnit = BattleManager.Instance.skillTargetUnit;
        Obstacle targetBomb = BattleManager.Instance.skillTargetObstacle;

        // 타겟의 Transform(위치 정보)을 담을 빈 바구니를 만듭니다.
        Transform targetTransform = null;

        // 유닛이 있다면 유닛의 위치를, 폭탄이 있다면 폭탄의 위치를 바구니에 담습니다.
        if (targetUnit != null) targetTransform = targetUnit.transform;
        else if (targetBomb != null) targetTransform = targetBomb.transform;

        // 혹시라도 둘 다 없으면 안전하게 빠져나갑니다.
        if (targetTransform == null) return;

        // 💡 [수정] 위에서 찾은 타겟의 위치를 기준으로 시작 칸을 정합니다!
        Vector3Int startCell = BattleManager.Instance.gridTilemap.WorldToCell(targetTransform.position);
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

                    Collider2D[] hits = Physics2D.OverlapPointAll(worldPos);
                    bool isPassable = true;

                    foreach (Collider2D hit in hits)
                    {
                        Obstacle obs = hit.GetComponent<Obstacle>();
                        if (obs != null && !obs.IsPassable()) isPassable = false;

                        if (hit.GetComponent<Unit>() != null || hit.GetComponent<Core>() != null)
                        {
                            isPassable = false;
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