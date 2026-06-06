using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Obstacle : MonoBehaviour
{
    public enum ObstacleType { Barricade, Spike, Bomb}

    [Header("Obstacle Settings")]
    public ObstacleType obstacleType;
    public int trapDamage = 10;

    [Header("Bomb Settings")]
    public GameObject bombEffectPrefab; // 터질 때 생성될 이펙트 프리팹
    public int explosionDamage = 30;    // 폭발 데미지
    public int explosionRange = 2;      // 맨해튼 거리 2칸

    private bool isTriggered = false;
    private int turnsUntilExplosion = 2;
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        // 폭탄일 경우, 턴이 넘어가는 것을 감지하기 위해 TurnManager를 구독합니다.
        if (obstacleType == ObstacleType.Bomb && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnStateChanged += OnTurnStateChanged;
        }
    }

    void OnDestroy()
    {
        if (obstacleType == ObstacleType.Bomb && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnStateChanged -= OnTurnStateChanged;
        }
    }

    public bool IsPassable()
    {
       if (obstacleType == ObstacleType.Barricade || obstacleType == ObstacleType.Bomb)
        {
            return false;
        }
        return true;
    }

    public void OnUnitStepped(Unit unit)
    {
        if (obstacleType == ObstacleType.Spike)
        {
            unit.currentHp -= trapDamage;
            Debug.Log($"가시 함정 : {unit.unitClass}의 Hp가 {trapDamage}만큼 감소");
        }
    }

    public void TriggerBomb()
    {
        // 폭탄이 아니거나 이미 불이 붙은 상태면 무시
        if (obstacleType != ObstacleType.Bomb || isTriggered) return;

        isTriggered = true;
        turnsUntilExplosion = 2;
        
        // 점화되었다는 것을 시각적으로 보여주기 위해 빨간색으로 변경
        if (spriteRenderer != null) spriteRenderer.color = Color.red; 
        
        Debug.Log("💣 폭탄이 점화되었습니다! 2턴 뒤 폭발합니다.");
    }

    private void OnTurnStateChanged(GameState newState)
    {
        // 플레이어의 턴이 돌아올 때마다 카운트다운을 1씩 깎습니다.
        if (newState == GameState.PlayerUnitSelect && isTriggered)
        {
            turnsUntilExplosion--;
            if (turnsUntilExplosion <= 0)
            {
                Explode(); // 0이 되면 펑!
            }
            else
            {
                Debug.Log($"💣 폭탄 폭발까지 {turnsUntilExplosion}턴 남았습니다!");
            }
        }
    }

    private void Explode()
    {
        Debug.Log("💥 폭탄 폭발!");

        Vector3Int centerCell = BattleManager.Instance.gridTilemap.WorldToCell(transform.position);

        // 맨해튼 거리(x 절대값 + y 절대값)가 2칸 이하인 모든 타일 스캔
        for (int x = -explosionRange; x <= explosionRange; x++)
        {
            for (int y = -explosionRange; y <= explosionRange; y++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) <= explosionRange)
                {
                    Vector3Int targetCell = centerCell + new Vector3Int(x, y, 0);
                    Vector3 targetWorldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(targetCell);
                    targetWorldPos.z = 0;

                    // 1. 해당 칸에 이펙트 생성
                    if (bombEffectPrefab != null)
                    {
                        GameObject effect = Instantiate(bombEffectPrefab, targetWorldPos, Quaternion.identity);

                        Destroy(effect, 1f);
                    }

                    // 2. 해당 칸에 있는 유닛/코어 타격
                    Collider2D[] hits = Physics2D.OverlapPointAll(targetWorldPos);
                    foreach (Collider2D hit in hits)
                    {
                        Unit unit = hit.GetComponent<Unit>();
                        if (unit != null) unit.TakeDamage(explosionDamage);

                        Core core = hit.GetComponent<Core>();
                        if (core != null) core.TakeDamage(explosionDamage);

                        // 💡 [연쇄 폭발] 폭발 범위 안에 '다른 폭탄'이 있다면 같이 점화시킵니다!
                        Obstacle obs = hit.GetComponent<Obstacle>();
                        if (obs != null && obs != this && obs.obstacleType == ObstacleType.Bomb)
                        {
                            obs.TriggerBomb();
                        }
                    }
                }
            }
        }

        // 폭탄 자신의 오브젝트는 파괴되어 맵에서 사라짐
        Destroy(gameObject);
    }
}
