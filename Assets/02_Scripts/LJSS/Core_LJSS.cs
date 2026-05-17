using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Core : MonoBehaviour
{
    [Header("Core Settings")]
    public string team;
    public int maxHp = 100;
    public int currentHp;

    void Start()
    {
        currentHp = maxHp;
    }

    public void TakeDamage(int damage)
    {
        currentHp -= damage;
        Debug.Log($"[{team} 진영 핵] 피격, 현재 HP : {currentHp}/{maxHp}");

        if (currentHp <= 0)
        {
            currentHp = 0;
            DestroyCore();
        }
    }

    private void DestroyCore()
    {
        Debug.Log($"[{team} 진영 핵] 파괴, 게임 종료");
    }
}
