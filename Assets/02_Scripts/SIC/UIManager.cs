using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.VisualScripting;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // ─── 턴 배너 ─────────────────────────────────────────────
    [Header("턴 배너")]
    [SerializeField] private GameObject playerTurnObject;
    [SerializeField] private GameObject enemyTurnObject;
    [SerializeField] private float bannerFadeDuration = 0.5f;
    [SerializeField] private float bannerDisplayTime  = 1.5f;
    private Coroutine _turnBannerCoroutine;

    // ─── 턴 종료 버튼 ─────────────────────────────────────────
    [Header("턴 종료 버튼")]
    [SerializeField] private Button turnEndButton;

    // ─── 행동 불가 알림 ───────────────────────────────────────
    [Header("행동 불가 알림")]
    [SerializeField] private GameObject turnEndNoticeImage;
    [SerializeField] private float noticeFadeDuration = 0.4f;
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
    public Button skill2Button;

    [Header("Default")]
    public Sprite defaultPortraitSprite;
    private const string DEFAULT_STAT = "-";

    [Header("Core Hp UI")]
    public Slider playerCoreHpBar;
    public Slider enemyCoreHpBar;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public GameObject victoryImage; // 플레이어 승리 이미지
    public GameObject defeatImage;  // 적 승리(Game Over) 이미지


    void Awake()
    {
        /*
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }*/
        Instance = this;
        // DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        playerTurnObject?.SetActive(false);
        enemyTurnObject?.SetActive(false);
        turnEndNoticeImage?.SetActive(false);
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
        // 배너는 TurnStart 상태 전환 시 표시하므로 여기서는 처리 없음
    }

    // ─── 턴 배너 표시 ─────────────────────────────────────────
    private void ShowTurnBanner(bool isPlayerTurn)
    {
        GameObject active   = isPlayerTurn ? playerTurnObject : enemyTurnObject;
        GameObject inactive = isPlayerTurn ? enemyTurnObject  : playerTurnObject;

        inactive?.SetActive(false);

        if (active == null) return;
        active.SetActive(true);

        if (_turnBannerCoroutine != null) StopCoroutine(_turnBannerCoroutine);
        _turnBannerCoroutine = StartCoroutine(TurnBannerRoutine(active));
    }

    private IEnumerator TurnBannerRoutine(GameObject obj)
    {
        CanvasGroup cg = obj.GetComponent<CanvasGroup>();

        if (cg == null)
        {
            yield return new WaitForSeconds(bannerDisplayTime);
            obj.SetActive(false);
            yield break;
        }

        cg.alpha = 0f;
        yield return StartCoroutine(FadeCanvasGroup(cg, 1f));
        yield return new WaitForSeconds(bannerDisplayTime);
        yield return StartCoroutine(FadeCanvasGroup(cg, 0f));
        obj.SetActive(false);
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration = -1f)
    {
        float d          = duration < 0f ? bannerFadeDuration : duration;
        float startAlpha = cg.alpha;
        float elapsed    = 0f;

        while (elapsed < d)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / d);
            yield return null;
        }

        cg.alpha = targetAlpha;
    }

    // ─── 행동 불가 알림 표시 ──────────────────────────────────
    public void ShowTurnEndNotice()
    {
        if (turnEndNoticeImage == null) return;

        if (_hideNoticeCoroutine != null) StopCoroutine(_hideNoticeCoroutine);
        _hideNoticeCoroutine = StartCoroutine(NoticeRoutine());
    }

    private IEnumerator NoticeRoutine()
    {
        CanvasGroup cg = turnEndNoticeImage.GetComponent<CanvasGroup>();
        turnEndNoticeImage.SetActive(true);

        if (cg == null)
        {
            yield return new WaitForSeconds(noticeDisplayTime);
            turnEndNoticeImage.SetActive(false);
            yield break;
        }

        cg.alpha = 0f;
        yield return StartCoroutine(FadeCanvasGroup(cg, 1f, noticeFadeDuration));
        yield return new WaitForSeconds(noticeDisplayTime);
        yield return StartCoroutine(FadeCanvasGroup(cg, 0f, noticeFadeDuration));
        turnEndNoticeImage.SetActive(false);
    }

    // ─── TurnEnd 버튼 클릭 ────────────────────────────────────
    public void OnTurnEndButtonClicked()
    {
        TurnManager.Instance.OnTurnEndButtonClicked();
    }

    // ─── 상태 변화 처리 ───────────────────────────────────────
    private void OnStateChanged(GameState state)
    {
        if (state == GameState.PlayerTurnStart)
        {
            ShowTurnBanner(true);
            if (_hideNoticeCoroutine != null) StopCoroutine(_hideNoticeCoroutine);
            turnEndNoticeImage?.SetActive(false);
        }
        else if (state == GameState.EnemyTurnStart)
        {
            ShowTurnBanner(false);
            if (_hideNoticeCoroutine != null) StopCoroutine(_hideNoticeCoroutine);
            turnEndNoticeImage?.SetActive(false);
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
        if (skill2Button != null) skill2Button.interactable = interactable;
        if (cancelButton != null) cancelButton.interactable = interactable;
    }

    // Core HP
    public void UpdateCoreHp(string team, int currentHp, int maxHp)
    {
        if (team == "Player" && playerCoreHpBar != null)
        {
            playerCoreHpBar.maxValue = maxHp;
            playerCoreHpBar.value = currentHp;
        }
        else if (team == "Enemy" && enemyCoreHpBar != null)
        {
            enemyCoreHpBar.maxValue = maxHp;
            enemyCoreHpBar.value = currentHp;
        }
    }

    public void ShowGameOver(bool isVictory)
    {
        if (_turnBannerCoroutine != null) StopCoroutine(_turnBannerCoroutine);
        if (playerTurnObject != null) playerTurnObject.SetActive(false);
        if (enemyTurnObject != null) enemyTurnObject.SetActive(false);
        
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);

            // 💡 [추가] 승패 결과에 따라 알맞은 이미지만 켜고 끕니다.
            if (victoryImage != null) victoryImage.SetActive(isVictory);
            if (defeatImage != null) defeatImage.SetActive(!isVictory);
        }
    }

    // 💡 [추가] Restart 버튼 클릭 시 실행될 함수
    public void RestartGame()
    {
        // 0 또는 1을 랜덤으로 뽑습니다. (Random.Range에서 정수 사용 시 최댓값은 포함되지 않음)
        int randomSceneIndex = UnityEngine.Random.Range(0, 2);
        
        // 0이면 BlackMagician, 1이면 Slime 씬을 선택합니다.
        string sceneToLoad = (randomSceneIndex == 0) ? "0516_LJSS_Black" : "0516_LJSS_Slime";
        
        Debug.Log($"[UIManager] 랜덤 씬 로드: {sceneToLoad}");
        SceneManager.LoadScene(sceneToLoad);
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
