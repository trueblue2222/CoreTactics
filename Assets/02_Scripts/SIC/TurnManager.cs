using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    // ─── 상태 ────────────────────────────────────────────────
    public GameState CurrentState { get; private set; } = GameState.None;
    public bool IsPlayerTurn { get; private set; }
    public int TurnCount { get; private set; }

    // ─── 입력 잠금 ───────────────────────────────────────────
    // PlayerUnitSelect / PlayerActionSelect 외에는 모두 차단
    public bool IsInputBlocked { get; private set; } = true;

    // ─── 이벤트 (UI 등 외부 시스템이 구독) ──────────────────
    public event Action<GameState> OnStateChanged;
    public event Action<bool> OnFirstAttackDecided; // true = 플레이어 선공

    // ─── 선공 결정 연출 대기 시간 ────────────────────────────
    [SerializeField] private float pickFirstAttackDelay = 1f;
    [SerializeField] private float firstAttackResultDisplayTime = 1.5f;

    // ─── 턴 시작 배너 대기 시간 (UIManager의 fadeDuration×2 + displayTime과 맞출 것) ──
    [SerializeField] private float turnStartDelay = 2.5f;

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
        ChangeState(GameState.GameInit);
    }

    // ─── 외부에서 상태 전환 요청 시 사용 ─────────────────────
    public void ChangeState(GameState newState)
    {
        Debug.Log($"[TurnManager] {CurrentState} → {newState}");
        CurrentState = newState;
        IsInputBlocked = newState != GameState.PlayerUnitSelect
                      && newState != GameState.PlayerActionSelect;
        OnStateChanged?.Invoke(newState);
        HandleState(newState);
    }

    private void HandleState(GameState state)
    {
        switch (state)
        {
            case GameState.GameInit:            OnGameInit(); break;
            case GameState.PickFirstAttack:     StartCoroutine(PickFirstAttackRoutine()); break;
            case GameState.PlayerTurnStart:     OnPlayerTurnStart(); break;
            case GameState.PlayerTurnEnd:       OnPlayerTurnEnd(); break;
            case GameState.EnemyTurnStart:      OnEnemyTurnStart(); break;
            case GameState.LLMBuildingGameData: StartCoroutine(LLMPipelineRoutine()); break;
            case GameState.LLMFallback:         EnemyAIManager.Instance.ExecuteFallbackAI(); break;
            case GameState.Victory:             onGameOver(true); break;
            case GameState.Defeat:              onGameOver(false); break;
        }
    }

    // ─── GameInit ────────────────────────────────────────────
    private void OnGameInit()
    {
        TurnCount = 0;
        Debug.Log("[TurnManager] GameInit: 씬 로드 및 유닛·코어 배치 완료");

        // LLM 시스템 초기화
        if (GameStateSerializer.Instance != null)
            GameStateSerializer.Instance.InitializeUnitIds();
        else
            Debug.LogWarning("[TurnManager] GameStateSerializer가 씬에 없습니다. LLM 기능이 비활성화됩니다.");

        // 게임 규칙 사전 전달 (백그라운드, 논블로킹)
        if (GeminiAPIManager.Instance != null)
            GeminiAPIManager.Instance.InitializeWithGameRules();

        ChangeState(GameState.PickFirstAttack);
    }

    // ─── PickFirstAttack ─────────────────────────────────────
    private IEnumerator PickFirstAttackRoutine()
    {
        Debug.Log("[TurnManager] 선공 결정 중...");

        // UI 연출(코인 애니메이션 등)을 위한 대기
        yield return new WaitForSeconds(pickFirstAttackDelay);

        // 50:50 랜덤 선공 결정
        IsPlayerTurn = UnityEngine.Random.value >= 0.5f;

        string first = IsPlayerTurn ? "플레이어" : "적";
        Debug.Log($"[TurnManager] '{first}' 선공 결정!");

        // UI가 결과를 표시할 수 있도록 이벤트 발행
        OnFirstAttackDecided?.Invoke(IsPlayerTurn);

        // 결과 표시 후 실제 턴 시작
        yield return new WaitForSeconds(firstAttackResultDisplayTime);

        ChangeState(IsPlayerTurn ? GameState.PlayerTurnStart : GameState.EnemyTurnStart);
    }

    // ─── PlayerTurnStart ─────────────────────────────────────
    private void OnPlayerTurnStart()
    {
        TurnCount++;
        IsPlayerTurn = true;
        Debug.Log($"[TurnManager] 플레이어 턴 시작 (턴 {TurnCount})");

        foreach (Unit unit in FindObjectsOfType<Unit>())
            if (unit.team == "Player") unit.UpdateTurnState();

        if (TurnCount > 1)
        {
            if (GiantSlime.Instance != null) GiantSlime.Instance.OnRoundPassed();
            if (BlackMage.Instance != null)  BlackMage.Instance.OnRoundPassed();
        }

        StartCoroutine(DelayThenChangeState(GameState.PlayerUnitSelect));
    }

    // ─── PlayerTurnEnd ───────────────────────────────────────
    private void OnPlayerTurnEnd()
    {
        // 방금 행동한 유닛 마킹
        Unit actedUnit = BattleManager.Instance.activeUnit;
        if (actedUnit != null && !actedUnit.hasActedThisTurn)
        {
            actedUnit.hasActedThisTurn = true;
            actedUnit.SetActedVisual(true);
        }

        // 아직 행동하지 않은 플레이어 유닛이 있으면 선택 단계로 복귀
        foreach (Unit unit in FindObjectsOfType<Unit>())
        {
            if (unit.team == "Player" && unit.gameObject.activeInHierarchy
                && unit.currentHp > 0 && !unit.hasActedThisTurn)
            {
                Debug.Log($"[TurnManager] 미행동 유닛 존재 — 다음 유닛 선택");
                ChangeState(GameState.PlayerUnitSelect);
                return;
            }
        }

        Debug.Log($"[TurnManager] 모든 플레이어 유닛 행동 완료 (턴 {TurnCount}) — 턴 종료 대기");
    }

    // ─── TurnEnd 버튼 클릭 시 호출 (UIManager에서 연결) ─────────────────
    /*
    public void OnTurnEndButtonClicked()
    {
        if (CurrentState != GameState.PlayerTurnEnd) return;
        ChangeState(GameState.EnemyTurnStart);
    }
    */

    public void OnTurnEndButtonClicked() // 0523 LJSS 수정 : 행동하기 싫을 때 턴 강제 종료
    {
        if (CurrentState == GameState.PlayerUnitSelect ||
            CurrentState == GameState.PlayerActionSelect ||
            CurrentState == GameState.PlayerTurnEnd)
        {
            Debug.Log("[TurnManager] 사용자가 강제로 턴 종료 버튼을 눌렀습니다.");

            // 필요하다면 여기서 선택된 유닛(activeUnit)의 선택 상태를 초기화하는 로직을 추가할 수도 있습니다.

            ChangeState(GameState.EnemyTurnStart);
        }
        else
        {
            Debug.Log("[TurnManager] 현재 턴을 강제 종료할 수 없는 상태입니다.");
        }
    }

    private void OnEnemyTurnStart()
    {
        TurnCount++;
        IsPlayerTurn = false;
        Debug.Log($"[TurnManager] 적 턴 시작 (턴 {TurnCount})");

        foreach (Unit unit in FindObjectsOfType<Unit>())
            if (unit.team == "Enemy") unit.UpdateTurnState();

        StartCoroutine(DelayThenStartEnemyAction());
    }

    private IEnumerator DelayThenStartEnemyAction()
    {
        yield return new WaitForSeconds(turnStartDelay);

        if (GeminiAPIManager.Instance != null &&
            GameStateSerializer.Instance != null &&
            LLMActionParser.Instance != null &&
            LLMActionExecutor.Instance != null)
        {
            ChangeState(GameState.LLMBuildingGameData);
        }
        else
        {
            Debug.LogWarning("[TurnManager] LLM 컴포넌트 누락 → Fallback AI 실행");
            ChangeState(GameState.LLMFallback);
        }
    }

    private IEnumerator DelayThenChangeState(GameState nextState)
    {
        yield return new WaitForSeconds(turnStartDelay);
        ChangeState(nextState);
    }

    // ─── LLM 파이프라인 ─────────────────────────────────────────────────
    private IEnumerator LLMPipelineRoutine()
    {
        // 1단계: 게임 상태 직렬화 (LLMBuildingGameData 상태에서 실행)
        string gameStateJson = GameStateSerializer.Instance.SerializeCurrentGameState();
        Debug.Log($"[TurnManager] 게임 상태 직렬화 완료 ({gameStateJson.Length} chars)");
        // ※ BeginLLMTurn은 GeminiAPIManager.RequestRoutine 내부에서 호출됩니다.
        yield return null;

        // 2단계: LLM API 요청
        ChangeState(GameState.LLMRequesting);
        string rawResponse = null;
        bool requestDone = false;
        bool requestFailed = false;

        GeminiAPIManager.Instance.RequestEnemyAction(
            gameStateJson,
            resp => { rawResponse = resp; requestDone = true; },
            ()   => { requestFailed = true; requestDone = true; }
        );

        while (!requestDone) yield return null;

        if (requestFailed || string.IsNullOrEmpty(rawResponse))
        {
            Debug.LogWarning("[TurnManager] LLM 요청 실패 → Fallback AI 전환");
            LLMLogger.Instance.LogResult(rawResponse, null, "API 요청 실패");
            ChangeState(GameState.LLMFallback);
            yield break;
        }

        // 3단계: 응답 수신
        ChangeState(GameState.LLMResponseReceived);

        // 4단계: 파싱 및 검증
        ChangeState(GameState.LLMValidating);
        List<EnemyActionData> actions = LLMActionParser.Instance.ParseAndValidate(rawResponse);

        if (actions == null || actions.Count == 0)
        {
            Debug.LogWarning("[TurnManager] LLM 응답 검증 실패 → Fallback AI 전환");
            LLMLogger.Instance.LogResult(rawResponse, null, "파싱·검증 실패");
            ChangeState(GameState.LLMFallback);
            yield break;
        }

        LLMLogger.Instance.LogResult(rawResponse, actions[0]);

        // 5단계: 행동 실행
        ChangeState(GameState.EnemyActionExecute);
        yield return StartCoroutine(LLMActionExecutor.Instance.ExecuteActions(actions));

        // 6단계: 플레이어 턴으로 전환
        ChangeState(GameState.PlayerTurnStart);
    }

    private IEnumerator SkipEnemyTurnRoutine() // 0523 LJSS 추가 : 적 AI 미구현으로 인해 스킬테스트를 위해 추가 
    {
        Debug.Log("적 AI 미구현 : 1초 대기 후플레이어 턴으로 넘어가기");
        yield return new WaitForSeconds(1.0f);
        ChangeState(GameState.PlayerTurnStart);
    }

    // blackMage 접근 위해 설정
    public void SetInputBlocked(bool isBlocked)
    {
        IsInputBlocked = isBlocked;
    }

    // GameOver Case1 : Core break

    private void onGameOver(bool isVictory)
    {
        UIManager.Instance.ShowGameOver(isVictory);
    }

    // GameOver Case2 : 유닛 사망

    public void CheckUnitDeathWinCondition()
    {
        if (CurrentState == GameState.Victory || CurrentState == GameState.Defeat) return;

        Unit[] allUnits = FindObjectsOfType<Unit>(true); // 비활성화된 유닛 포함 모든 유닛 찾기
        
        bool isPlayerAlive = false;
        bool isEnemyAlive = false;

        foreach (Unit unit in allUnits)
        {
            // hierarchy 상에서 활성화되어 있고 체력이 0보다 크다면 살아있는 것
            if (unit.gameObject.activeInHierarchy && unit.currentHp > 0)
            {
                if (unit.team == "Player") isPlayerAlive = true;
                if (unit.team == "Enemy") isEnemyAlive = true;
            }
        }

        // 💡 조건 판정
        if (!isPlayerAlive) ChangeState(GameState.Defeat); // 아군 전멸 -> 패배
        else if (!isEnemyAlive) ChangeState(GameState.Victory); // 적군 전멸 -> 승리
    }
}
