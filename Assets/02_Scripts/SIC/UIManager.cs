using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // ─── 선공 표시 ────────────────────────────────────────────
    [Header("선공 표시")]
    [SerializeField] private TMP_Text firstAttackText;

    // ─── 턴 종료 버튼 ─────────────────────────────────────────
    [Header("턴 종료 버튼")]
    [SerializeField] private Button turnEndButton;

    // ─────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        firstAttackText?.gameObject.SetActive(false);
        SetTurnEndButton(false);

        TurnManager.Instance.OnFirstAttackDecided += ShowFirstAttackResult;
        TurnManager.Instance.OnStateChanged += OnStateChanged;
    }

    void OnDestroy()
    {
        if (TurnManager.Instance == null) return;
        TurnManager.Instance.OnFirstAttackDecided -= ShowFirstAttackResult;
        TurnManager.Instance.OnStateChanged -= OnStateChanged;
    }

    // ─── 선공 결과 표시 ───────────────────────────────────────
    private void ShowFirstAttackResult(bool isPlayerFirst)
    {
        if (firstAttackText != null)
            firstAttackText.text = isPlayerFirst ? "Player Turn!" : "Enemy Turn!";

        firstAttackText?.gameObject.SetActive(true);
        // 텍스트는 TurnManager가 다음 상태로 전환할 때 OnStateChanged에서 숨김
    }

    // ─── 상태 변화 처리 ───────────────────────────────────────
    private void OnStateChanged(GameState state)
    {
        // 선공 패널: PlayerTurnStart / EnemyTurnStart에 진입하면 닫기
        if (state == GameState.PlayerTurnStart || state == GameState.EnemyTurnStart)
            firstAttackText?.gameObject.SetActive(false);

        // 턴 종료 버튼: 플레이어가 유닛·행동을 고르는 동안만 활성화
        // PlayerActionExecute(애니메이션 중)는 제외해 중복 클릭 방지
        bool playerCanAct = state == GameState.PlayerUnitSelect
                         || state == GameState.PlayerActionSelect;
        SetTurnEndButton(playerCanAct);
    }

    private void SetTurnEndButton(bool interactable)
    {
        if (turnEndButton != null)
            turnEndButton.interactable = interactable;
    }
}
