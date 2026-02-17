---
name: dda-test
description: Simulate player profiles and test LLM-based difficulty adjustments without running the game. Create synthetic test cases matching the paper's Table 1 evaluation methodology.
argument-hint: [simulate|batch|validate]
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# DDA Simulation & Testing Skill

You help students test their LLM DDA integration by simulating player scenarios and validating outputs, following the evaluation methodology from the DDA-MAPEKit paper (Table 1).

## Arguments

- `simulate` — Run a single simulation with a specified player profile and symptom
- `simulate custom` — Create and test a custom player profile interactively
- `batch` — Run all 5+ standard test scenarios from the paper's methodology
- `batch custom` — Run batch tests with user-defined scenarios
- `validate` — Check a set of LLM outputs against thresholds and reasonableness
- `compare-providers` — Run same scenarios on both Gemini and Claude and compare results
- If no argument, default to `simulate`

## Standard Test Scenarios

Following the paper's Table 1 approach, create test cases covering different profiles and symptoms. For RedRunner, define these standard simulations:

### Simulation Set

```json
[
    {
        "id": 1,
        "description": "Expert player dominating",
        "symptom": "sharply.high",
        "metrics": {
            "distanceTraveled": 450.0,
            "deathCount": 0,
            "totalRunTime": 120.0,
            "avgTimeBetweenDeaths": 120.0,
            "coinsCollected": 38,
            "jumpsPerSecond": 1.2
        },
        "currentValues": {
            "enemyDensity": 0.4,
            "gapFrequency": 0.2,
            "runSpeed": 5.0,
            "jumpStrength": 10.0,
            "sawProbability": 0.3,
            "spikeProbability": 0.3,
            "coinDensity": 0.5,
            "platformHeightVariance": 0.5
        }
    },
    {
        "id": 2,
        "description": "Good player, slightly above target",
        "symptom": "slightly.high",
        "metrics": {
            "distanceTraveled": 200.0,
            "deathCount": 1,
            "totalRunTime": 60.0,
            "avgTimeBetweenDeaths": 60.0,
            "coinsCollected": 15,
            "jumpsPerSecond": 0.9
        },
        "currentValues": {
            "enemyDensity": 0.5,
            "gapFrequency": 0.3,
            "runSpeed": 5.0,
            "jumpStrength": 10.0,
            "sawProbability": 0.4,
            "spikeProbability": 0.3,
            "coinDensity": 0.5,
            "platformHeightVariance": 1.0
        }
    },
    {
        "id": 3,
        "description": "Average player in flow state",
        "symptom": "normal",
        "metrics": {
            "distanceTraveled": 100.0,
            "deathCount": 2,
            "totalRunTime": 45.0,
            "avgTimeBetweenDeaths": 22.5,
            "coinsCollected": 8,
            "jumpsPerSecond": 0.7
        },
        "currentValues": {
            "enemyDensity": 0.5,
            "gapFrequency": 0.3,
            "runSpeed": 5.0,
            "jumpStrength": 10.0,
            "sawProbability": 0.4,
            "spikeProbability": 0.3,
            "coinDensity": 0.5,
            "platformHeightVariance": 1.0
        }
    },
    {
        "id": 4,
        "description": "Struggling player, dying frequently",
        "symptom": "low",
        "metrics": {
            "distanceTraveled": 35.0,
            "deathCount": 5,
            "totalRunTime": 30.0,
            "avgTimeBetweenDeaths": 6.0,
            "coinsCollected": 2,
            "jumpsPerSecond": 0.4
        },
        "currentValues": {
            "enemyDensity": 0.6,
            "gapFrequency": 0.4,
            "runSpeed": 6.0,
            "jumpStrength": 10.0,
            "sawProbability": 0.5,
            "spikeProbability": 0.4,
            "coinDensity": 0.4,
            "platformHeightVariance": 1.5
        }
    },
    {
        "id": 5,
        "description": "New player, completely overwhelmed",
        "symptom": "very.low",
        "metrics": {
            "distanceTraveled": 12.0,
            "deathCount": 8,
            "totalRunTime": 20.0,
            "avgTimeBetweenDeaths": 2.5,
            "coinsCollected": 0,
            "jumpsPerSecond": 0.2
        },
        "currentValues": {
            "enemyDensity": 0.7,
            "gapFrequency": 0.5,
            "runSpeed": 7.0,
            "jumpStrength": 9.0,
            "sawProbability": 0.6,
            "spikeProbability": 0.5,
            "coinDensity": 0.3,
            "platformHeightVariance": 2.0
        }
    }
]
```

## How to Simulate

### For `simulate`

1. Read the current prompt from `Assets/Scripts/RedRunner/DDA/LLMPolicyEngine.cs` (the `m_SystemPrompt` field)
2. Read `DifficultyProfile.cs` to understand the variable schema
3. Ask the student which simulation ID to run (1-5) or accept custom input
4. Construct the full prompt by combining: System prompt + Examples + Request JSON
5. Print the complete prompt that WOULD be sent to the API
6. If the student has an API key configured, offer to make the actual API call. Determine the provider from `LLMPolicyEngine.cs` (`m_Provider` field):
   - **Gemini** (default, free): `curl -X POST "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key=API_KEY" -H "Content-Type: application/json" -d '{"contents":[{"parts":[{"text":"FULL_PROMPT"}]}],"generationConfig":{"maxOutputTokens":1024,"temperature":0.2}}'`
   - **Claude** (paid): `curl -X POST "https://api.anthropic.com/v1/messages" -H "x-api-key: API_KEY" -H "anthropic-version: 2023-06-01" -H "content-type: application/json" -d '{"model":"claude-sonnet-4-5-20250929","max_tokens":1024,"system":"SYSTEM_PROMPT","messages":[{"role":"user","content":"REQUEST_JSON"}]}'`
   - Note: Gemini responses may include markdown code fences around JSON — strip ` ```json ``` ` before parsing
7. Display the result and validate it

### For `batch`

1. Run all 5 simulations sequentially
2. Format results in a table matching the paper's Table 1 format:

```
| # | Symptom       | Input Values                          | Output Values (LLM)                    |
|   |               | eD   gF   rS   jS   sP   spP  cD  pH | eD   gF   rS   jS   sP   spP  cD  pH  |
|---|---------------|---------------------------------------|----------------------------------------|
| 1 | sharply.high  | 0.4  0.2  5.0  10.0 0.3  0.3  0.5 0.5| ...                                    |
```

3. Save results to `Assets/Scripts/RedRunner/DDA/TestResults/batch_results.json`

### For `validate`

Check LLM output against these rules:
1. **Threshold compliance**: Every value within [min, max]
2. **Directional correctness**: Low symptom should decrease harming variables, increase helping
3. **Magnitude reasonableness**: Changes should not exceed 30% per adjustment cycle
4. **JSON validity**: Output parses correctly
5. **Completeness**: All expected variables present in output
6. **No hallucinations**: No extra fields or unexpected data types

Print a validation report with pass/fail per check.

## Custom Simulation Wizard

When `simulate custom` is invoked:

1. **Player profile type**
   - Choose from: "Expert", "Good", "Average", "Struggling", "Beginner", "Custom"
   - For custom, ask for metrics values

2. **Symptom classification**
   - Manual: Choose from 7 symptom levels
   - Auto: Calculate symptom from metrics using DDAAnalyzer thresholds

3. **Starting difficulty values**
   - Use current profile from scene
   - Use balanced defaults (0.5, midpoint values)
   - Custom values

4. **Output preference**
   - Console only
   - Save to file (JSON)
   - Both

When `batch custom` is invoked:
- Ask how many scenarios (1-10 recommended)
- For each scenario, run the custom wizard or load from file
- Save batch definition to `TestResults/custom_batch.json` for reuse

When `compare-providers` is invoked:
- Check if both Gemini and Claude API keys are configured
- Run the same scenarios on both
- Display side-by-side comparison table:
```
| Scenario | Variable    | Gemini Output | Claude Output | Difference |
|----------|-------------|---------------|---------------|------------|
| 1        | enemyDensity| 0.55          | 0.52          | -0.03      |
...
```
- Flag significant disagreements (>0.1 difference)
- Suggest which provider is more appropriate for this use case

## Output Format

Always present results clearly:

```
=== Simulation #1: Expert player dominating ===
Symptom: sharply.high

Input Profile:
  enemyDensity:           0.4  [0.0 - 1.0]
  gapFrequency:           0.2  [0.0 - 0.8]
  runSpeed:               5.0  [3.0 - 12.0]
  jumpStrength:           10.0 [6.0 - 15.0]
  sawProbability:         0.3  [0.0 - 1.0]
  spikeProbability:       0.3  [0.0 - 1.0]
  coinDensity:            0.5  [0.1 - 1.0]
  platformHeightVariance: 0.5  [0.0 - 3.0]

LLM Output:
  enemyDensity:           0.6  (+0.2) CORRECT (increased harming)
  gapFrequency:           0.35 (+0.15) CORRECT (increased harming)
  ...

Validation: 8/8 checks passed
```
