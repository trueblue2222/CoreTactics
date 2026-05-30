using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GiantSlime : MonoBehaviour
{
    public static GiantSlime Instance { get; private set; }

    [Header("Slime Setting")]
    public GameObject slimePuddlePrefab;
    public int attackCooldown = 3;
    private int currentCooldown;

    [Header("Map Setting")]
    public int minX = -7;
    public int maxX = 6;
    public int minY = -3;
    public int maxY = 2;

    private List<SlimePuddle> activePuddles = new List<SlimePuddle>();

    void Awake()
    {
        Instance = this;
        currentCooldown = attackCooldown;
    }

    public void OnRoundPassed()
    {
        for (int i = activePuddles.Count - 1; i >= 0; i--)
        {
            if (activePuddles[i] != null) activePuddles[i].OnRoundEnd();
            else activePuddles.RemoveAt(i);
        }

        currentCooldown--;
        if (currentCooldown <= 0)
        {
            Debug.Log("<color=green>[거대 슬라임] 이번 턴에 점액을 발사합니다!</color>");
            ShootSlimePuddles(4);
            currentCooldown = attackCooldown;
        }
        else
        {
            Debug.Log($"<color=orange>[거대 슬라임] 점액 발사까지 {currentCooldown}라운드 남았습니다.</color>");
        }
    }

    private void ShootSlimePuddles(int amount)
    {
        Debug.Log($"거대 슬라임이 {amount}개의 점액을 무작위로 뱉습니다");
        int spawned = 0;
        int maxAttempts = 50;

        while (spawned < amount && maxAttempts > 0)
        {
            maxAttempts--;

            int randomX = Random.Range(minX, maxX + 1);
            int randomY = Random.Range(minY, maxY + 1);
            Vector3Int randomCell = new Vector3Int(randomX, randomY, 0);

            Vector3 worldPos = BattleManager.Instance.gridTilemap.GetCellCenterWorld(randomCell);
            worldPos.z = 0;

            Collider2D[] hits = Physics2D.OverlapPointAll(worldPos);
            bool hasObstacle = false;

            foreach(Collider2D hit in hits){
                Obstacle obs = hit.GetComponent<Obstacle>();
                if (hit.GetComponent<Obstacle>() != null || 
                    hit.GetComponent<Unit>() != null || 
                    hit.GetComponent<Core>() != null)
                {
                    hasObstacle = true;
                    break;
                }
            }

            if (!hasObstacle)
            {
                GameObject puddleObj = Instantiate(slimePuddlePrefab, worldPos, Quaternion.identity);
                SlimePuddle puddle = puddleObj.GetComponent<SlimePuddle>();
                activePuddles.Add(puddle);

                puddle.CheckUnitOnPuddle();

                spawned++;
            }
        }
    }
}

// -6.5 ~ 6.5 || 3.5 ~ -2.5