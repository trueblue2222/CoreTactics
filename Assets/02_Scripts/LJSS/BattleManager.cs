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

    [Header("Units")]
    public Unit activeUnit;
    public Unit inspectedUnit;

    [Header("Movement Highlights")]
    public GameObject moveHighlightPrefab;
    private List<GameObject> activeHighlights = new List<GameObject>();
    private List<Vector3Int> validMoveCells = new List<Vector3Int>();

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleMouseClick();
        }
    }

    void HandleMouseClick()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);
        Unit clickedUnit = hit.collider != null ? hit.collider.GetComponent<Unit>() : null;
        Vector3Int cellPos = gridTilemap.WorldToCell(mousePos);

        switch (currentState)
        {
            case BattleState.Idle:
                if (clickedUnit != null)
                {
                    inspectedUnit = clickedUnit;
                    Debug.Log($"{inspectedUnit.unitClass}의 상태 확인");
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
                if (clickedUnit != null && clickedUnit != activeUnit)
                {
                    Debug.Log($"{activeUnit.unitClass}가 {clickedUnit.unitClass}를 공격");
                    currentState = BattleState.Idle;
                }
                break;
            case BattleState.SelectingSKill:
                if (clickedUnit != null)
                {
                    Debug.Log($"{activeUnit.unitClass}가 {clickedUnit.unitClass}에게 스킬 사용");
                    activeUnit.UseSkill();
                    currentState = BattleState.Idle;
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
    }

    public void OnSkillButtonClicked()
    {
        if (activeUnit == null) return;
        Debug.Log("스킬을 사용할 대상을 클릭하세요");
        currentState = BattleState.SelectingSKill;
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

        Vector3Int[] directions = {Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right};

        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            int currentDist = visited[current];

            validMoveCells.Add(current);
            SpawnHighlight(current);

            if (currentDist >= range) continue;

            foreach(Vector3Int dir in directions)
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
    }
}
