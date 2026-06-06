using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.VisualScripting;

public class Unit : MonoBehaviour
{
    public enum UnitClass { Warrior, Archer, Magician }
    [Header("Basic Info")]
    public UnitClass unitClass;
    public string team;

    [Header("Stats")]
    public int maxHp;
    public int currentHp;
    public int atk;
    public int def;

    [Header("Ranges (Manhattan Distance)")]
    public int moveRange;
    public int attackRange;

    [Header("Skill & State")]
    public int skillCooldown = 0;
    public bool isSniperMode = false;
    public int sniperModeTurnsLeft = 0;
    public int rootedTurns = 0; // 점액 구속 상태 (0이면 정상, 1 이상 이면 이동 불가)
    public bool hasActedThisTurn = false;

    public GameObject buffEffectObj;

    [Header("Animation Setting")]
    public float moveSpeed = 2f;
    [SerializeField] private float hitFlashDuration = 0.3f;

    [Header("Highlgiht")]
    public GameObject activeHighlight;
    public GameObject inspectedHighlight;

    private SpriteRenderer spriteRenderer;
    protected Animator anim;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
    }


    void Start()
    {
        currentHp = maxHp;
        ClearHighlights();
    }

    public void TakeDamage(int damage)
    {
        int actualDamage = Mathf.Max(1, damage - def); 
        
        // (만약 방어력이 더 높을 때 데미지를 아예 0으로 만들고 싶으시다면 아래 코드를 쓰시면 됩니다)
        // int actualDamage = Mathf.Max(0, damage - def);

        currentHp -= actualDamage; // 계산된 실제 데미지만큼만 체력 감소
        
        // 로그도 상세하게 출력하여 방어력이 잘 적용되었는지 확인하기 쉽게 바꿉니다.
        Debug.Log($"[{team}] {unitClass} 피격! (원래 피해: {damage}, 방어력: {def}) ➡️ 실제 받은 피해: {actualDamage}, 남은 체력: {currentHp}");

        TriggerAttackedAnim();

        if (BattleManager.Instance.activeUnit == this)
            UIManager.Instance.UpdateActiveUnitUI(this);
        else if (BattleManager.Instance.inspectedUnit == this)
            UIManager.Instance.UpdateInspectedUnitUI(this);

        StartCoroutine(HitFlashRoutine());

        if (currentHp <= 0)
        {
            Die();
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        if (spriteRenderer == null) yield break;

        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(hitFlashDuration);

        if (gameObject.activeInHierarchy)
            spriteRenderer.color = Color.white;
    }

    private void Die()
    {
        Debug.Log($"[{team}] {unitClass} 사망");
        gameObject.SetActive(false);

        TurnManager.Instance.CheckUnitDeathWinCondition();
    }

    public virtual void OnSkillButtonPressed()
    {
        Debug.Log("기본 유닛은 스킬이 없습니다.");
    }

    public virtual void OnSecondSkillButtonPressed()
    {
        Debug.Log("기본 유닛은 스킬이 없습니다.");
    }

    public virtual void OnSkillTargetClicked(Vector3Int cellPos, Unit clickedUnit, Core clickedCore)
    {
    }

    public virtual void OnSkillDestinationClicked(Vector3Int cellPos)
    {
    }

    public void SetActedVisual(bool acted)
    {
        if (spriteRenderer == null) return;
        Color c = spriteRenderer.color;
        c.a = acted ? 0.4f : 1f;
        spriteRenderer.color = c;
    }

    public void UpdateTurnState()
    {
        hasActedThisTurn = false;
        SetActedVisual(false);

        if (skillCooldown > 0)
        {
            skillCooldown--;
        }

        if (rootedTurns > 0)
        {
            rootedTurns--;
            if (rootedTurns == 0) 
            {
                Debug.Log($"[{team}] {unitClass} 점액 구속 해제");

                SetSlimeColor(false);
            }
        }

        if (unitClass == UnitClass.Archer && isSniperMode)
        {
            sniperModeTurnsLeft--;
            if (sniperModeTurnsLeft <= 0)
            {
                Debug.Log("궁수 : 저격 모드 해제");
                isSniperMode = false;
                attackRange -= 1;
                moveRange = 2;
                skillCooldown = 2;

                if (buffEffectObj != null) buffEffectObj.SetActive(false);
            }
        }
    }

    public IEnumerator MoveSmoothly(Vector3 targetPos, Action onMoveComplete)
    {
        yield return StartCoroutine(MoveSmoothly(targetPos, moveSpeed, onMoveComplete));
    }

    public IEnumerator MoveSmoothly(Vector3 targetPos, float speed, Action onMoveComplete)
    {
        bool intercepted = false;

        Vector3Int previousCell = BattleManager.Instance.gridTilemap.WorldToCell(transform.position);

        if (anim != null) anim.SetBool("Move", true);

        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

            Vector3Int currentCell = BattleManager.Instance.gridTilemap.WorldToCell(transform.position);

            Collider2D[] hits = Physics2D.OverlapPointAll(transform.position);
            SlimePuddle puddleFound = null;
            bool hitBarricade = false;

            foreach (Collider2D hit in hits)
            {
                Obstacle obs = hit.GetComponent<Obstacle>();
                if (obs != null && !obs.IsPassable())
                {
                    hitBarricade = true;
                    break;
                }

                SlimePuddle puddle = hit.GetComponent<SlimePuddle>();
                if (puddle != null)
                {
                    puddleFound = puddle;
                    break;
                }
            }

            if (hitBarricade)
            {
                Vector3 stopPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(previousCell);
                stopPos.z = 0;
                transform.position = stopPos;

                intercepted = true;
                break;
            }

            if (puddleFound != null)
            {
                Vector3 stopPos = puddleFound.transform.position;
                stopPos.z = 0;
                transform.position = stopPos;

                puddleFound.ApplyDebuff(this);

                intercepted = true;
                break;
            }
            yield return null;
        }

        if (!intercepted)
        {
            transform.position = targetPos;
        }

        Collider2D[] finalHits = Physics2D.OverlapPointAll(transform.position);
        foreach (Collider2D hit in finalHits)
        {
            Obstacle obstacle = hit.GetComponent<Obstacle>();
            if (obstacle != null)
            {
                obstacle.OnUnitStepped(this);
            }
        }

        if (anim != null) anim.SetBool("Move", false);

        onMoveComplete?.Invoke();
    }

    public void SetActiveHighlight(bool on)
    {
        if (activeHighlight != null) activeHighlight.SetActive(on);
        if (on && inspectedHighlight != null) inspectedHighlight.SetActive(false);
    }

    public void SetInspectedHighlight(bool on)
    {
        if (inspectedHighlight != null) inspectedHighlight.SetActive(on);
        if (on && activeHighlight != null) activeHighlight.SetActive(false);
    }

    public void ClearHighlights()
    {
        if (activeHighlight != null) activeHighlight.SetActive(false);
        if (inspectedHighlight != null) inspectedHighlight.SetActive(false);
    }

    public void SetSlimeColor(bool isRooted)
    {
        if (spriteRenderer == null) return;

        spriteRenderer.color = isRooted ? Color.green : Color.white;
    }

    public virtual void TriggerAttackAnim()
    {
        if (anim != null) anim.SetTrigger("Attack");
    }

    public virtual void TriggerSkillAnim() // 1스킬
    {
        if (anim != null) anim.SetTrigger("Skill");
    }

    // 💡 [새로 추가] 2스킬 애니메이션
    public virtual void TriggerSecondSkillAnim() 
    {
        if (anim != null) anim.SetTrigger("Skill2");
    }

    // 💡 [새로 추가] 피격 애니메이션
    public virtual void TriggerAttackedAnim()
    {
        if (anim != null) anim.SetTrigger("Attacked"); 
    }
}
