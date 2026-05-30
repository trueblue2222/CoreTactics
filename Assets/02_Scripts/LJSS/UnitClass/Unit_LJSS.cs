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

    [Header("Animation Setting")]
    public float moveSpeed = 2f;
    [SerializeField] private float hitFlashDuration = 0.3f;

    [Header("Highlgiht")]
    public GameObject activeHighlight;
    public GameObject inspectedHighlight;

    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }


    void Start()
    {
        currentHp = maxHp;
        ClearHighlights();
    }

    public void TakeDamage(int damage)
    {
        currentHp -= damage;
        Debug.Log($"[{team}] {unitClass} 피격, 남은 체력: {currentHp}");

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
    }

    public virtual void OnSkillButtonPressed()
    {
        Debug.Log("기본 유닛은 스킬이 없습니다.");
    }

    public virtual void OnSkillTargetClicked(Vector3Int cellPos, Unit clickedUnit, Core clickedCore)
    {
    }

    public virtual void OnSkillDestinationClicked(Vector3Int cellPos)
    {
    }

    public void UpdateTurnState()
    {
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
            }
        }
    }

    public IEnumerator MoveSmoothly(Vector3 targetPos, Action onMoveComplete)
    {
        yield return StartCoroutine(MoveSmoothly(targetPos, moveSpeed, onMoveComplete));
    }

    public IEnumerator MoveSmoothly(Vector3 targetPos, float speed, Action onMoveComplete)
    {
        bool interceptedBySlime = false;

        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

            Collider2D[] hits = Physics2D.OverlapPointAll(transform.position);
            SlimePuddle puddleFound = null;

            foreach (Collider2D hit in hits)
            {
                SlimePuddle puddle = hit.GetComponent<SlimePuddle>();
                if (puddle != null)
                {
                    puddleFound = puddle;
                    break;
                }
            }

            if (puddleFound != null)
            {
                Vector3 stopPos = puddleFound.transform.position;
                stopPos.z = 0;
                transform.position = stopPos;

                puddleFound.ApplyDebuff(this);

                interceptedBySlime = true;
                break;
            }
            yield return null;
        }

        if (!interceptedBySlime)
        {
            transform.position = targetPos;
        }

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
}
