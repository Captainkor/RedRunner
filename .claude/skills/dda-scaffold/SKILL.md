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

## Interaction Guidelines (CRITICAL for CLI)

**Do NOT use the AskUserQuestion tool.** It does not work reliably in all environments (Rider, CLI, etc.).

Instead, ask questions as **plain chat messages**. Print the question with numbered options, then **STOP and wait for the user to reply** before continuing. Do NOT call any tools — just print the question and end your turn.

### How to Ask a Question

1. Print a short explanation of what this step configures and why it matters.
2. List numbered options with descriptions.
3. Indicate the recommended default.
4. End your message. Do NOT call any tools. Wait for the user to type a reply.

**Example:**

```
### Step 1: Difficulty Variables

These are the game parameters the LLM can adjust at runtime.

| Variable | Range | Effect |
|---|---|---|
| enemyDensity | [0.0, 1.0] | More enemies (harder) |
| gapFrequency | [0.0, 0.8] | More gaps (harder) |
| runSpeed | [3.0, 12.0] | Faster character (harder) |
| jumpStrength | [6.0, 15.0] | Higher jumps (easier) |
| sawProbability | [0.0, 1.0] | More saws (harder) |
| spikeProbability | [0.0, 1.0] | More spikes (harder) |
| coinDensity | [0.1, 1.0] | More coins (easier) |
| platformHeightVariance | [0.0, 3.0] | Uneven terrain (harder) |

Which set should the DDA control?

1. **All 8 variables** ← recommended
2. **Core 4** — enemyDensity, runSpeed, jumpStrength, coinDensity
3. **Enemies only** — enemyDensity, sawProbability, spikeProbability
4. **Custom** — I'll tell you which ones
```

Then STOP. Wait for the user to reply with a number or description.

### Flow Rules

- **One question per message.** Ask one thing, stop, wait for the reply.
- **Use numbered options** (1, 2, 3...) so the user can just type a number.
- **Mark the recommended default** with "← recommended".
- **Keep it to 6–8 total questions.** Use presets to bundle complex choices.
- **If the user says "default" or just presses enter**, apply the recommended option and move on.
- **After the user answers**, acknowledge their choice briefly and immediately ask the next question (or proceed to save if done).
- **Never call tools while waiting for an answer.** The question IS your entire response.

## Interactive Configuration Mode

When `configure` is invoked, walk through these steps one at a time. Each step is one chat message with numbered options. Wait for the user's reply before moving to the next step.

### Step 1: Variable Selection

Print the variable table (see example above), then ask which set. Options:
1. All 8 variables ← recommended
2. Core 4 (enemyDensity, runSpeed, jumpStrength, coinDensity)
3. Enemies only (enemyDensity, sawProbability, spikeProbability)
4. Custom — user lists which ones they want

### Step 2: Metrics

Explain that metrics are what the Monitor collects to feed the Analyzer. Options:
1. Standard set ← recommended (distanceTraveled, deathCount, totalRunTime, avgTimeBetweenDeaths, coinsCollected, jumpsCount, jumpsPerSecond)
2. Minimal (deathCount, distanceTraveled, totalRunTime)
3. Extended — standard + ask what custom metric to add

### Step 3: LLM Provider

Options:
1. Gemini Flash ← recommended (free tier, 10 RPM, ~1000 req/day)
2. Claude Sonnet (paid, $3/$15 per 1M tokens)
3. Both (A/B testing, compare with `/dda-test`)

After this step, remind user where to get API key but don't ask for it here.

### Step 4: DDA Tendency

Explain: this controls whether the system favors helping struggling players or challenging skilled ones.

1. Protective ← recommended (ease 15-25%, toughen 5-10%)
2. Punishing (toughen 15-25%, ease 5-10%)
3. Symmetric (equal 10-20% both directions)
4. Custom — follow-up to pick multipliers

### Step 5: Variable Sensitivity

Explain: sensitivity (0.0–2.0) controls how aggressively each variable changes per cycle.

1. Balanced ← recommended (all at 1.0, medium priority)
2. Protective preset (helping vars at 1.5, harming vars at 0.7)
3. Aggressive preset (all at 1.5, high priority)
4. Custom per-variable — walk through groups

### Step 6: Analyzer Harshness

Explain: how easily the system decides the player is struggling/dominating.

1. Standard ← recommended (death rate normal band [2.0, 4.0], survival [10s, 25s])
2. Lenient (wider band — [1.5, 5.0], [8s, 30s])
3. Strict (narrow band — [2.5, 3.5], [15s, 22s])

### Step 7: Effector Wiring

1. Reflection ← recommended (sets private fields via C# reflection, works out of the box)
2. Public setters (cleaner but requires adding setter methods to RedCharacter/Block)
3. Mixed (reflection for character, probability scaling for terrain)

### Step 8: Save & Scaffold

After all questions are answered, do NOT ask another question. Automatically:
1. Save all choices to `Assets/Scripts/RedRunner/DDA/dda_config.json`
2. Print a summary table of all configured settings
3. Scaffold/update components according to the saved preferences
4. Print what the student should do next

### Config JSON Format

Save to `Assets/Scripts/RedRunner/DDA/dda_config.json`:
```json
{
  "variables": ["enemyDensity", "gapFrequency", ...],
  "metrics": ["distanceTraveled", "deathCount", ...],
  "customMetrics": [],
  "llmProvider": "gemini",
  "llmModel": "gemini-2.0-flash",
  "tendency": "protective",
  "easeMultiplier": 1.5,
  "hardenMultiplier": 0.8,
  "variableBias": {
    "enemyDensity": { "sensitivity": 1.0, "priority": "high", "directionLock": "none" },
    ...
  },
  "analyzerHarshness": "standard",
  "analyzerThresholds": {
    "deathRateNormalBand": [2.0, 4.0],
    "survivalTimeNormalBand": [10, 25]
  },
  "effectorWiring": "reflection"
}
```

This config is read by:
- `/dda-prompt generate` → injects bias instructions into the SPAR prompt
- `/dda-evaluate configure-baseline` → sets comparison thresholds
- `/dda-integrate wire` → applies wiring preferences
- `/dda-tune` → reads and updates tendency/bias settings

### Handling Defaults

If the user says "default", "recommended", or gives an unclear answer:
- Apply the recommended option for that step
- Print: "Using default: [setting]. You can change this later with `/dda-tune`."
- Move to the next step

After all steps, scaffold components according to the saved preferences.

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
