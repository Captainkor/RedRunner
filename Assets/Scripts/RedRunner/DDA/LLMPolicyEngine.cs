using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace RedRunner.DDA
{

    /// <summary>
    /// Which LLM provider to use for the Policy Engine.
    /// </summary>
    public enum LLMProvider
    {
        /// <summary>
        /// Google Gemini API (default). Free tier: 10 RPM, ~1000 req/day.
        /// Get key at https://aistudio.google.com/apikey
        /// </summary>
        Gemini,

        /// <summary>
        /// Anthropic Claude API. Paid. ~$3/$15 per 1M tokens.
        /// Get key at https://console.anthropic.com
        /// </summary>
        Claude
    }

    /// <summary>
    /// PLAN phase of the MAPE-K loop.
    /// Replaces rule-based policy with LLM API calls following the DDA-MAPEKit paper.
    /// Uses SPAR prompt format: Situation, Purpose, Action, Examples, Request.
    ///
    /// Supports multiple LLM providers:
    /// - Gemini Flash (FREE tier, recommended for classroom demos)
    /// - Claude Sonnet (paid, higher quality)
    /// </summary>
    public class LLMPolicyEngine : MonoBehaviour
    {

        #region Fields

        [Header("LLM Provider")]
        [Space]
        [SerializeField]
        [Tooltip("Gemini = free tier available (recommended for demos). Claude = paid, higher quality.")]
        private LLMProvider m_Provider = LLMProvider.Gemini;

        [Header("API Configuration")]
        [Space]
        [SerializeField]
        [Tooltip("Gemini: get free key at https://aistudio.google.com/apikey\nClaude: get key at https://console.anthropic.com")]
        private string m_ApiKey = "";
        [SerializeField]
        [Tooltip("Override the default model. Leave empty to use provider default.\nGemini default: gemini-2.0-flash\nClaude default: claude-sonnet-4-5-20250929")]
        private string m_ModelIdOverride = "";
        [SerializeField]
        private int m_MaxTokens = 1024;

        [Header("Prompt Configuration")]
        [Space]
        [SerializeField]
        [TextArea(10, 30)]
        private string m_SystemPrompt =
@"# Situation
You are a Dynamic Difficulty Adjustment mechanism for a 2D platformer game called RedRunner. You receive player performance metrics, a symptom classification, and current game variable values with their allowed thresholds.

# Purpose
Generate a JSON object with adjusted float values for game variables. Adjust values to balance difficulty based on the player's performance symptom. Keep all values within their defined thresholds. The goal is to maintain player flow — not too easy, not too hard.

# Action
Follow these reasoning steps:
1. Read the symptom level to understand player state.
2. For each game variable, determine if it helps the player or harms the player:
   - Player-HELPING: jumpStrength, coinDensity (higher = easier)
   - Player-HARMING: enemyDensity, gapFrequency, runSpeed, sawProbability, spikeProbability, platformHeightVariance (higher = harder)
3. If symptom is low/very.low (player struggling):
   - Decrease player-harming variables
   - Increase player-helping variables
4. If symptom is high/sharply.high (player dominating):
   - Increase player-harming variables
   - Decrease player-helping variables
5. For slight symptoms (slightly.low, slightly.high): make smaller adjustments.
6. For normal: make no or minimal changes.
7. Consider the current values — prefer gradual changes (10-20% shifts) over sudden jumps.
8. Ensure all output values are within the threshold [min, max] for each variable.

Respond with ONLY a valid JSON object, no markdown, no explanation.";

        [Header("Example Buffer")]
        [Space]
        [SerializeField]
        private int m_MaxExampleBufferSize = 5;

        [Header("Current Profile")]
        [Space]
        [SerializeField]
        private DifficultyProfile m_CurrentProfile;

        [Header("Debug")]
        [Space]
        [SerializeField]
        private bool m_LogPrompts = false;
        [SerializeField]
        private bool m_LogResponses = false;

        private List<string> m_ExampleBuffer = new List<string>();
        private bool m_IsProcessing = false;

        #endregion

        #region Properties

        public DifficultyProfile CurrentProfile
        {
            get { return m_CurrentProfile; }
        }

        public bool IsProcessing
        {
            get { return m_IsProcessing; }
        }

        public string SystemPrompt
        {
            get { return m_SystemPrompt; }
            set { m_SystemPrompt = value; }
        }

        public LLMProvider Provider
        {
            get { return m_Provider; }
            set { m_Provider = value; }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sends a difficulty adjustment request to the LLM.
        /// Constructs the full SPAR prompt, calls the API, parses the response.
        /// </summary>
        /// <param name="metricsJson">Player metrics JSON from PlayerMetricsCollector</param>
        /// <param name="symptom">Performance symptom string from DDAAnalyzer</param>
        /// <param name="onResult">Callback with the adjusted profile (or current profile on failure)</param>
        public Coroutine RequestAdjustment(string metricsJson, string symptom, Action<DifficultyProfile> onResult)
        {
            return StartCoroutine(RequestAdjustmentCoroutine(metricsJson, symptom, onResult));
        }

        /// <summary>
        /// Builds the full prompt that would be sent to the LLM. Useful for testing/debugging.
        /// </summary>
        public string BuildFullPrompt(string metricsJson, string symptom)
        {
            string profileJson = m_CurrentProfile != null ? m_CurrentProfile.ToJson() : "{}";

            string prompt = m_SystemPrompt + "\n\n";

            // Add examples section
            prompt += "# Examples\n";
            if (m_ExampleBuffer.Count > 0)
            {
                foreach (string example in m_ExampleBuffer)
                {
                    prompt += example + "\n";
                }
            }
            else
            {
                prompt += GetDefaultExamples();
            }

            // Add request section
            prompt += "\n# Request\n";
            prompt += "// Input\n";
            prompt += "{\"symptom\":\"" + symptom + "\",\"metrics\":" + metricsJson + ",\"game_variables\":" + profileJson + "}\n";
            prompt += "// Output\n";

            return prompt;
        }

        /// <summary>
        /// Clears the example buffer.
        /// </summary>
        public void ClearExampleBuffer()
        {
            m_ExampleBuffer.Clear();
        }

        /// <summary>
        /// Returns the effective model ID based on provider and override.
        /// </summary>
        public string GetEffectiveModelId()
        {
            if (!string.IsNullOrEmpty(m_ModelIdOverride))
                return m_ModelIdOverride;

            switch (m_Provider)
            {
                case LLMProvider.Gemini:
                    return "gemini-2.0-flash";
                case LLMProvider.Claude:
                    return "claude-sonnet-4-5-20250929";
                default:
                    return "gemini-2.0-flash";
            }
        }

        /// <summary>
        /// Returns the API URL for the current provider.
        /// </summary>
        public string GetApiUrl()
        {
            switch (m_Provider)
            {
                case LLMProvider.Gemini:
                    return "https://generativelanguage.googleapis.com/v1beta/models/"
                           + GetEffectiveModelId()
                           + ":generateContent?key=" + m_ApiKey;
                case LLMProvider.Claude:
                    return "https://api.anthropic.com/v1/messages";
                default:
                    return "";
            }
        }

        #endregion

        #region Private Methods

        private IEnumerator RequestAdjustmentCoroutine(string metricsJson, string symptom, Action<DifficultyProfile> onResult)
        {
            if (m_IsProcessing)
            {
                Debug.LogWarning("[LLMPolicyEngine] Already processing a request. Skipping.");
                onResult?.Invoke(m_CurrentProfile);
                yield break;
            }

            if (string.IsNullOrEmpty(m_ApiKey))
            {
                Debug.LogWarning("[LLMPolicyEngine] API key not set. Returning current profile unchanged.");
                onResult?.Invoke(m_CurrentProfile);
                yield break;
            }

            if (m_CurrentProfile == null)
            {
                Debug.LogError("[LLMPolicyEngine] No DifficultyProfile assigned.");
                onResult?.Invoke(null);
                yield break;
            }

            m_IsProcessing = true;

            string fullPrompt = BuildFullPrompt(metricsJson, symptom);

            if (m_LogPrompts)
            {
                Debug.Log("[LLMPolicyEngine] Provider: " + m_Provider + ", Model: " + GetEffectiveModelId());
                Debug.Log("[LLMPolicyEngine] Prompt:\n" + fullPrompt);
            }

            // Build provider-specific request
            string apiUrl = GetApiUrl();
            string requestBody = BuildApiRequestBody(fullPrompt);

            using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(requestBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                // Provider-specific headers
                if (m_Provider == LLMProvider.Claude)
                {
                    request.SetRequestHeader("x-api-key", m_ApiKey);
                    request.SetRequestHeader("anthropic-version", "2023-06-01");
                }
                // Gemini uses API key in URL query parameter, no extra headers needed

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[LLMPolicyEngine] API request failed: " + request.error +
                                     "\nResponse: " + request.downloadHandler.text);
                    m_IsProcessing = false;
                    onResult?.Invoke(m_CurrentProfile);
                    yield break;
                }

                string responseBody = request.downloadHandler.text;

                if (m_LogResponses)
                {
                    Debug.Log("[LLMPolicyEngine] Response:\n" + responseBody);
                }

                // Extract the text content using provider-specific parsing
                string llmOutput = ExtractTextFromResponse(responseBody);

                if (string.IsNullOrEmpty(llmOutput))
                {
                    Debug.LogWarning("[LLMPolicyEngine] Could not extract text from API response.");
                    m_IsProcessing = false;
                    onResult?.Invoke(m_CurrentProfile);
                    yield break;
                }

                // Strip markdown code fences if present (Gemini sometimes wraps in ```json...```)
                llmOutput = StripMarkdownCodeFences(llmOutput);

                if (m_LogResponses)
                {
                    Debug.Log("[LLMPolicyEngine] Extracted LLM output:\n" + llmOutput);
                }

                // Store the input/output pair in the example buffer
                string inputJson = "{\"symptom\":\"" + symptom + "\",\"metrics\":" + metricsJson + "}";
                AddToExampleBuffer(inputJson, llmOutput);

                // Parse and apply the response
                DifficultyProfile adjustedProfile = m_CurrentProfile.CreateRuntimeCopy();
                if (adjustedProfile.FromJson(llmOutput))
                {
                    adjustedProfile.ClampToThresholds();
                    m_CurrentProfile = adjustedProfile;

                    if (m_LogResponses)
                    {
                        Debug.Log("[LLMPolicyEngine] Successfully applied LLM adjustments.");
                    }
                }
                else
                {
                    Debug.LogWarning("[LLMPolicyEngine] Failed to parse LLM output. Using current profile.");
                }

                m_IsProcessing = false;
                onResult?.Invoke(m_CurrentProfile);
            }
        }

        /// <summary>
        /// Builds the request body in the format expected by the selected provider.
        /// </summary>
        private string BuildApiRequestBody(string prompt)
        {
            string escapedPrompt = EscapeJsonString(prompt);

            switch (m_Provider)
            {
                case LLMProvider.Gemini:
                    return BuildGeminiRequestBody(escapedPrompt);
                case LLMProvider.Claude:
                    return BuildClaudeRequestBody(escapedPrompt);
                default:
                    return "";
            }
        }

        /// <summary>
        /// Gemini API request body format.
        /// https://ai.google.dev/gemini-api/docs/text-generation
        /// </summary>
        private string BuildGeminiRequestBody(string escapedPrompt)
        {
            return string.Format(
                "{{\"contents\":[{{\"parts\":[{{\"text\":\"{0}\"}}]}}],\"generationConfig\":{{\"maxOutputTokens\":{1},\"temperature\":0.2}}}}",
                escapedPrompt,
                m_MaxTokens);
        }

        /// <summary>
        /// Claude/Anthropic Messages API request body format.
        /// https://docs.anthropic.com/en/api/messages
        /// </summary>
        private string BuildClaudeRequestBody(string escapedPrompt)
        {
            return string.Format(
                "{{\"model\":\"{0}\",\"max_tokens\":{1},\"messages\":[{{\"role\":\"user\",\"content\":\"{2}\"}}]}}",
                GetEffectiveModelId(),
                m_MaxTokens,
                escapedPrompt);
        }

        /// <summary>
        /// Extracts the generated text from the API response based on current provider.
        /// </summary>
        private string ExtractTextFromResponse(string responseBody)
        {
            switch (m_Provider)
            {
                case LLMProvider.Gemini:
                    return ExtractTextFromGeminiResponse(responseBody);
                case LLMProvider.Claude:
                    return ExtractTextFromClaudeResponse(responseBody);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Extracts text from Gemini API response.
        /// Response format: {"candidates":[{"content":{"parts":[{"text":"..."}]}}]}
        /// </summary>
        private string ExtractTextFromGeminiResponse(string responseBody)
        {
            // Find "text":"..." in the candidates array
            string textKey = "\"text\":\"";

            // There may be multiple "text" keys; we want the one inside candidates[0].content.parts[0]
            int candidatesIndex = responseBody.IndexOf("\"candidates\"", StringComparison.Ordinal);
            if (candidatesIndex < 0)
            {
                Debug.LogWarning("[LLMPolicyEngine] Gemini response missing 'candidates' field.");
                return null;
            }

            int textStart = responseBody.IndexOf(textKey, candidatesIndex, StringComparison.Ordinal);
            if (textStart < 0) return null;

            textStart += textKey.Length;
            return ExtractQuotedString(responseBody, textStart);
        }

        /// <summary>
        /// Extracts text from Claude/Anthropic Messages API response.
        /// Response format: {"content":[{"type":"text","text":"..."}]}
        /// </summary>
        private string ExtractTextFromClaudeResponse(string responseBody)
        {
            string textKey = "\"text\":\"";
            int textStart = responseBody.IndexOf(textKey, StringComparison.Ordinal);
            if (textStart < 0) return null;

            textStart += textKey.Length;
            return ExtractQuotedString(responseBody, textStart);
        }

        /// <summary>
        /// Extracts a string starting at the given position until the closing unescaped quote.
        /// Handles escaped characters.
        /// </summary>
        private string ExtractQuotedString(string source, int startIndex)
        {
            int end = startIndex;
            while (end < source.Length)
            {
                if (source[end] == '\\')
                {
                    end += 2; // Skip escaped character
                    continue;
                }
                if (source[end] == '"')
                {
                    break;
                }
                end++;
            }

            string extracted = source.Substring(startIndex, end - startIndex);

            // Unescape common sequences
            extracted = extracted
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");

            return extracted;
        }

        /// <summary>
        /// Strips markdown code fences (```json ... ```) that some models wrap their output in.
        /// </summary>
        private string StripMarkdownCodeFences(string text)
        {
            if (text == null) return null;

            string trimmed = text.Trim();

            // Check for ```json or ``` at start
            if (trimmed.StartsWith("```"))
            {
                int firstNewline = trimmed.IndexOf('\n');
                if (firstNewline > 0)
                {
                    trimmed = trimmed.Substring(firstNewline + 1);
                }
            }

            // Check for ``` at end
            if (trimmed.EndsWith("```"))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 3);
            }

            return trimmed.Trim();
        }

        /// <summary>
        /// Escapes a string for safe embedding in a JSON value.
        /// </summary>
        private string EscapeJsonString(string text)
        {
            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private void AddToExampleBuffer(string inputJson, string outputJson)
        {
            string example = "// Input\n" + inputJson + "\n// Output\n" + outputJson;
            m_ExampleBuffer.Add(example);

            // Trim buffer to max size
            while (m_ExampleBuffer.Count > m_MaxExampleBufferSize)
            {
                m_ExampleBuffer.RemoveAt(0);
            }
        }

        private string GetDefaultExamples()
        {
            return @"// Input
{""symptom"":""low"",""metrics"":{""distanceTraveled"":35.0,""deathCount"":5,""totalRunTime"":30.0,""avgTimeBetweenDeaths"":6.0,""coinsCollected"":2,""jumpsPerSecond"":0.4},""game_variables"":{""game_variables"":[{""description"":""enemyDensity"",""threshold"":[0.0,1.0],""value"":0.60},{""description"":""gapFrequency"",""threshold"":[0.0,0.8],""value"":0.40},{""description"":""runSpeed"",""threshold"":[3.0,12.0],""value"":6.00},{""description"":""jumpStrength"",""threshold"":[6.0,15.0],""value"":10.00},{""description"":""sawProbability"",""threshold"":[0.0,1.0],""value"":0.50},{""description"":""spikeProbability"",""threshold"":[0.0,1.0],""value"":0.40},{""description"":""coinDensity"",""threshold"":[0.1,1.0],""value"":0.40},{""description"":""platformHeightVariance"",""threshold"":[0.0,3.0],""value"":1.50}]}}
// Output
{""game_variables"":[{""description"":""enemyDensity"",""value"":0.40},{""description"":""gapFrequency"",""value"":0.25},{""description"":""runSpeed"",""value"":4.50},{""description"":""jumpStrength"",""value"":12.00},{""description"":""sawProbability"",""value"":0.30},{""description"":""spikeProbability"",""value"":0.25},{""description"":""coinDensity"",""value"":0.65},{""description"":""platformHeightVariance"",""value"":0.80}]}

// Input
{""symptom"":""sharply.high"",""metrics"":{""distanceTraveled"":450.0,""deathCount"":0,""totalRunTime"":120.0,""avgTimeBetweenDeaths"":120.0,""coinsCollected"":38,""jumpsPerSecond"":1.2},""game_variables"":{""game_variables"":[{""description"":""enemyDensity"",""threshold"":[0.0,1.0],""value"":0.40},{""description"":""gapFrequency"",""threshold"":[0.0,0.8],""value"":0.20},{""description"":""runSpeed"",""threshold"":[3.0,12.0],""value"":5.00},{""description"":""jumpStrength"",""threshold"":[6.0,15.0],""value"":10.00},{""description"":""sawProbability"",""threshold"":[0.0,1.0],""value"":0.30},{""description"":""spikeProbability"",""threshold"":[0.0,1.0],""value"":0.30},{""description"":""coinDensity"",""threshold"":[0.1,1.0],""value"":0.50},{""description"":""platformHeightVariance"",""threshold"":[0.0,3.0],""value"":0.50}]}}
// Output
{""game_variables"":[{""description"":""enemyDensity"",""value"":0.60},{""description"":""gapFrequency"",""value"":0.40},{""description"":""runSpeed"",""value"":7.00},{""description"":""jumpStrength"",""value"":8.50},{""description"":""sawProbability"",""value"":0.50},{""description"":""spikeProbability"",""value"":0.45},{""description"":""coinDensity"",""value"":0.35},{""description"":""platformHeightVariance"",""value"":1.20}]}
";
        }

        #endregion

    }

}
