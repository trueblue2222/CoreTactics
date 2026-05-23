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

    [Header("Animation Setting")]
    public float moveSpeed = 2f;


    void Start()
    {
        currentHp = maxHp;
    }

    public void TakeDamage(int damage)
    {
        currentHp -= damage;
        Debug.Log($"[{team}] {unitClass} 피격, 남은 체력: {currentHp}");
        if (currentHp <= 0)
        {
            Die();
        }
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
        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPos;
        onMoveComplete?.Invoke();
    }
}
