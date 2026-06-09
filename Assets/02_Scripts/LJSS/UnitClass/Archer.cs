using UnityEngine;

public class Archer : Unit
{
    [Header("Second Skill (Vertical Arrow Shower)")]
    public GameObject arrowEffectPrefab; // 화살 떨어지는 이펙트
    public int arrowDamage = 10;         // 화살비 피해량
    
    private bool isUsingSecondSkill = false;

    // 🎯 1스킬: 저격 모드 (버프)
    public override void OnSkillButtonPressed()
    {
        if (rootedTurns > 0)
        {
            Debug.Log("점액으로 인해 스킬 사용 불가");
            return;
        }

        isUsingSecondSkill = false; // 2스킬 모드 OFF
        isSniperMode = true;
        
        // 💡 [수정됨] 버프 지속 시간을 기존보다 1턴 늘렸습니다. (예: 2 -> 3턴)
        sniperModeTurnsLeft = 3; 
        
        // (기존 스탯 변화 로직 - 게임 밸런스에 맞게 수정해서 쓰세요!)
        attackRange += 1; 
        moveRange = 0;    
        
        // 💡 [핵심 추가] 버프 사용 즉시 자식 오브젝트 이펙트를 켭니다!
        if (buffEffectObj != null) buffEffectObj.SetActive(true);

        Debug.Log($"궁수: 저격 모드 활성화! ({sniperModeTurnsLeft}턴 지속)");

        skillCooldown = 2;

        if (TurnManager.Instance.IsPlayerTurn)
        {
            TurnManager.Instance.ChangeState(GameState.PlayerTurnEnd);
        }
    }

    // 🌧️ 2스킬: 수직 화살비 (광역 공격)
   public override void OnSecondSkillButtonPressed()
    {
        if (rootedTurns > 0)
        {
            Debug.Log("점액으로 인해 스킬 사용 불가");
            return;
        }

        isUsingSecondSkill = true; // 2스킬 모드 ON!
        Debug.Log("궁수: 화살비를 내릴 대상을 선택하세요. (맨해튼 4칸 범위 전체 표시)");

        BattleManager.Instance.currentState = BattleManager.BattleState.SelectingSkill;
        BattleManager.Instance.ClearHighlights();

        Vector3Int centerCell = BattleManager.Instance.gridTilemap.WorldToCell(transform.position);

        // 💡 맨해튼 거리 4칸 스캔
        int range = 4;
        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) <= range)
                {
                    Vector3Int targetCell = centerCell + new Vector3Int(x, y, 0);
                    
                    // 💡 [수정] 유닛 유무와 상관없이 맨해튼 4칸 사거리 전체를 표시합니다.
                    BattleManager.Instance.validSkillCells.Add(targetCell);
                    BattleManager.Instance.SpawnHighlight(targetCell);
                }
            }
        }
    }

    // 🎯 마우스 클릭 시 실행
    public override void OnSkillTargetClicked(Vector3Int cellPos, Unit clickedUnit, Core clickedCore)
    {
        // 2스킬(화살비) 상태일 때만 작동
        if (isUsingSecondSkill)
        {
            bool isValidTarget = false;

            // 💡 [핵심 필터링 로직]
            // 1. 클릭한 대상이 유닛(Unit)이고, 나와 팀이 다를 때(적군) O
            if (clickedUnit != null && clickedUnit.team != this.team) 
            {
                isValidTarget = true;
            }
            // 2. 클릭한 대상이 코어(Core)이고, 나와 팀이 다를 때(적 코어) O
            else if (clickedCore != null && clickedCore.team != this.team) 
            {
                isValidTarget = true;
            }

            // 🛑 아군 유닛, 아군 코어, 빈 땅, 장애물(폭탄/바리케이드)은 위 조건에 맞지 않아 
            // isValidTarget이 무조건 false가 되며 자연스럽게 걸러집니다!

            if (isValidTarget)
            {
                // 올바른 적을 클릭했다면 화살비 발사!
                ExecuteArrowShower(cellPos);
            }
            else
            {
                // 아군이나 장애물을 클릭하면 스킬이 나가지 않고 경고를 띄웁니다.
                Debug.Log("적 유닛 또는 적 코어만 선택할 수 있습니다! (아군 및 장애물 선택 불가)");
            }
        }
    }

    // 🌧️ 화살비 데미지 및 연출 판정 함수
    private void ExecuteArrowShower(Vector3Int targetCellPos)
    {
        TriggerSecondSkillAnim();

        // 💡 대상 타일을 기준으로 세로 1칸(위, 타겟, 아래) 좌표를 모아둡니다.
        Vector3Int[] aoeCells = {
            targetCellPos + Vector3Int.up,      // 위칸
            targetCellPos,                      // 타겟이 있는 중앙칸
            targetCellPos + Vector3Int.down     // 아래칸
        };

        foreach (Vector3Int cell in aoeCells)
        {
            Vector3 worldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(cell);
            worldPos.z = 0;

            // 1. 해당 위치에 화살이 떨어지는 이펙트 소환 (1초 뒤 자동 파괴)
            if (arrowEffectPrefab != null)
            {
                GameObject effect = Instantiate(arrowEffectPrefab, worldPos, Quaternion.identity);
                Destroy(effect, 1f);
            }

            // 2. 데미지 판정 (세로 3칸에 있는 모든 적 타격)
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPos);
            foreach (Collider2D hit in hits)
            {
                Unit unit = hit.GetComponent<Unit>();
                if (unit != null && unit.team != this.team) unit.TakeDamage(arrowDamage);

                Core core = hit.GetComponent<Core>();
                if (core != null && core.team != this.team) core.TakeDamage(arrowDamage);

                // 화살이 꽂혀도 폭탄은 터집니다!
                Obstacle obs = hit.GetComponent<Obstacle>();
                if (obs != null && obs.obstacleType == Obstacle.ObstacleType.Bomb) obs.TriggerBomb();
            }
        }

        // 스킬 종료 뒷정리
        BattleManager.Instance.ClearHighlights();
        BattleManager.Instance.currentState = BattleManager.BattleState.Idle;
        isUsingSecondSkill = false;
        skillCooldown = 3;

        if (TurnManager.Instance.IsPlayerTurn)
        {
            TurnManager.Instance.ChangeState(GameState.PlayerTurnEnd);
        }
    }

    public override void TriggerSecondSkillAnim()
    {
        if (anim != null) anim.SetTrigger("Attack");
    }
}
