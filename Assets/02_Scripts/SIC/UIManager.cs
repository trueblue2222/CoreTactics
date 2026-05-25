using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.VisualScripting;

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


    // LJSS 0524 하단 UI 설정을 위한 참조 목록
    // ── 왼쪽 패널: 행동 중인 아군 유닛 ──────────────────────────
    [Header("Active Unit Panel (Left)")]
    public GameObject activeUnitPanel;
    public Image activeUnitPortrait;
    public TextMeshProUGUI activeUnitHP;
    public TextMeshProUGUI activeUnitAtk;
    public TextMeshProUGUI activeUnitDef;

    // ── 오른쪽 패널: 탐색(inspect) 중인 유닛 ────────────────────
    [Header("Inspected Unit Panel (Right)")]
    public GameObject inspectedUnitPanel;
    public Image inspectedUnitPortrait;
    public TextMeshProUGUI inspectedUnitHP;
    public TextMeshProUGUI inspectedUnitAtk;
    public TextMeshProUGUI inspectedUnitDef;

    // ── 아군 초상화 스프라이트 ────────────────────────────────────
    [Header("Player Portrait Sprites")]
    public Sprite playerWarriorSprite;
    public Sprite playerArcherSprite;
    public Sprite playerMagicianSprite;

    // ── 적군 초상화 스프라이트 ────────────────────────────────────
    [Header("Enemy Portrait Sprites")]
    public Sprite enemyWarriorSprite;
    public Sprite enemyArcherSprite;
    public Sprite enemyMagicianSprite;

    // ── 기본 초상화 (스프라이트 미설정 시 폴백) ───────────────────
    [Header("Misc")]
    public GameObject turnEndNoticeObject;

    [Header("Action Button Panel")]
    public GameObject actionButtonPanel;
    public Button moveButton;
    public Button attackButton;
    public Button skillButton;
    public Button cancelButton;

    [Header("Default")]
    public Sprite defaultPortraitSprite;
    private const string DEFAULT_STAT = "-";


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

        ClearActiveUnitUI();
        ClearInspectedUnitUI();
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


    // ─── 0524 하단 유닛 상태 수정 UI  ───────────────────────────────────────

    // ────────────────────────────────────────────────────────────
    // 아군 Active Unit UI 갱신
    // ────────────────────────────────────────────────────────────
    public void UpdateActiveUnitUI(Unit unit)
    {
        if (unit == null) { ClearActiveUnitUI(); return; }

        activeUnitPanel.SetActive(true);

        activeUnitPortrait.sprite = GetPortrait(unit.unitClass, unit.team);
        activeUnitHP.text = $"HP  {unit.currentHp} / {unit.maxHp}";
        activeUnitAtk.text = $"ATK  {unit.atk}";
        activeUnitDef.text = $"DEF  {unit.def}";
    }

    // ────────────────────────────────────────────────────────────
    // 탐색 Inspected Unit UI 갱신
    // ────────────────────────────────────────────────────────────
    public void UpdateInspectedUnitUI(Unit unit)
    {
        if (unit == null) { ClearInspectedUnitUI(); return; }

        inspectedUnitPanel.SetActive(true);

        inspectedUnitPortrait.sprite = GetPortrait(unit.unitClass, unit.team);
        inspectedUnitHP.text = $"HP  {unit.currentHp} / {unit.maxHp}";
        inspectedUnitAtk.text = $"ATK  {unit.atk}";
        inspectedUnitDef.text = $"DEF  {unit.def}";
    }

    // ────────────────────────────────────────────────────────────
    // UI 초기화
    // ────────────────────────────────────────────────────────────
    public void ClearActiveUnitUI()
    {
        activeUnitPortrait.sprite = defaultPortraitSprite;
        activeUnitHP.text = DEFAULT_STAT;
        activeUnitAtk.text = DEFAULT_STAT;
        activeUnitDef.text = DEFAULT_STAT;

        HideActionButtons();
    }

    public void ClearInspectedUnitUI()
    {
        inspectedUnitPortrait.sprite = defaultPortraitSprite;
        inspectedUnitHP.text = DEFAULT_STAT;
        inspectedUnitAtk.text = DEFAULT_STAT;
        inspectedUnitDef.text = DEFAULT_STAT;
    }

    // ────────────────────────────────────────────────────────────
    // HP 변동 시 즉시 반영
    // ────────────────────────────────────────────────────────────
    public void RefreshUnitUI()
    {
        if (BattleManager.Instance.activeUnit != null)
            UpdateActiveUnitUI(BattleManager.Instance.activeUnit);

        if (BattleManager.Instance.inspectedUnit != null)
            UpdateInspectedUnitUI(BattleManager.Instance.inspectedUnit);
    }

    // ────────────────────────────────────────────────────────────
    // 팀 + 직업에 맞는 초상화 반환
    // ────────────────────────────────────────────────────────────
    private Sprite GetPortrait(Unit.UnitClass unitClass, string team)
    {
        bool isPlayer = team == "Player";

        Sprite portrait = unitClass switch
        {
            Unit.UnitClass.Warrior => isPlayer ? playerWarriorSprite : enemyWarriorSprite,
            Unit.UnitClass.Archer => isPlayer ? playerArcherSprite : enemyArcherSprite,
            Unit.UnitClass.Magician => isPlayer ? playerMagicianSprite : enemyMagicianSprite,
            _ => null
        };

        return portrait != null ? portrait : defaultPortraitSprite;
    }

    public void ShowActionButtons()
    {
        if (actionButtonPanel != null)
            actionButtonPanel.SetActive(true);

        
        SetActionButtons(true);
    }

    public void HideActionButtons()
    {
        SetActionButtons(false);
    }

    private void SetActionButtons(bool interactable)
    {
        if (moveButton != null) moveButton.interactable = interactable;
        if (attackButton != null) attackButton.interactable = interactable;
        if (skillButton != null) skillButton.interactable = interactable;
        if (cancelButton != null) cancelButton.interactable = interactable;
    }
}
