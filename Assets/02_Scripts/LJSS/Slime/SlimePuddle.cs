using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlimePuddle : MonoBehaviour
{
    public int durationTurns = 3;

    public void CheckUnitOnPuddle()
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(transform.position);
        foreach (Collider2D hit in hits)
        {
            Unit unit = hit.GetComponent<Unit>();
            if (unit != null)
            {
                ApplyDebuff(unit);
            }
        }
    }

    public void ApplyDebuff(Unit unit)
    {
        if (unit.rootedTurns <= 0)
        {
            Debug.Log($"{unit.unitClass}가 점액을 밟았습니다 1턴 간 이동이 불가능합니다");
            unit.rootedTurns = 2;

            unit.SetSlimeColor(true);

            Destroy(gameObject);
        }
    }

    public void OnRoundEnd()
    {
        durationTurns--;
        if (durationTurns < 0)
        {
            Debug.Log("점액이 사라집니다");
            Destroy(gameObject);
        }
    }
}
