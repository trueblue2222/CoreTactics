using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Gemini REST API 통신 담당.
// Inspector에서 apiKey를 반드시 설정하세요.
// 필수: Package Manager → Add package by name → com.unity.nuget.newtonsoft-json
public class GeminiAPIManager : MonoBehaviour
{
    public static GeminiAPIManager Instance { get; private set; }

    [SerializeField] private string apiKey = "YOUR_GEMINI_API_KEY_HERE";
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
        cachedSystemPrompt = BuildSystemPrompt();
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
        string userMsg =
            $"Current game state (JSON):\n{gameStateJson}\n\n" +
            "Choose the best action for ONE enemy unit this turn. " +
            "Respond with a single JSON object only — no explanation, no markdown.";

        yield return StartCoroutine(PostRequest(userMsg, onSuccess, onFailure));
    }

    // ─── HTTP POST 핵심 로직 (429 자동 재시도 포함) ─────────────────────
    private IEnumerator PostRequest(string userMessage, Action<string> onSuccess, Action onFailure)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

        // Newtonsoft.Json으로 요청 본문 직렬화 — 특수문자 이스케이프 보장
        string bodyJson = BuildRequestJson(userMessage);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(bodyJson);

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
                        Debug.Log($"[Gemini] 응답 수신: {extracted}");
                        onSuccess?.Invoke(extracted);
                    }
                    else
                    {
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
"- Magician skillDestination: empty cell.";
}
