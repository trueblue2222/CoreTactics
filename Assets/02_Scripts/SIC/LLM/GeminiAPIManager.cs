using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Gemini REST API 통신 담당.
// API 키는 Assets/StreamingAssets/gemini_key.txt 에 저장하세요 (git 제외).
// 필수: Package Manager → Add package by name → com.unity.nuget.newtonsoft-json
public class GeminiAPIManager : MonoBehaviour
{
    public static GeminiAPIManager Instance { get; private set; }

    // API 키는 코드/씬에 직접 입력하지 마세요.
    // Assets/StreamingAssets/gemini_key.txt 파일에 키만 한 줄로 저장하면 자동으로 읽습니다.
    private string apiKey = "";
    [SerializeField] private string modelName = "gemini-2.5-flash";
    [SerializeField] [Range(5f, 30f)] private float timeoutSeconds = 15f;
    [SerializeField] [Range(0f, 1f)] private float temperature = 0.3f;
    // thinking 토큰과 실제 응답 토큰이 이 예산을 공유합니다.
    // thinking 비활성화 시 512로도 충분하지만, 활성화 상태라면 8192 이상 권장.
    [SerializeField] private int maxOutputTokens = 8192;
    // 0 = thinking 비활성화 (게임 AI 응답은 단순 JSON이므로 thinking 불필요)
    // -1 = 모델 기본값 사용
    [SerializeField] private int thinkingBudget = 0;

    [Header("Rate Limit 처리")]
    [SerializeField] private bool enableInitRequest = false;
    [SerializeField] private int maxRetries = 2;
    [SerializeField] private float retryDelaySec = 5f;

    [Header("디버그")]
    [SerializeField] private bool logSystemPrompt = false; // 시스템 프롬프트는 고정이므로 기본 off
    [SerializeField] private bool logUserMessage   = true;  // 게임 상태 + 요청 내용 출력

    private string cachedSystemPrompt;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        LoadApiKey();
    }

    void Start()
    {
        // 씬 이름으로 BigObject를 자동 감지합니다.
        // 타이틀 화면 없이 게임 씬을 직접 실행해도 올바른 프롬프트가 생성됩니다.
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName.Contains("BlackMage"))
            GameConfig.SelectedBigObject = GameConfig.BigObjectType.BlackMage;
        else if (sceneName.Contains("Slime"))
            GameConfig.SelectedBigObject = GameConfig.BigObjectType.GiantSlime;

        cachedSystemPrompt = BuildSystemPrompt();

        if (logSystemPrompt)
            Debug.Log($"[Gemini] 시스템 프롬프트 빌드 완료 (BigObject: {GameConfig.SelectedBigObject})");
    }

    private void LoadApiKey()
    {
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, "gemini_key.txt");
        if (System.IO.File.Exists(path))
        {
            apiKey = System.IO.File.ReadAllText(path).Trim();
            Debug.Log("[Gemini] API 키 로드 완료 (StreamingAssets/gemini_key.txt)");
        }
        else
        {
            Debug.LogError("[Gemini] API 키 파일 없음: Assets/StreamingAssets/gemini_key.txt\n" +
                           "해당 파일을 생성하��� API 키를 한 줄로 입력하세요.");
        }
    }

    // ─── 게임 시작 시 규칙 사전 전달 (백그라운드, 논블로킹) ───────────────
    public void InitializeWithGameRules()
    {
        if (!enableInitRequest)
        {
            Debug.Log("[Gemini] 초기화 요청 비활성화됨. 쿼터 절약.");
            return;
        }
        StartCoroutine(InitRoutine());
    }

    private IEnumerator InitRoutine()
    {
        Debug.Log("[Gemini] 게임 규칙 사전 전달 시작...");
        yield return StartCoroutine(PostRequest(
            "Acknowledge you understand the rules. Respond: {\"ready\": true}",
            _ => Debug.Log("[Gemini] 초기화 성공 — API 연결 확인됨"),
            () => Debug.LogWarning("[Gemini] 초기화 실패 — API 키 또는 네트워크를 확인하세요.")
        ));
    }

    // ─── 적 턴 행동 요청 ──────────────────────────────────────────────────
    public void RequestEnemyAction(string gameStateJson, Action<string> onSuccess, Action onFailure)
    {
        StartCoroutine(RequestRoutine(gameStateJson, onSuccess, onFailure));
    }

    private IEnumerator RequestRoutine(string gameStateJson, Action<string> onSuccess, Action onFailure)
    {
        // 로그 세션의 턴 폴더를 여기서 생성 + gamestate 저장
        // (PostRequest보다 반드시 먼저 호출되어야 _currentTurnPath가 세팅됨)
        LLMLogger.Instance.BeginLLMTurn(gameStateJson);

        string userMsg =
            $"Current game state (JSON):\n{gameStateJson}\n\n" +
            "Choose the best action for ONE enemy unit this turn. " +
            "Respond with a single JSON object only — no explanation, no markdown.";

        yield return StartCoroutine(PostRequest(userMsg, onSuccess, onFailure, logForSession: true));
    }

    // ─── HTTP POST 핵심 로직 (429 자동 재시도 포함) ─────────────────────
    private IEnumerator PostRequest(string userMessage, Action<string> onSuccess, Action onFailure, bool logForSession = false)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

        // Newtonsoft.Json으로 요청 본문 직렬화 — 특수문자 이스케이프 보장
        string bodyJson = BuildRequestJson(userMessage);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(bodyJson);

        if (logForSession) LLMLogger.Instance.LogAPIRequest(bodyJson);

        if (logSystemPrompt)
            Debug.Log($"[Gemini] ── System Prompt ──────────────────────\n{cachedSystemPrompt}\n────────────────────────────────────────────");
        if (logUserMessage)
            Debug.Log($"[Gemini] ── User Message ───────────────────────\n{userMessage}\n────────────────────────────────────────────");

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (attempt > 0)
            {
                Debug.Log($"[Gemini] {retryDelaySec}초 대기 후 재시도 ({attempt}/{maxRetries})...");
                yield return new WaitForSeconds(retryDelaySec);
            }

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler   = new UploadHandlerRaw(bodyBytes);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = Mathf.RoundToInt(timeoutSeconds);

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    string raw = req.downloadHandler.text;
                    string extracted = ExtractText(raw);
                    if (extracted != null)
                    {
                        if (logForSession) LLMLogger.Instance.LogAPIResponse(raw, extracted);
                        Debug.Log($"[Gemini] 응답 수신: {extracted}");
                        onSuccess?.Invoke(extracted);
                    }
                    else
                    {
                        if (logForSession) LLMLogger.Instance.LogAPIResponse(raw, null);
                        Debug.LogWarning($"[Gemini] text 추출 실패. 원본 응답:\n{raw}");
                        onFailure?.Invoke();
                    }
                    yield break;
                }

                if (req.responseCode == 429 && attempt < maxRetries)
                {
                    Debug.LogWarning($"[Gemini] 429 Too Many Requests — {retryDelaySec}초 후 재시도 ({attempt + 1}/{maxRetries})");
                    continue;
                }

                Debug.LogWarning($"[Gemini] 요청 실패 (HTTP {req.responseCode}): {req.error}\n{req.downloadHandler?.text}");
                onFailure?.Invoke();
                yield break;
            }
        }
    }

    // ─── 요청 JSON 빌드 (Newtonsoft.Json) ────────────────────────────────
    private string BuildRequestJson(string userMessage)
    {
        var requestObj = new JObject
        {
            ["system_instruction"] = new JObject
            {
                ["parts"] = new JArray { new JObject { ["text"] = cachedSystemPrompt } }
            },
            ["contents"] = new JArray
            {
                new JObject
                {
                    ["role"] = "user",
                    ["parts"] = new JArray { new JObject { ["text"] = userMessage } }
                }
            },
            ["generationConfig"] = BuildGenerationConfig()
        };

        return requestObj.ToString(Formatting.None);
    }

    // ─── generationConfig 빌드 ────────────────────────────────────────────
    private JObject BuildGenerationConfig()
    {
        var cfg = new JObject
        {
            ["responseMimeType"] = "application/json",
            ["temperature"]      = Math.Round((double)temperature, 2),
            ["maxOutputTokens"]  = maxOutputTokens
        };

        // thinkingBudget >= 0 이면 thinkingConfig 포함
        // 0 = thinking 비활성화, 양수 = 해당 토큰 수만큼 허용
        if (thinkingBudget >= 0)
            cfg["thinkingConfig"] = new JObject { ["thinkingBudget"] = thinkingBudget };

        return cfg;
    }

    // ─── 응답 파싱 (Newtonsoft.Json) ─────────────────────────────────────
    // Gemini 2.5 Flash는 내부 추론(thought)을 parts[0]에, 실제 응답을 그 뒤에 담습니다.
    // thought 파트를 건너뛰고 첫 번째 실제 응답 text를 반환합니다.
    private string ExtractText(string rawJson)
    {
        try
        {
            JObject root = JObject.Parse(rawJson);
            JArray parts = root["candidates"]?[0]?["content"]?["parts"] as JArray;

            if (parts == null)
            {
                Debug.LogWarning($"[Gemini] parts 필드 없음. 원본:\n{rawJson}");
                return null;
            }

            foreach (JToken part in parts)
            {
                // thought:true 파트는 내부 추론 — 건너뜁니다
                bool isThought = part["thought"]?.Value<bool>() ?? false;
                if (isThought) continue;

                string text = part["text"]?.ToString();
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }

            Debug.LogWarning($"[Gemini] 유효한 응답 파트 없음. 원본:\n{rawJson}");
            return null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Gemini] 응답 JSON 파싱 오류: {e.Message}\n원본:\n{rawJson}");
            return null;
        }
    }

    // ─── 시스템 프롬프트 ─────────────────────────────────────────────────
    private string BuildSystemPrompt() =>
        BuildBasePrompt() + "\n\n" + BuildBigObjectRules();

    private string BuildBasePrompt() =>
"You are an AI enemy controller in a turn-based grid strategy game.\n" +
"Your goal: destroy the PLAYER's Core (reduce its HP to 0). Protect your own Core.\n\n" +
"=== UNIT CLASSES ===\n" +
"Warrior  : high HP/ATK, attackRange=1 (melee), moveRange=3.\n" +
"           Skill=Dash (cooldown 2): charge in one cardinal direction up to 4 cells, dealing 20 damage to everything in the path.\n" +
"           Needs dashDestination: same row OR column as warrior, within 4 cells, landing cell must be empty.\n\n" +
"Archer   : medium stats, attackRange=3, moveRange=2.\n" +
"           Skill=SniperMode (cooldown 2): +1 attackRange, moveRange=0 for 2 turns. No extra params.\n\n" +
"Magician : low HP, high ATK, attackRange=2, moveRange=2.\n" +
"           Skill=Teleport (cooldown 3): move any unit to a new cell.\n" +
"           Needs skillTargetId + skillDestination (empty cell).\n\n" +
"=== UNIT STATE FIELDS ===\n" +
"isRooted=true  : unit is trapped by slime — cannot move, Warrior cannot use Dash skill.\n" +
"isSniperMode   : Archer is in sniper mode — cannot move but has extended attack range.\n\n" +
"=== ACTIONS (pick exactly one) ===\n" +
"move   : move to a cell in reachableCells → set moveTarget:{x,y}\n" +
"attack : attack a target → set attackTargetId (MUST be from attackableTargetIds)\n" +
"skill  : use special ability (skillCooldown must be 0)\n" +
"skip   : do nothing\n\n" +
"=== OUTPUT FORMAT (JSON only, no markdown) ===\n" +
"{\"unitId\":\"enemy_warrior_0\",\"actionType\":\"move\",\"moveTarget\":{\"x\":5,\"y\":3}}\n" +
"{\"unitId\":\"enemy_archer_0\",\"actionType\":\"attack\",\"attackTargetId\":\"player_warrior_0\"}\n" +
"{\"unitId\":\"enemy_warrior_0\",\"actionType\":\"attack\",\"attackTargetId\":\"player_core\"}\n" +
"{\"unitId\":\"enemy_warrior_0\",\"actionType\":\"skill\",\"dashDestination\":{\"x\":7,\"y\":3}}\n" +
"{\"unitId\":\"enemy_archer_0\",\"actionType\":\"skill\"}\n" +
"{\"unitId\":\"enemy_magician_0\",\"actionType\":\"skill\",\"skillTargetId\":\"player_warrior_0\",\"skillDestination\":{\"x\":8,\"y\":5}}\n" +
"{\"unitId\":\"enemy_warrior_0\",\"actionType\":\"skip\"}\n\n" +
"=== STRATEGY ===\n" +
"1. Attack player Core if reachable.\n" +
"2. Eliminate low-HP player units.\n" +
"3. Use skills when advantageous.\n" +
"4. Move toward player Core.\n\n" +
"=== CONSTRAINTS ===\n" +
"- moveTarget MUST be in reachableCells.\n" +
"- attackTargetId MUST be in attackableTargetIds (pre-calculated, no guessing needed).\n" +
"- If attackableTargetIds is empty, do NOT use attack — choose move or skill instead.\n" +
"- skill requires skillCooldown == 0.\n" +
"- Warrior dashDestination: same row/column, within 4 cells, empty.\n" +
"- Magician skillDestination: empty cell.\n" +
"- isRooted=true units: do NOT use move or Warrior skill — choose attack or skip instead.";

    private string BuildBigObjectRules() =>
        GameConfig.SelectedBigObject switch
        {
            GameConfig.BigObjectType.GiantSlime => BuildGiantSlimeRules(),
            GameConfig.BigObjectType.BlackMage  => BuildBlackMageRules(),
            _                                   => ""
        };

    private string BuildGiantSlimeRules() =>
"=== MAP GIMMICK: GIANT SLIME ===\n" +
"Every 3 rounds, the Giant Slime spawns SlimePuddles on 2 random empty tiles.\n\n" +
"bigObject fields:\n" +
"  type             = \"GiantSlime\"\n" +
"  cooldownRemaining = rounds until next puddle spawn (3 → 2 → 1 → spawns → resets to 3)\n" +
"  slimePuddles     = list of {x,y} positions with active slime on the map\n\n" +
"SLIME RULES:\n" +
"- A unit that moves onto a slimed tile becomes ROOTED (isRooted=true) for 1 turn.\n" +
"- The puddle disappears after a unit steps on it.\n" +
"- Rooted units: cannot move, Warrior cannot use Dash.\n" +
"- Magician CAN teleport rooted units — skillTargetId may be a rooted unit.\n\n" +
"STRATEGY vs SLIME:\n" +
"- Check slimePuddles before choosing moveTarget — avoid slimed tiles for your own units.\n" +
"- If a player unit is rooted (isRooted=true), prioritize attacking it (easy target).\n" +
"- Use Magician Teleport to move an enemy unit that is rooted to a better position.\n" +
"- Use Magician Teleport to drop a player unit onto a slimed tile intentionally.";

    private string BuildBlackMageRules() =>
"=== MAP GIMMICK: BLACK MAGE ===\n" +
"Every 3 rounds, the Black Mage teleports one random unit from EACH team to a random tile.\n\n" +
"bigObject fields:\n" +
"  type              = \"BlackMage\"\n" +
"  cooldownRemaining = rounds until next teleport (counts down 3 → 2 → 1 → 0 → teleports)\n" +
"  isWarningPhase    = true when teleport happens NEXT round (cooldownRemaining=1)\n" +
"  warnedEnemyUnitId  = ID of the enemy unit that WILL be teleported next round (warning only)\n" +
"  warnedPlayerUnitId = ID of the player unit that WILL be teleported next round (warning only)\n" +
"  enemyTeleportDest  = {x,y} where the warned enemy unit will land (warning only)\n" +
"  playerTeleportDest = {x,y} where the warned player unit will land (warning only)\n\n" +
"BLACK MAGE RULES:\n" +
"- Teleport destinations are always empty, passable tiles — no choice in destination.\n" +
"- Both teams are affected simultaneously; you cannot prevent it.\n" +
"- Teleport ignores isRooted state — a rooted unit can still be teleported.\n\n" +
"STRATEGY vs BLACK MAGE:\n" +
"- When isWarningPhase=true: if warnedEnemyUnitId is a key attacker, delay committing it\n" +
"  to a multi-step plan since its position will change next round.\n" +
"- When isWarningPhase=true: if warnedPlayerUnitId is your current attack target,\n" +
"  prioritize finishing it this round before it teleports away.\n" +
"- cooldownRemaining=1 (warning) and cooldownRemaining=0 (imminent) both signal caution.";
}
