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
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
        HandleState(newState);
    }

    private void HandleState(GameState state)
    {
        switch (state)
        {
            case GameState.GameInit:       OnGameInit();                              break;
            case GameState.PickFirstAttack: StartCoroutine(PickFirstAttackRoutine()); break;
            case GameState.PlayerTurnStart: OnPlayerTurnStart();                      break;
            case GameState.EnemyTurnStart:  OnEnemyTurnStart();                       break;
        }
    }

    // ─── GameInit ────────────────────────────────────────────
    private void OnGameInit()
    {
        TurnCount = 0;
        Debug.Log("[TurnManager] GameInit: 씬 로드 및 유닛·코어 배치 완료");

        // 배치가 완료된 뒤 선공 결정 단계로 진입
        // 유닛 배치 로직이 비동기라면 외부에서 ChangeState(PickFirstAttack)을 호출할 것
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
    }

    // ─── EnemyTurnStart ──────────────────────────────────────
    private void OnEnemyTurnStart()
    {
        TurnCount++;
        IsPlayerTurn = false;
        Debug.Log($"[TurnManager] 적 턴 시작 (턴 {TurnCount})");
    }
}
