// 나중에 필요 없는 상태 지우거나 필요한 상태 마음대로 선언하셔도 상관 없습니다!!!

public enum GameState
{
    // ─── 게임 초기화 ───────────────────────────────────────
    None,                   // 초기값, 혹은 미설정 상태
    GameInit,               // 씬 로드, 유닛·코어 배치
    PickFirstAttack,        // 선공 랜덤 결정

    // ─── 플레이어(아군) 턴 ─────────────────────────────────
    PlayerTurnStart,        // 아군 턴 시작 (UI 갱신, AP 초기화 등)
    PlayerUnitSelect,       // 행동할 유닛 선택 대기
    PlayerActionSelect,     // 행동 종류 선택 (이동 / 공격 / 스킬 / 대기)
    PlayerActionExecute,    // 선택한 행동 실행 및 애니메이션
    PlayerTurnEnd,          // 아군 턴 종료 (유닛 상태 정리)

    // ─── 적(LLM) 턴 ────────────────────────────────────────
    EnemyTurnStart,         // 적 턴 시작

    // LLM 호출 파이프라인
    LLMBuildingGameData,   // GameData JSON 직렬화 (validMoves / Targets 포함)
    LLMRequesting,          // API 호출 중 — 응답 대기 (로딩 표시)
    LLMResponseReceived,    // 응답 수신 완료, 파싱 준비
    LLMValidating,          // 검증: 형식 / unitId / validMoves·Targets / timeout
    LLMFallback,            // 검증 실패 → Fallback AI로 대체 행동 결정

    EnemyActionExecute,     // 결정된 행동 실행 및 애니메이션
    EnemyTurnEnd,           // 적 턴 종료

    // ─── 승패 판정 ─────────────────────────────────────────
    CheckEnemyCore,         // 적 진영 코어 파괴 여부 확인 → Victory 분기
    CheckAllyCore,          // 아군 진영 코어 파괴 여부 확인 → Defeat 분기

    // ─── 종료 ──────────────────────────────────────────────
    Victory,
    Defeat,

    // ─── 기타 ──────────────────────────────────────────────
    Paused,                 // 게임 일시정지
}