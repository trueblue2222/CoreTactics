using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Obstacle : MonoBehaviour
{
    public enum ObstacleType { Barricade, Spike}

    [Header("Obstacle Settings")]
    public ObstacleType obstacleType;
    public int trapDamage = 10;

    public bool IsPassable()
    {
        if (obstacleType == ObstacleType.Barricade)
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
}
