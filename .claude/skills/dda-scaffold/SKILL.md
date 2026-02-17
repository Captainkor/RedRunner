---
name: dda-scaffold
description: Scaffold MAPE-K DDA components (PlayerMetricsCollector, DDAAnalyzer, LLMPolicyEngine, DifficultyEffector, DifficultyProfile) into the RedRunner project following existing code conventions.
argument-hint: [component-name or "all"]
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# DDA MAPE-K Scaffold Generator

You are scaffolding Dynamic Difficulty Adjustment components for the RedRunner Unity project based on the MAPE-K loop (Monitor, Analyze, Plan, Execute) with LLM integration, following the approach from the DDA-MAPEKit + LLM paper (SBGames 2024).

## Arguments

- `$ARGUMENTS` can be one of: `all`, `metrics`, `analyzer`, `policy`, `effector`, `profile`, `configure`, or a custom component name.
- If no argument, scaffold all components.
- `configure` — Interactive configuration wizard to choose which variables to track, thresholds, LLM provider, etc.

## Project Conventions (MUST follow)

1. **Namespace**: `RedRunner.DDA` for all new DDA scripts
2. **File location**: `Assets/Scripts/RedRunner/DDA/`
3. **Field naming**: `m_` prefix for private/serialized fields
4. **Singleton pattern**: Match `GameManager.cs` style (static `m_Singleton`, null check in `Awake`, `Destroy(gameObject)` on duplicate)
5. **Serialization**: `[SerializeField]` on private fields, `[Header()]` for Inspector groups
6. **Regions**: Use `#region` blocks (Fields, Properties, MonoBehaviour Messages, Public Methods, Private Methods, Events)
7. **Properties**: Explicit get-only properties wrapping `m_` fields
8. **Event system**: Use `Property<T>` from `Assets/Scripts/Utils/Property.cs` for observable values, static delegate events for global notifications
9. **Physics API**: Use `Rigidbody2D.linearVelocity` (Unity 6 API)

## Components to Scaffold

### 1. DifficultyProfile (ScriptableObject)
Location: `Assets/Scripts/RedRunner/DDA/DifficultyProfile.cs`

A ScriptableObject holding all tunable difficulty knobs with thresholds (min/max) for LLM output validation:

```
- enemyDensity (float, threshold [0.0, 1.0])
- gapFrequency (float, threshold [0.0, 0.8])
- runSpeed (float, threshold [3.0, 12.0])
- jumpStrength (float, threshold [6.0, 15.0])
- sawProbability (float, threshold [0.0, 1.0])
- spikeProbability (float, threshold [0.0, 1.0])
- coinDensity (float, threshold [0.1, 1.0])
- platformHeightVariance (float, threshold [0.0, 3.0])
```

Include a `[System.Serializable] DifficultyVariable` struct with fields: `description`, `value`, `thresholdMin`, `thresholdMax`. Include methods: `ToJson()`, `FromJson(string)`, `ClampToThresholds()`.

### 2. PlayerMetricsCollector (MonoBehaviour) — MONITOR
Location: `Assets/Scripts/RedRunner/DDA/PlayerMetricsCollector.cs`

Hooks into existing systems to collect per-run player data:
- Subscribe to `GameManager.OnScoreChanged` for distance
- Subscribe to `Character.IsDead` Property for death events
- Subscribe to `GameManager.OnReset` for run resets
- Track: `distanceTraveled`, `deathCount`, `totalRunTime`, `avgTimeBetweenDeaths`, `coinsCollected`, `jumpsCount`, `jumpsPerSecond`
- Expose a `GetMetricsJson()` method returning JSON string
- Expose a `ResetMetrics()` method called on new run

### 3. DDAAnalyzer (MonoBehaviour) — ANALYZE
Location: `Assets/Scripts/RedRunner/DDA/DDAAnalyzer.cs`

Classifies player performance into symptoms:
- Input: `PlayerMetricsCollector` reference
- Symptom levels (enum): `sharply_high`, `high`, `slightly_high`, `normal`, `slightly_low`, `low`, `very_low`
- Configurable thresholds per metric via `[SerializeField]` fields
- Method: `PerformanceSymptom AnalyzePerformance()` — combines metrics into a single symptom
- Method: `string GetSymptomString()` — returns symptom as string matching paper format ("sharply.high", "high", etc.)

### 4. LLMPolicyEngine (MonoBehaviour) — PLAN
Location: `Assets/Scripts/RedRunner/DDA/LLMPolicyEngine.cs`

Replaces rule-based policy with LLM API calls. **Supports multiple providers** via `LLMProvider` enum.

#### Provider Support
```csharp
public enum LLMProvider { Gemini, Claude }
```
- `[SerializeField] LLMProvider m_Provider` — default `Gemini` (free tier, recommended for classroom)
- `[SerializeField] string m_ApiKey` — API key for the selected provider
  - Gemini: Get free key at https://aistudio.google.com/apikey (10 RPM, ~1000 req/day)
  - Claude: Get key at https://console.anthropic.com/ (paid, $3/$15 per 1M tokens)
- `[SerializeField] string m_ModelId` — leave empty for provider defaults:
  - Gemini default: `gemini-2.0-flash`
  - Claude default: `claude-sonnet-4-5-20250929`

#### API Details
- **Gemini**: POST to `https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}`
  - Body: `{"contents":[{"parts":[{"text":"..."}]}],"generationConfig":{"maxOutputTokens":1024,"temperature":0.2}}`
  - Response: `{"candidates":[{"content":{"parts":[{"text":"..."}]}}]}`
  - Gemini may wrap JSON in markdown code fences — include `StripMarkdownCodeFences()` method
- **Claude**: POST to `https://api.anthropic.com/v1/messages`
  - Headers: `x-api-key`, `anthropic-version`, `content-type`
  - Body: `{"model":"...","max_tokens":1024,"system":"...","messages":[...]}`
  - Response: `{"content":[{"text":"..."}]}`

#### Common Fields
- `[SerializeField] int m_MaxExampleBufferSize` — default 5
- `[SerializeField] DifficultyProfile m_CurrentProfile`
- `[SerializeField] [TextArea(10, 30)] string m_SystemPrompt` — editable in Inspector, SPAR format
- Example buffer: `List<string>` storing recent input-output JSON pairs, injected into prompt
- Method: `Coroutine RequestAdjustment(string metricsJson, string symptom, Action<DifficultyProfile> onResult)`
- Provider-specific request building and response parsing methods
- JSON response parsing with threshold validation
- Error handling: log warnings, fall back to current profile on failure

### 5. DifficultyEffector (MonoBehaviour) — EXECUTE
Location: `Assets/Scripts/RedRunner/DDA/DifficultyEffector.cs`

Applies difficulty profile changes to actual game systems:
- `[SerializeField] RedCharacter m_Character` reference
- `[SerializeField] TerrainGenerationSettings m_TerrainSettings` reference
- Method: `void ApplyProfile(DifficultyProfile profile)`
  - Maps `runSpeed` → `RedCharacter` speed (requires exposing setter or using reflection)
  - Maps `jumpStrength` → `RedCharacter` jump
  - Maps `enemyDensity`, `sawProbability`, `spikeProbability` → Block probability weights
  - Maps `coinDensity` → Coin spawn rates in blocks
  - Maps `gapFrequency` → Middle block selection weights
  - Maps `platformHeightVariance` → Block Y offset randomization
- Logs all changes for debugging

### 6. DDAManager (MonoBehaviour) — ORCHESTRATOR
Location: `Assets/Scripts/RedRunner/DDA/DDAManager.cs`

Singleton orchestrating the full MAPE-K loop:
- References to all components above
- Triggers analysis cycle on player death (subscribe to `Character.IsDead`)
- Calls: Collect → Analyze → Plan (LLM) → Execute
- Configurable: `[SerializeField] bool m_Enabled` — toggle DDA on/off
- Configurable: `[SerializeField] float m_MinTimeBetweenAdjustments` — prevent rapid fire
- Event: `static event Action<DifficultyProfile> OnDifficultyChanged`

## Interactive Configuration Mode

When `configure` is invoked, guide the user through these choices:

1. **Which difficulty variables to use?**
   - Show the full list of 8 standard variables
   - Let user select subset or add custom variables
   - For each variable, ask: description, threshold min/max, help/harm classification

2. **Which metrics to track?**
   - Show standard metrics (distanceTraveled, deathCount, etc.)
   - Let user add custom metrics (e.g., "time near enemies", "precision landing score")
   - For each custom metric, ask: how to collect it? (event subscription, polling, derived calculation)

3. **LLM Provider setup**
   - Choose provider: Gemini (free, 10 RPM), Claude (paid), or both (for A/B testing)
   - Guide API key setup
   - Suggest model IDs or allow custom

4. **Symptom classification thresholds**
   - Show example thresholds for each metric
   - Let user customize (e.g., "what distance counts as 'good' performance?")

5. **Effector wiring preferences**
   - For each variable, ask: "How should this wire to the game?"
   - Suggest default mappings, allow custom (e.g., "runSpeed → RedCharacter.m_RunSpeed" or "runSpeed → custom system")
   - Choose integration approach: direct setters, reflection, or manual

6. **Save configuration to JSON**
   - Save choices to `Assets/Scripts/RedRunner/DDA/dda_config.json`
   - Use this config to scaffold components with user's exact needs

After configuration, scaffold components according to the saved preferences.

## After Scaffolding

1. Create a `DDA` subfolder in `Assets/Prefabs/` for a DDA Manager prefab
2. If configuration was used, print a summary of the custom setup
3. Otherwise remind the student to:
   - Create a DifficultyProfile ScriptableObject asset via the Create menu
   - Add the DDAManager to the scene or an existing manager prefab
   - Select their LLM provider in the Inspector (Gemini recommended for free tier)
   - Get an API key: Gemini at https://aistudio.google.com/apikey (free) or Claude at https://console.anthropic.com/ (paid)
   - Paste the API key in the Inspector
   - Configure symptom thresholds
4. Print a summary of all created files and what the student should do next
