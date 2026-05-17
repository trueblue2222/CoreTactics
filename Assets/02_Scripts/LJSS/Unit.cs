using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    void Start()
    {
        currentHp = maxHp;
    }

    public void UseSkill()
    {
        if (skillCooldown > 0)
        {
            Debug.Log($"{unitClass}의 스킬 쿨타임이 {skillCooldown}턴 남았습니다");
            return;
        }

        switch (unitClass)
        {
            case UnitClass.Warrior:
                UseDashSkill();
                break;
            case UnitClass.Archer:
                UseSniperSkill();
                break;
            case UnitClass.Magician:
                UseTeleportSkill();
                break;
        }
    }

    private void UseDashSkill()
    {
        Debug.Log("전사 : 4칸 돌진 스킬 사용");
        skillCooldown = 2;
    }

    private void UseSniperSkill()
    {
        Debug.Log("궁수 : 저격 모드 사용");
        isSniperMode = true;
        sniperModeTurnsLeft = 2;

        attackRange += 1;
        moveRange = 0;
    }

    private void UseTeleportSkill()
    {
        Debug.Log("마법사 : 3칸 순간이동 스킬 사용");
        skillCooldown = 3;
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
}
