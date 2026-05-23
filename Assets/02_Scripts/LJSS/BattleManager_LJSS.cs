using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;

public class BattleManager : MonoBehaviour
{
    public enum BattleState
    {
        Idle,
        SelectingMove,
        SelectingAttack,
        SelectingSkill,
        SelectingSkillDestination // 마법사용 스킬 타겟
    }

    [Header("System")]
    public Tilemap gridTilemap;
    public BattleState currentState = BattleState.Idle;
    [SerializeField] private string playerTeamName = "Player";

    [Header("Units")]
    public Unit activeUnit;
    public Unit inspectedUnit;
    public Unit skillTargetUnit;

    [Header("Movement Highlights")]
    public GameObject moveHighlightPrefab;
    private List<GameObject> activeHighlights = new List<GameObject>();
    private List<Vector3Int> validMoveCells = new List<Vector3Int>();

    [Header("Combat Highlights")]
    public GameObject attackHighlightPrefab;
    private List<Vector3Int> validAttackCells = new List<Vector3Int>();

    [Header("Skill Highlights")]
    private List<Vector3Int> validSkillCells = new List<Vector3Int>();

    void Start()
    {
        TurnManager.Instance.OnStateChanged += OnTurnStateChanged;
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current.IsPointerOverGameObject()) return;

        if (TurnManager.Instance.CurrentState == GameState.PlayerTurnEnd)
        {
            UIManager.Instance.ShowTurnEndNotice();
            return;
        }

        if (!TurnManager.Instance.IsInputBlocked)
            HandleMouseClick();
    }

    void OnDestroy()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnStateChanged -= OnTurnStateChanged;
        }
    }

    private void OnTurnStateChanged(GameState newState)
    {
        if (newState == GameState.EnemyTurnStart)
        {
            Debug.Log("[BattleManager] 적 턴 시작시 플레이어 행동 및 하이라이트 및 선택 상태 초기화");

            ClearHighlights();

            currentState = BattleState.Idle;

            activeUnit = null;
            inspectedUnit = null;
            skillTargetUnit = null;
        }
    }

    void HandleMouseClick()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);
        Unit clickedUnit = hit.collider != null ? hit.collider.GetComponent<Unit>() : null;
        Core clickedCore = hit.collider != null ? hit.collider.GetComponent<Core>() : null;

        Vector3Int cellPos = gridTilemap.WorldToCell(mousePos);

        switch (currentState)
        {
            case BattleState.Idle:
                if (clickedUnit != null)
                {
                    GameState gs = TurnManager.Instance.CurrentState;
                    bool isSelectPhase = gs == GameState.PlayerUnitSelect
                                     || gs == GameState.PlayerActionSelect;

                    if (isSelectPhase && clickedUnit.team == playerTeamName)
                    {
                        activeUnit = clickedUnit;
                        Debug.Log($"[유닛 선택] {activeUnit.unitClass}");
                        TurnManager.Instance.ChangeState(GameState.PlayerActionSelect);
                    }
                    else
                    {
                        inspectedUnit = clickedUnit;
                        Debug.Log($"{inspectedUnit.unitClass}의 상태 확인");
                    }
                }
                break;
            case BattleState.SelectingMove:
                if (validMoveCells.Contains(cellPos))
                {
                    TurnManager.Instance.ChangeState(GameState.PlayerActionExecute);
                    Vector3 centerPos = gridTilemap.GetCellCenterWorld(cellPos);
                    centerPos.z = 0;
                    activeUnit.transform.position = centerPos;
                    Debug.Log($"[이동] {activeUnit.unitClass}가 {cellPos}로 이동");

                    ClearHighlights();
                    currentState = BattleState.Idle;
                    TurnManager.Instance.ChangeState(GameState.PlayerTurnEnd);
                }
                else
                {
                    Debug.Log("이동할 수 없는 범위입니다.");
                }
                break;
            case BattleState.SelectingAttack:
                if (validAttackCells.Contains(cellPos))
                {
                    if (clickedUnit != null && clickedUnit.team != activeUnit.team)
                    {
                        TurnManager.Instance.ChangeState(GameState.PlayerActionExecute);
                        Debug.Log($"{activeUnit.unitClass}가 {clickedUnit.unitClass}를 공격");
                        clickedUnit.TakeDamage(activeUnit.atk);

                        ClearHighlights();
                        currentState = BattleState.Idle;
                        TurnManager.Instance.ChangeState(GameState.PlayerTurnEnd);
                    }
                    else if (clickedCore != null && clickedCore.team != activeUnit.team)
                    {
                        TurnManager.Instance.ChangeState(GameState.PlayerActionExecute);
                        Debug.Log($"[핵 공격] {activeUnit.unitClass}가 상대방 핵을 공격");
                        clickedCore.TakeDamage(activeUnit.atk);

                        ClearHighlights();
                        currentState = BattleState.Idle;
                        TurnManager.Instance.ChangeState(GameState.PlayerTurnEnd);
                    }
                }
                else
                {
                    Debug.Log("공격 범위 밖입니다.");
                }
                break;
            case BattleState.SelectingSkill:
                if (validSkillCells.Contains(cellPos))
                {
                    if (activeUnit.unitClass == Unit.UnitClass.Warrior)
                    {
                        TurnManager.Instance.ChangeState(GameState.PlayerActionExecute);
                        Vector3Int startCell = gridTilemap.WorldToCell(activeUnit.transform.position);

                        Vector3Int dir = new Vector3Int(
                            Mathf.Clamp((cellPos.x - startCell.x), -1, 1),
                            Mathf.Clamp((cellPos.y - startCell.y), -1, 1),
                            0
                        );
                        int dist = (int)Mathf.Max(Mathf.Abs(cellPos.x - startCell.x), Mathf.Abs(cellPos.y - startCell.y));

                        for (int i = 1; i <= dist; i++)
                        {
                            Vector3Int pathCell = new Vector3Int(
                                (int)(startCell.x + (dir.x * i)),
                                (int)(startCell.y + (dir.y * i)),
                                0
                            );
                            Vector3 pathWorldPos = gridTilemap.GetCellCenterWorld(pathCell);

                            Collider2D hitTarget = Physics2D.OverlapPoint(pathWorldPos);
                            if (hitTarget != null)
                            {
                                Unit targetUnit = hitTarget.GetComponent<Unit>();
                                if (targetUnit != null && targetUnit.team != activeUnit.team)
                                {
                                    Debug.Log($"[돌진 공격] {targetUnit.unitClass}에게 20 데미지");
                                    targetUnit.TakeDamage(20);
                                }

                                Core targetCore = hitTarget.GetComponent<Core>();
                                if (targetCore != null && targetCore.team != activeUnit.team)
                                {
                                    targetCore.TakeDamage(20);
                                }
                            }
                        }
                        ClearHighlights();
                        currentState = BattleState.Idle;

                        Vector3 targetWorldPos = gridTilemap.GetCellCenterWorld(cellPos);
                        targetWorldPos.z = 0;

                        StartCoroutine(activeUnit.MoveSmoothly(targetWorldPos, () =>
                        {
                            Debug.Log("돌진 스킬 완료");
                            TurnManager.Instance.ChangeState(GameState.PlayerTurnEnd);
                        }));
                    }
                    else if (activeUnit.unitClass == Unit.UnitClass.Magician)
                    {
                        if (clickedUnit != null)
                        {
                            Debug.Log($"대상 유닛 {clickedUnit.unitClass} 선택");
                            skillTargetUnit = clickedUnit;

                            currentState = BattleState.SelectingSkillDestination;
                            ShowMagicianSkillDestinationTiles(skillTargetUnit);
                        }
                        else
                        {
                            Debug.Log("스킬을 적용할 유닛을 선택하세요");
                        }
                    }
                }
                else
                {
                    Debug.Log("스킬을 사용할 수 없는 위치입니다.");
                }
                break;
            case BattleState.SelectingSkillDestination:
                if (validSkillCells.Contains(cellPos))
                {
                    TurnManager.Instance.ChangeState(GameState.PlayerActionExecute);

                    Vector3 targetWorldPos = gridTilemap.GetCellCenterWorld(cellPos);
                    targetWorldPos.z = 0;

                    ClearHighlights();
                    currentState = BattleState.Idle;

                    activeUnit.UseSkill();

                    StartCoroutine(skillTargetUnit.MoveSmoothly(targetWorldPos, () =>
                    {
                        Debug.Log("마법사 스킬 완료");
                        skillTargetUnit = null;
                        TurnManager.Instance.ChangeState(GameState.PlayerTurnEnd);
                        
                    }));
                }
                else
                {
                    Debug.Log("이동할 수 없는 자리입니다");
                }
                break;
        }
    }

    public void OnMoveButtonClicked()
    {
        if (activeUnit == null) return;

        if (activeUnit.isSniperMode)
        {
            Debug.Log("저격 모드 중에는 이동할 수 없습니다");
            return;
        }
        Debug.Log("이동할 타일을 클릭하세요");
        currentState = BattleState.SelectingMove;
        ShowMovableTiles(activeUnit);
    }

    public void OnAttackButtonClicked()
    {
        if (activeUnit == null) return;
        Debug.Log("공격할 대상을 클릭하세요");
        currentState = BattleState.SelectingAttack;
        ShowAttackableTiles(activeUnit);
    }

    void ShowAttackableTiles(Unit unit)
    {
        ClearHighlights();
        validAttackCells.Clear();

        Vector3Int startCell = gridTilemap.WorldToCell(unit.transform.position);
        int range = unit.attackRange;

        if (unit.isSniperMode)
        {
            range += 1;
            Debug.Log("저격 모드 : 공격 범위 +1");
        }

        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) <= range)
                {
                    Vector3Int targetCell = startCell + new Vector3Int(x, y, 0);
                    validAttackCells.Add(targetCell);

                    Vector3 pos = gridTilemap.GetCellCenterWorld(targetCell);
                    pos.z = 0;
                    GameObject highlight = Instantiate(attackHighlightPrefab, pos, Quaternion.identity);
                    activeHighlights.Add(highlight);
                }
            }
        }
    }

    public void OnSkillButtonClicked()
    {
        if (activeUnit == null) return;
        // 1. 스킬 쿨타임이 남았는지 먼저 확인합니다.
        if (activeUnit.skillCooldown > 0)
        {
            Debug.Log($"{activeUnit.unitClass}의 스킬 쿨타임이 {activeUnit.skillCooldown}턴 남았습니다!");
            return;
        }

        // 2. 직업에 따라 발동 방식을 나눕니다.
        if (activeUnit.unitClass == Unit.UnitClass.Warrior)
        {
            // 전사: 타일을 선택해야 하므로 상태를 변경하고 빨간 타일을 깔아줍니다.
            Debug.Log("돌진할 방향의 타일을 클릭하세요.");
            currentState = BattleState.SelectingSkill;
            ShowWarriorSkillTiles(activeUnit);
        }
        else if (activeUnit.unitClass == Unit.UnitClass.Archer)
        {
            // 궁수: 타일 선택 없이 제자리에서 버프가 즉시 발동됩니다!
            activeUnit.UseSkill(); // Unit.cs에 만들어두신 UseSniperSkill()이 알아서 실행됩니다.

            // 타일을 클릭할 필요가 없으므로 상태는 Idle로 둡니다.
            currentState = BattleState.Idle;

            // 행동을 소모했으므로 턴을 종료시킵니다.
            TurnManager.Instance.ChangeState(GameState.PlayerTurnEnd);
        }
        else if (activeUnit.unitClass == Unit.UnitClass.Magician)
        {
            Debug.Log("공간 이동시킬 유닛을 클릭하세요");
            currentState = BattleState.SelectingSkill;
            ShowMagicianSkillTargetTiles(activeUnit);
        }
    }

    void ShowWarriorSkillTiles(Unit unit)
    {
        ClearHighlights();

        Vector3Int startCell = gridTilemap.WorldToCell(unit.transform.position);

        Vector3Int[] directions = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };

        foreach (Vector3Int dir in directions)
        {
            for (int i = 1; i <= 4; i++)
            {
                Vector3Int nextCell = startCell + dir * i;
                Vector3 nextWorldPos = gridTilemap.GetCellCenterWorld(nextCell);

                Collider2D hit = Physics2D.OverlapPoint(nextWorldPos);
                if (hit != null)
                {
                    Obstacle obstacle = hit.GetComponent<Obstacle>();
                    if (obstacle != null && !obstacle.IsPassable())
                    {
                        break;
                    }
                }

                validSkillCells.Add(nextCell);
                SpawnHighlight(nextCell);
            }
        }
    }

    void ShowMovableTiles(Unit unit)
    {
        ClearHighlights();

        Vector3Int startCell = gridTilemap.WorldToCell(unit.transform.position);
        int range = unit.moveRange;

        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        Dictionary<Vector3Int, int> visited = new Dictionary<Vector3Int, int>();

        queue.Enqueue(startCell);
        visited.Add(startCell, 0);

        Vector3Int[] directions = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };

        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            int currentDist = visited[current];

            validMoveCells.Add(current);
            SpawnHighlight(current);

            if (currentDist >= range) continue;

            foreach (Vector3Int dir in directions)
            {
                Vector3Int next = current + dir;

                if (visited.ContainsKey(next)) continue;

                Vector3 nextWorldPos = gridTilemap.GetCellCenterWorld(next);

                Collider2D hit = Physics2D.OverlapPoint(nextWorldPos);
                bool isPassable = true;

                if (hit != null)
                {
                    Obstacle obstacle = hit.GetComponent<Obstacle>();
                    if (obstacle != null)
                    {
                        isPassable = obstacle.IsPassable();
                    }

                    Unit unitOnTile = hit.GetComponent<Unit>();
                    if (unitOnTile != null)
                    {
                        isPassable = false;
                    }
                }

                if (isPassable)
                {
                    visited.Add(next, currentDist + 1);
                    queue.Enqueue(next);
                }
            }
        }
    }

    void SpawnHighlight(Vector3Int cellPos)
    {
        Vector3 pos = gridTilemap.GetCellCenterWorld(cellPos);
        pos.z = 0;
        GameObject highlight = Instantiate(moveHighlightPrefab, pos, Quaternion.identity);
        activeHighlights.Add(highlight);
    }

    void ClearHighlights()
    {
        foreach (var obj in activeHighlights) Destroy(obj);
        activeHighlights.Clear();
        validMoveCells.Clear();
        validAttackCells.Clear();
        validSkillCells.Clear();
    }


    void ShowArcherSkillTiles(Unit unit)
    {
        ClearHighlights();

        if (unit.unitClass == Unit.UnitClass.Archer && unit.isSniperMode)
        {
            Debug.Log("저격 모드 실행 중");
            currentState = BattleState.Idle;
            return;
        }

        unit.isSniperMode = true;
        unit.sniperModeTurnsLeft = 2;
        Debug.Log($"{unit.unitClass}가 스킬 발동");
        currentState = BattleState.Idle;
    }

    void ShowMagicianSkillTargetTiles(Unit unit)
    {
        ClearHighlights();
        validSkillCells.Clear();

        Vector3Int startCell = gridTilemap.WorldToCell(unit.transform.position);
        int range = 4;

        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) <= range) // 맨해튼 거리 계산
                {
                    Vector3Int targetCell = startCell + new Vector3Int(x, y, 0);
                    validSkillCells.Add(targetCell);
                    SpawnHighlight(targetCell);
                }
            }
        }
    }

    void ShowMagicianSkillDestinationTiles(Unit targetUnit)
    {
        ClearHighlights();
        validSkillCells.Clear();

        Vector3Int startCell = gridTilemap.WorldToCell(targetUnit.transform.position);
        int range = 3;

        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) <= range)
                {
                    Vector3Int targetCell = startCell + new Vector3Int(x, y, 0);

                    if (targetCell == startCell) continue;

                    Vector3 worldPos = gridTilemap.GetCellCenterWorld(targetCell);
                    Collider2D hit = Physics2D.OverlapPoint(worldPos);
                    bool isPassable = true;

                    if (hit != null)
                    {
                        Obstacle obs = hit.GetComponent<Obstacle>();
                        if (obs != null && !obs.IsPassable())
                        {
                            isPassable = false;
                        }
                        if (hit.GetComponent<Unit>() != null || hit.GetComponent<Core>() != null) isPassable = false;
                    }

                    if (isPassable)
                    {
                        validSkillCells.Add(targetCell);
                        SpawnHighlight(targetCell);
                    }
                }
            }
        }
    }
}
