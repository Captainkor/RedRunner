---
name: dda-tune
description: Adjust DDA tendency, per-variable bias, sensitivity, and analyzer harshness on an already-implemented system. Use after initial setup to fine-tune how the DDA behaves without re-running the full scaffold wizard.
argument-hint: [tendency|bias|harshness|show|apply]
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# DDA Tuning Skill

You help students fine-tune their DDA system's behavior after it has been scaffolded and implemented. This skill modifies tendency, per-variable bias/sensitivity, and analyzer harshness — then propagates changes to all affected files (config, prompt, analyzer thresholds, effector logic).

## Arguments

- `show` — Display current tendency, bias, and harshness settings from dda_config.json and code
- `tendency [protective|punishing|symmetric|custom]` — Change the overall DDA tendency
- `bias <variable-name>` — Adjust sensitivity, priority, or direction lock for a single variable
- `bias all` — Review and adjust bias settings for all variables interactively
- `harshness [lenient|standard|strict|custom]` — Change analyzer harshness preset
- `apply` — Push current dda_config.json settings to all affected code files
- `reset` — Reset all tendency/bias/harshness to defaults
- If no argument, default to `show`

## Interaction Guidelines (CRITICAL for CLI)

When asking the user to choose between options, you MUST follow this pattern for every question:

1. **Always print a descriptive header** explaining what this step configures and why it matters
2. **Always describe each option in full** in your message text BEFORE calling AskUserQuestion
3. **Use descriptive labels** in AskUserQuestion options — never just numbers or short codes
4. **Include the option description** in the AskUserQuestion `description` field for each option

Example of CORRECT interaction for tendency:
```
### Change DDA Tendency

The tendency controls whether your DDA system prioritizes helping struggling players or challenging skilled ones.
Your current tendency is: **symmetric**

- **Protective**: Eases difficulty faster for struggling players (ease: 1.5x, harden: 0.8x). Best for casual/accessible games.
- **Punishing**: Ramps difficulty faster for skilled players (ease: 0.8x, harden: 1.5x). Best for competitive/hardcore games.
- **Symmetric**: Equal adjustment speed in both directions (1.0x each). Neutral default.
- **Custom**: You set exact ease/harden multipliers.
```
Then call AskUserQuestion with labels like "Protective", "Punishing", "Symmetric", "Custom" — each with a full description.

Example of CORRECT interaction for bias:
```
### Adjust Bias for enemyDensity

Current settings:
  Sensitivity: 1.0 (normal)
  Priority: medium
  Direction lock: none

Sensitivity controls how aggressively this variable changes (0.0-2.0):
  0.0 = locked (never changes)
  0.5 = conservative (half-sized adjustments)
  1.0 = normal (default)
  1.5 = aggressive (50% larger adjustments)
  2.0 = very aggressive (double-sized adjustments)
```
Then ask what to change with clear option labels.

**NEVER** present bare numbers without explaining what each means.

## How It Works

### For `show`

1. Read `Assets/Scripts/RedRunner/DDA/dda_config.json`
2. Read current `DDAAnalyzer.cs` thresholds
3. Read current `LLMPolicyEngine.cs` prompt for bias instructions
4. Display a summary:

```
=== DDA Tuning Summary ===

Tendency: protective
  Ease multiplier:   1.5x (struggling players get bigger help)
  Harden multiplier: 0.8x (skilled players get gentler challenge)

Analyzer Harshness: standard
  Death rate "normal" band:    [2.0, 4.0] per 100 distance
  Survival time "normal" band: [10s, 25s]

Per-Variable Bias:
| Variable              | Sensitivity | Priority | Direction Lock | Status    |
|-----------------------|-------------|----------|----------------|-----------|
| enemyDensity          | 1.5         | high     | none           | In prompt |
| gapFrequency          | 1.0         | medium   | none           | In prompt |
| runSpeed              | 0.5         | medium   | none           | In prompt |
| jumpStrength          | 1.0         | high     | increase_only  | In prompt |
| sawProbability        | 1.0         | low      | none           | In prompt |
| spikeProbability      | 1.0         | low      | none           | In prompt |
| coinDensity           | 1.2         | low      | none           | In prompt |
| platformHeightVariance| 0.8         | medium   | none           | In prompt |

Prompt sync: UP TO DATE (or OUT OF SYNC — run /dda-tune apply)
Analyzer sync: UP TO DATE (or OUT OF SYNC)
Effector sync: UP TO DATE (or OUT OF SYNC)
```

### For `tendency`

1. If argument provided (e.g., `tendency punishing`), set it directly
2. If no argument, ask which tendency:
   - **Protective** (default): easeMultiplier=1.5, hardenMultiplier=0.8
   - **Punishing**: easeMultiplier=0.8, hardenMultiplier=1.5
   - **Symmetric**: easeMultiplier=1.0, hardenMultiplier=1.0
   - **Custom**: Ask for easeMultiplier (0.1–3.0) and hardenMultiplier (0.1–3.0)
3. Update `dda_config.json`
4. Offer to run `apply` immediately to push changes to code

### For `bias <variable-name>`

1. Read current bias for that variable from dda_config.json
2. Show current values and ask what to change:

```
Current bias for enemyDensity:
  Sensitivity: 1.5 (aggressive)
  Priority: high
  Direction lock: none

What would you like to change?
```

3. Allow changing:
   - **Sensitivity** (0.0–2.0):
     - 0.0 = locked (LLM should never change this)
     - 0.5 = conservative
     - 1.0 = normal (default)
     - 1.5 = aggressive
     - 2.0 = very aggressive
   - **Priority** (low/medium/high): Which variables the LLM prioritizes
   - **Direction lock** (none/increase_only/decrease_only): Constrain adjustment direction
4. Update `dda_config.json`
5. Offer to run `apply`

### For `bias all`

1. Show a table of all variables with current bias
2. Walk through each variable, asking if the student wants to change it
3. Allow bulk operations: "Set all sensitivities to 1.0" or "Lock all enemy variables"
4. Update config and offer to apply

### For `harshness`

1. If argument provided (e.g., `harshness strict`), set it directly
2. If no argument, show current and ask:
   - **Lenient**: Wide "normal" band — DDA rarely triggers
     - Death rate normal: [1.5, 5.0], Survival time normal: [8s, 30s]
   - **Standard** (default): Paper's thresholds
     - Death rate normal: [2.0, 4.0], Survival time normal: [10s, 25s]
   - **Strict**: Narrow "normal" band — DDA reacts to small changes
     - Death rate normal: [2.5, 3.5], Survival time normal: [15s, 22s]
   - **Custom**: Set each threshold individually:
     - m_DeathRateSharplyHigh, m_DeathRateHigh, m_DeathRateSlightlyHigh
     - m_DeathRateSlightlyLow, m_DeathRateLow, m_DeathRateVeryLow
     - m_SurvivalTimeSharplyHigh through m_SurvivalTimeVeryLow
3. Update `dda_config.json`
4. Offer to run `apply`

### For `apply`

This is the key operation — it reads dda_config.json and pushes settings to all affected files:

#### 1. Update LLMPolicyEngine.cs prompt (`m_SystemPrompt`)

Read the current prompt. Find or insert tendency instructions in the Action section:

- If tendency is "protective", ensure the prompt contains:
  ```
  IMPORTANT: When the player is struggling (low/very.low symptoms), make MORE aggressive adjustments (15-25%). When dominating (high/sharply.high), make SMALLER adjustments (5-10%).
  ```
- If tendency is "punishing", swap the emphasis
- If tendency is "symmetric" or custom, adjust accordingly
- Remove any conflicting previous tendency instructions before inserting new ones

Add/update sensitivity CoT steps:
```
9. Pay attention to each variable's "sensitivity" field:
   - sensitivity > 1.0: adjust MORE aggressively
   - sensitivity < 1.0: adjust MORE conservatively
   - sensitivity = 0.0: do NOT change this variable
10. Prioritize "high" priority variables over "low" ones.
```

#### 2. Update DifficultyProfile.cs `ToJson()` or `GetVariables()` (if sensitivity metadata needs to be in the Request JSON)

Add sensitivity and priority to the JSON output for each variable so the LLM sees it:
```json
{"description":"enemyDensity","threshold":[0.0,1.0],"value":0.60,"sensitivity":1.5,"priority":"high"}
```

This may require:
- Adding `sensitivity` and `priority` fields to `DifficultyVariable` struct
- Updating `ToJson()` to include these fields
- Loading values from dda_config.json at runtime or baking them in

#### 3. Update DDAAnalyzer.cs thresholds

If harshness changed, update the `[SerializeField]` threshold fields:
- `m_DeathRateSharplyHigh` through `m_DeathRateVeryLow`
- `m_SurvivalTimeSharplyHigh` through `m_SurvivalTimeVeryLow`

For preset harshness levels, use these values:

**Lenient:**
```csharp
m_DeathRateSharplyHigh = 0.3f;
m_DeathRateHigh = 0.8f;
m_DeathRateSlightlyHigh = 1.5f;
m_DeathRateSlightlyLow = 5.0f;
m_DeathRateLow = 7.0f;
m_DeathRateVeryLow = 10.0f;

m_SurvivalTimeSharplyHigh = 80f;
m_SurvivalTimeHigh = 50f;
m_SurvivalTimeSlightlyHigh = 30f;
m_SurvivalTimeSlightlyLow = 8f;
m_SurvivalTimeLow = 4f;
m_SurvivalTimeVeryLow = 2f;
```

**Standard (default):**
```csharp
m_DeathRateSharplyHigh = 0.5f;
m_DeathRateHigh = 1.0f;
m_DeathRateSlightlyHigh = 2.0f;
m_DeathRateSlightlyLow = 4.0f;
m_DeathRateLow = 6.0f;
m_DeathRateVeryLow = 8.0f;

m_SurvivalTimeSharplyHigh = 60f;
m_SurvivalTimeHigh = 40f;
m_SurvivalTimeSlightlyHigh = 25f;
m_SurvivalTimeSlightlyLow = 10f;
m_SurvivalTimeLow = 5f;
m_SurvivalTimeVeryLow = 3f;
```

**Strict:**
```csharp
m_DeathRateSharplyHigh = 0.3f;
m_DeathRateHigh = 0.7f;
m_DeathRateSlightlyHigh = 1.5f;
m_DeathRateSlightlyLow = 3.5f;
m_DeathRateLow = 5.0f;
m_DeathRateVeryLow = 6.5f;

m_SurvivalTimeSharplyHigh = 50f;
m_SurvivalTimeHigh = 35f;
m_SurvivalTimeSlightlyHigh = 22f;
m_SurvivalTimeSlightlyLow = 15f;
m_SurvivalTimeLow = 8f;
m_SurvivalTimeVeryLow = 4f;
```

#### 4. Update DifficultyEffector.cs (optional, for post-processing)

If the student's effector applies sensitivity as a post-processing multiplier on LLM deltas, update the sensitivity map. This is an advanced pattern — check if the effector already has sensitivity logic before adding it.

#### 5. Update few-shot examples in LLMPolicyEngine.cs

If tendency changed significantly, the default examples in `GetDefaultExamples()` may no longer match the expected behavior. Flag this to the student:
```
WARNING: Your tendency is now "punishing" but the few-shot examples
still show protective behavior (bigger ease-down changes).
Run /dda-prompt improve to regenerate examples matching your new tendency.
```

### For `reset`

1. Reset dda_config.json tendency/bias section to defaults:
   - tendency: "symmetric"
   - easeMultiplier: 1.0
   - hardenMultiplier: 1.0
   - All variable sensitivities: 1.0
   - All priorities: "medium"
   - All direction locks: "none"
   - harshness: "standard"
2. Run `apply` to push defaults to all files

## Sync Detection

When running `show`, detect if config and code are out of sync:

1. **Prompt sync**: Read `m_SystemPrompt` in LLMPolicyEngine.cs. Check if tendency instructions match config. If config says "protective" but prompt has no protective instructions (or has "punishing"), flag as OUT OF SYNC.

2. **Analyzer sync**: Read threshold values in DDAAnalyzer.cs. Compare against the harshness preset in config. If they don't match any preset or the custom values in config, flag as OUT OF SYNC.

3. **Effector sync**: If config has sensitivity values but DifficultyEffector doesn't apply them, flag as OUT OF SYNC (informational — effector sensitivity is optional post-processing).

## Files Modified by Apply

| File | What Changes |
|------|-------------|
| `Assets/Scripts/RedRunner/DDA/dda_config.json` | Tendency, bias, harshness settings |
| `Assets/Scripts/RedRunner/DDA/LLMPolicyEngine.cs` | `m_SystemPrompt` Action section (tendency + sensitivity instructions) |
| `Assets/Scripts/RedRunner/DDA/DDAAnalyzer.cs` | Threshold `[SerializeField]` default values |
| `Assets/Scripts/RedRunner/DDA/DifficultyProfile.cs` | `ToJson()` output (sensitivity/priority metadata) — optional |
| `Assets/Scripts/RedRunner/DDA/DifficultyEffector.cs` | Sensitivity post-processing — optional, advanced |
