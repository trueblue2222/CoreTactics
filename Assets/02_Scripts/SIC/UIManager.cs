using System.Collections;
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

    // ─── 행동 불가 알림 ───────────────────────────────────────
    [Header("행동 불가 알림")]
    [SerializeField] private TMP_Text turnEndNoticeText;
    [SerializeField] private float noticeDisplayTime = 1.5f;
    private Coroutine _hideNoticeCoroutine;

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
        turnEndNoticeText?.gameObject.SetActive(false);
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
    }

    // ─── 행동 불가 알림 표시 ──────────────────────────────────
    public void ShowTurnEndNotice()
    {
        if (turnEndNoticeText == null) return;

        turnEndNoticeText.text = "Cannot act anymore in this turn!";
        turnEndNoticeText.gameObject.SetActive(true);

        if (_hideNoticeCoroutine != null) StopCoroutine(_hideNoticeCoroutine);
        _hideNoticeCoroutine = StartCoroutine(HideNoticeAfterDelay());
    }

    private IEnumerator HideNoticeAfterDelay()
    {
        yield return new WaitForSeconds(noticeDisplayTime);
        turnEndNoticeText?.gameObject.SetActive(false);
    }

    // ─── TurnEnd 버튼 클릭 ────────────────────────────────────
    public void OnTurnEndButtonClicked()
    {
        TurnManager.Instance.OnTurnEndButtonClicked();
    }

    // ─── 상태 변화 처리 ───────────────────────────────────────
    private void OnStateChanged(GameState state)
    {
        if (state == GameState.PlayerTurnStart || state == GameState.EnemyTurnStart)
        {
            firstAttackText?.gameObject.SetActive(false);
            turnEndNoticeText?.gameObject.SetActive(false);
        }

        bool playerCanAct = state == GameState.PlayerUnitSelect
                         || state == GameState.PlayerActionSelect
                         || state == GameState.PlayerTurnEnd;
        SetTurnEndButton(playerCanAct);
    }

    private void SetTurnEndButton(bool interactable)
    {
        if (turnEndButton != null)
            turnEndButton.interactable = interactable;
    }
}
