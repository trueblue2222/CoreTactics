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
    [SerializeField] private string playerTeamName = "Player";

    [Header("Units")]
    public Unit activeUnit;
    public Unit inspectedUnit;

    [Header("Movement Highlights")]
    public GameObject moveHighlightPrefab;
    private List<GameObject> activeHighlights = new List<GameObject>();
    private List<Vector3Int> validMoveCells = new List<Vector3Int>();

    [Header("Combat Highlights")]
    public GameObject attackHighlightPrefab;
    private List<Vector3Int> validAttackCells = new List<Vector3Int>();

    [Header("Skill Highlights")]
    private List<Vector3Int> validSkillCells = new List<Vector3Int>();

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && !TurnManager.Instance.IsInputBlocked)
        {
            HandleMouseClick();
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
                    Vector3 centerPos = gridTilemap.GetCellCenterWorld(cellPos);
                    centerPos.z = 0;
                    activeUnit.transform.position = centerPos;
                    Debug.Log($"[이동] {activeUnit.unitClass}가 {cellPos}로 이동");

                    ClearHighlights();

                    currentState = BattleState.Idle;
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
                        Debug.Log($"{activeUnit.unitClass}가 {clickedUnit.unitClass}를 공격");
                        clickedUnit.TakeDamage(activeUnit.atk);

                        ClearHighlights();
                        currentState = BattleState.Idle;
                    }
                    else if (clickedCore != null && clickedCore.team != activeUnit.team)
                    {
                        Debug.Log($"[핵 공격] {activeUnit.unitClass}가 상대방 핵을 공격");
                        clickedCore.TakeDamage(activeUnit.atk);

                        ClearHighlights();
                        currentState = BattleState.Idle;
                    }
                }
                else
                {
                    Debug.Log("공격 범위 밖입니다.");
                }
                break;
            case BattleState.SelectingSKill:
                if (validSkillCells.Contains(cellPos))
                {
                    Vector3Int startCell = gridTilemap.WorldToCell(activeUnit.transform.position);

                    Vector3Int dir = new Vector3Int(
                        Mathf.Clamp((cellPos.x - startCell.x), -1, 1),
                        Mathf.Clamp((cellPos.y - startCell.y), -1, 1),
                        0
                    );
                    int dist = (int)Mathf.Max(Mathf.Abs(cellPos.x - startCell.x), Mathf.Abs(cellPos.y - startCell.y));

                    for(int i = 1; i <= dist; i++)
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

                    StartCoroutine(activeUnit.MoveSmoothly(targetWorldPos, ()=> {Debug.Log("돌진 스킬 완료");}));
                }
                else
                {
                    Debug.Log("스킬을 사용할 수 없는 위치입니다.");
                }
                break;
        }
    }

    public void OnMoveButtonClicked()
    {
        if (activeUnit == null) return;
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
        Debug.Log("스킬을 사용할 대상을 클릭하세요");
        currentState = BattleState.SelectingSKill;
        ShowWarriorSkillTiles(activeUnit);
    }

    void ShowWarriorSkillTiles(Unit unit)
    {
        ClearHighlights();

        Vector3Int startCell = gridTilemap.WorldToCell(unit.transform.position);

        Vector3Int[] directions = {Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right};

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
}
