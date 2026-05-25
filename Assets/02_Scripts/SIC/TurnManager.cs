using System;
using System.Collections;
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
    private void OnPlayerTurnStart() // 0523 LJSS 수정 : 턴 시작 시 맵에 존재하는 모든 Unit script의 UpdateTurnState() 호출하여 턴 상태 업데이트
    {
        TurnCount++;
        IsPlayerTurn = true;
        Debug.Log($"[TurnManager] 플레이어 턴 시작 (턴 {TurnCount})");

        Unit[] allUnits = FindObjectsOfType<Unit>(); // 맵에 존재하는 모든 Unit script 참조

        foreach (Unit unit in allUnits) // 모든 유닛의 턴 상태 업데이트
        {
            if (unit.team == "Player")
            {
                unit.UpdateTurnState();
            }
        }

        // UI 갱신·AP 초기화 등 턴 시작 처리가 추가될 경우 여기서 수행
        ChangeState(GameState.PlayerUnitSelect);
    }

    // ─── PlayerTurnEnd ───────────────────────────────────────
    private void OnPlayerTurnEnd()
    {
        Debug.Log($"[TurnManager] 플레이어 턴 종료 (턴 {TurnCount}) — TurnEnd 버튼 대기 중");
        // EnemyTurnStart 전환은 UIManager의 TurnEnd 버튼이 담당
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

        // LLM 파이프라인 시작 (GeminiAPIManager가 없으면 Fallback)
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
        EnemyActionData action = LLMActionParser.Instance.ParseAndValidate(rawResponse);

        if (action == null)
        {
            Debug.LogWarning("[TurnManager] LLM 응답 검증 실패 → Fallback AI 전환");
            LLMLogger.Instance.LogResult(rawResponse, null, "파싱·검증 실패");
            ChangeState(GameState.LLMFallback);
            yield break;
        }

        LLMLogger.Instance.LogResult(rawResponse, action);

        // 5단계: 행동 실행
        ChangeState(GameState.EnemyActionExecute);
        yield return StartCoroutine(LLMActionExecutor.Instance.ExecuteAction(action));

        // 6단계: 플레이어 턴으로 전환
        ChangeState(GameState.PlayerTurnStart);
    }

    private IEnumerator SkipEnemyTurnRoutine() // 0523 LJSS 추가 : 적 AI 미구현으로 인해 스킬테스트를 위해 추가 
    {
        Debug.Log("적 AI 미구현 : 1초 대기 후플레이어 턴으로 넘어가기");
        yield return new WaitForSeconds(1.0f);
        ChangeState(GameState.PlayerTurnStart);
    }
}
