using UnityEngine;

public class Archer : Unit
{
    public override void OnSkillButtonPressed()
    {
        if (isSniperMode)
        {
            Debug.Log("이미 저격 모드입니다.");
            return;
        }

        Debug.Log("궁수 : 저격 모드 발동!");
        isSniperMode = true;
        sniperModeTurnsLeft = 2;

        attackRange += 1;
        moveRange = 0;

        skillCooldown = 2;

        if (TurnManager.Instance.IsPlayerTurn)
        {
            BattleManager.Instance.currentState = BattleManager.BattleState.Idle;
            TurnManager.Instance.ChangeState(GameState.PlayerTurnEnd);
        }
    }
}
