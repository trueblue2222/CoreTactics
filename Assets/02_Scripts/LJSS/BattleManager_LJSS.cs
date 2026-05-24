using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    public void Awake()
    {
        Instance = this;
    }

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
    public List<Vector3Int> validSkillCells = new List<Vector3Int>();

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
                    // 💡 전사든 마법사든 본인의 타겟 클릭 로직을 알아서 실행합니다.
                    activeUnit.OnSkillTargetClicked(cellPos, clickedUnit, clickedCore);
                }
                else
                {
                    Debug.Log("스킬을 사용할 수 없는 위치입니다.");
                }
                break;

            case BattleState.SelectingSkillDestination:
                if (validSkillCells.Contains(cellPos))
                {
                    // 💡 마법사 본인의 도착지 클릭 로직을 알아서 실행합니다.
                    activeUnit.OnSkillDestinationClicked(cellPos);
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
        if (activeUnit.skillCooldown > 0)
        {
            Debug.Log($"{activeUnit.unitClass} 쿨타임 대기중 ({activeUnit.skillCooldown}턴 남음)");
            return;
        }

        activeUnit.OnSkillButtonPressed();
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
                // 💡 OverlapPointAll을 사용하여 해당 타일의 모든 충돌체를 배열로 가져옵니다!
                Collider2D[] hits = Physics2D.OverlapPointAll(nextWorldPos);
                bool isPassable = true;

                foreach (Collider2D hit in hits)
                {
                    Obstacle obstacle = hit.GetComponent<Obstacle>();
                    if (obstacle != null && !obstacle.IsPassable()) isPassable = false;

                    Unit unitOnTile = hit.GetComponent<Unit>();
                    if (unitOnTile != null) isPassable = false; // 다른 유닛이 있으면 이동 불가

                    Core coreOnTile = hit.GetComponent<Core>();
                    if (coreOnTile != null) isPassable = false; // 코어가 있어도 이동 불가
                }

                if (isPassable)
                {
                    visited.Add(next, currentDist + 1);
                    queue.Enqueue(next);
                }
            }
        }
    }

    public void SpawnHighlight(Vector3Int cellPos)
    {
        Vector3 pos = gridTilemap.GetCellCenterWorld(cellPos);
        pos.z = 0;
        GameObject highlight = Instantiate(moveHighlightPrefab, pos, Quaternion.identity);
        activeHighlights.Add(highlight);
    }

    public void ClearHighlights()
    {
        foreach (var obj in activeHighlights) Destroy(obj);
        activeHighlights.Clear();
        validMoveCells.Clear();
        validAttackCells.Clear();
        validSkillCells.Clear();
    }

}
