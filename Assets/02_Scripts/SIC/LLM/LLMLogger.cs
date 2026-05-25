using System;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// 적 턴마다 LLM과 주고받는 JSON을 프로젝트 루트의 LLMLogs/ 폴더에 저장합니다.
// 세션 폴더명 = 게임 시작 시각 (yyyy-MM-dd_HH-mm-ss)
// 턴 폴더명   = turn_XXX_TYYY  (XXX: LLM 호출 순번, YYY: 게임 턴 카운트)
public class LLMLogger : MonoBehaviour
{
    public static LLMLogger Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("LLMLogger");
                _instance = go.AddComponent<LLMLogger>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    private static LLMLogger _instance;

    private string _sessionPath;
    private string _currentTurnPath;
    private int _llmCallIndex;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitSession();
    }

    private void InitSession()
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        _sessionPath = Path.Combine(projectRoot, "LLMLogs", timestamp);

        try
        {
            Directory.CreateDirectory(_sessionPath);
            Debug.Log($"[LLMLogger] 세션 로그 폴더 생성: {_sessionPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LLMLogger] 폴더 생성 실패: {e.Message}");
            _sessionPath = null;
        }
    }

    // ─── GeminiAPIManager.RequestRoutine에서 PostRequest 직전에 호출 ──────
    public void BeginLLMTurn(string gameStateJson)
    {
        if (_sessionPath == null) return;

        _llmCallIndex++;
        int turnCount = TurnManager.Instance != null ? TurnManager.Instance.TurnCount : 0;
        string folderName = $"turn_{_llmCallIndex:D3}_T{turnCount}";
        _currentTurnPath = Path.Combine(_sessionPath, folderName);

        try
        {
            Directory.CreateDirectory(_currentTurnPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LLMLogger] 턴 폴더 생성 실패: {e.Message}");
            _currentTurnPath = null;
            return;
        }

        WriteToTurn("1_gamestate.json", PrettyJson(gameStateJson));
    }

    // ─── GeminiAPIManager에서 HTTP 요청 직전 호출 ────────────────────────
    public void LogAPIRequest(string requestBodyJson)
    {
        WriteToTurn("2_api_request.json", PrettyJson(requestBodyJson));
    }

    // ─── GeminiAPIManager에서 HTTP 응답 수신 직후 호출 ───────────────────
    public void LogAPIResponse(string rawResponseJson, string extractedText)
    {
        var obj = new JObject
        {
            ["extractedText"] = extractedText,
            ["rawResponse"]   = TryParseToken(rawResponseJson),
            ["timestamp"]     = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
        };
        WriteToTurn("3_api_response.json", obj.ToString(Formatting.Indented));
    }

    // ─── TurnManager에서 검증 결과 확정 후 호출 ──────────────────────────
    // action이 null이면 검증 실패 (fallback 전환)
    public void LogResult(string llmText, EnemyActionData action, string failReason = null)
    {
        bool success = action != null;
        var obj = new JObject
        {
            ["success"]      = success,
            ["llmText"]      = llmText,
            ["timestamp"]    = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
        };

        if (success)
            obj["parsedAction"] = TryParseToken(JsonUtility.ToJson(action));
        else
        {
            obj["fallback"] = true;
            obj["reason"]   = failReason ?? "unknown";
        }

        WriteToTurn("4_action_result.json", obj.ToString(Formatting.Indented));
    }

    // ─── 내부 유틸 ───────────────────────────────────────────────────────
    private void WriteToTurn(string fileName, string content)
    {
        if (_currentTurnPath == null) return;
        try
        {
            File.WriteAllText(
                Path.Combine(_currentTurnPath, fileName),
                content,
                System.Text.Encoding.UTF8);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LLMLogger] 저장 실패 ({fileName}): {e.Message}");
        }
    }

    private static string PrettyJson(string json)
    {
        try { return JToken.Parse(json).ToString(Formatting.Indented); }
        catch { return json; }
    }

    private static JToken TryParseToken(string json)
    {
        try { return JToken.Parse(json); }
        catch { return new JValue(json); }
    }
}
