---
name: dda-prompt
description: Generate, iterate, and test DDA prompts following the SPAR format (Situation, Purpose, Action, Examples, Request) for LLM-based difficulty adjustment in RedRunner.
argument-hint: [generate|improve|test|show]
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# DDA Prompt Engineering Skill

You help students design and iterate on prompts for the LLM Policy Engine following the SPAR framework from the DDA-MAPEKit paper.

## Arguments

- `generate` — Create a new SPAR prompt from scratch based on RedRunner's game variables
- `generate custom` — Interactive wizard to customize the SPAR prompt (choose variables, examples, reasoning style)
- `improve` — Read the current prompt in LLMPolicyEngine.cs and suggest improvements
- `test` — Dry-run the prompt with sample inputs (print what would be sent, no API call)
- `show` — Display the current prompt with annotations explaining each section
- `compare-providers` — Generate provider-specific optimizations (Gemini vs Claude prompt tuning)
- If no argument, default to `generate`

## SPAR Prompt Format (from the paper)

The prompt MUST have these 5 sections:

### 1. Situation
```
You are a Dynamic Difficulty Adjustment mechanism for a 2D platformer game called RedRunner.
You receive player performance metrics, a symptom classification, and current game variable
values with their allowed thresholds.
```

### 2. Purpose
```
Generate a JSON object with adjusted float values for game variables. Adjust values to
balance difficulty based on the player's performance symptom. Keep all values within their
defined thresholds. The goal is to maintain player flow — not too easy, not too hard.
```

### 3. Action (Chain-of-Thought guidance)
This section must include explicit reasoning steps for the LLM:
```
Follow these reasoning steps:
1. Read the symptom level to understand player state
2. For each game variable, determine if it helps the player or harms the player:
   - Player-HELPING: jumpStrength, coinDensity (higher = easier)
   - Player-HARMING: enemyDensity, gapFrequency, runSpeed, sawProbability, spikeProbability, platformHeightVariance (higher = harder)
3. If symptom is low/very_low (player struggling):
   - Decrease player-harming variables
   - Increase player-helping variables
4. If symptom is high/sharply_high (player dominating):
   - Increase player-harming variables
   - Decrease player-helping variables
5. For slight symptoms (slightly_low, slightly_high): make smaller adjustments
6. For normal: make no or minimal changes
7. Consider the current values — prefer gradual changes (10-20% shifts) over sudden jumps
8. Ensure all output values are within the threshold [min, max] for each variable
```

### 4. Examples (few-shot)
Include 2-3 input/output JSON pairs demonstrating correct adjustments:
- One with a struggling player (low symptom)
- One with a dominating player (high symptom)
- One with a balanced player (normal/slight symptom)

Format:
```
// Input
{"symptom":"low","metrics":{...},"game_variables":[{"description":"enemyDensity","threshold":[0.0,1.0],"value":0.6},...]}
// Output
{"game_variables":[{"description":"enemyDensity","value":0.4},...]}
```

### 5. Request
The current game state JSON — this is filled at runtime from `PlayerMetricsCollector` and `DifficultyProfile`.

## RedRunner-Specific Game Variables

When generating prompts, use these exact variable names and thresholds:

| Variable | Description | Threshold | Helps/Harms Player |
|----------|-------------|-----------|---------------------|
| enemyDensity | Probability weight of enemy-containing blocks | [0.0, 1.0] | Harms |
| gapFrequency | Probability of gap/pit blocks | [0.0, 0.8] | Harms |
| runSpeed | Player character run speed | [3.0, 12.0] | Harms (faster = harder) |
| jumpStrength | Player jump force | [6.0, 15.0] | Helps |
| sawProbability | Weight of saw enemies vs others | [0.0, 1.0] | Harms |
| spikeProbability | Weight of spike enemies | [0.0, 1.0] | Harms |
| coinDensity | Coin spawn rate in blocks | [0.1, 1.0] | Helps |
| platformHeightVariance | Y-offset randomness of platforms | [0.0, 3.0] | Harms |

## Provider-Specific Prompt Considerations

The `LLMPolicyEngine` supports multiple providers. The same SPAR prompt is used for all providers, but there are behavioral differences:

### Gemini (default, free tier)
- **Temperature**: Set to 0.2 for more deterministic JSON output
- **Code fence wrapping**: Gemini may wrap JSON output in ` ```json ... ``` ` markdown fences. The engine strips these automatically via `StripMarkdownCodeFences()`, but the prompt should still explicitly request "Respond with JSON only, no markdown formatting"
- **Free tier limits**: 10 requests per minute, ~1000 requests per day. Sufficient for classroom demos
- **Model**: `gemini-2.0-flash` — fast, cheap, good at structured JSON output

### Claude (paid)
- **Temperature**: Default (not explicitly set)
- **Clean JSON**: Claude typically returns clean JSON without code fences when instructed
- **Model**: `claude-sonnet-4-5-20250929` — higher quality reasoning but costs $3/$15 per 1M tokens

### Prompt Best Practices for Both Providers
- Always include "Respond ONLY with the JSON object" in the prompt
- Include thresholds in the request JSON to prevent hallucinated values
- Both providers handle few-shot examples well — always include 2+ examples

## Quality Checklist

When generating or reviewing a prompt, verify:
- [ ] All 5 SPAR sections present
- [ ] Chain-of-Thought steps are explicit in Action section
- [ ] Examples use valid JSON matching DifficultyProfile schema
- [ ] All variable names match exactly what the code uses
- [ ] Thresholds are included in the Request JSON (prevents hallucination)
- [ ] Output format is specified as JSON-only (no markdown, no explanation)
- [ ] Gradual adjustment guidance is included (prevent jarring changes)
- [ ] At least 2 few-shot examples with different symptoms
- [ ] Prompt works with both Gemini and Claude (provider-agnostic language)

## Custom Prompt Wizard

When `generate custom` is invoked, guide the user through:

1. **Which variables to include in the prompt?**
   - Show all variables from dda_config.json or defaults
   - Let user select subset (e.g., only player-helping vars, only enemy-related)

2. **Chain-of-Thought style**
   - **Step-by-step** (default from paper): Explicit numbered reasoning steps
   - **Natural**: "Consider the symptom and adjust accordingly..."
   - **Minimal**: Just output format, no reasoning guidance
   - **Custom**: User writes their own CoT instructions

3. **Few-shot examples**
   - How many examples? (0-5 recommended)
   - Auto-generate examples from test scenarios or let user write custom
   - Cover which symptom types? (all 7, or just low/normal/high)

4. **Adjustment aggressiveness**
   - Conservative: "Make gradual 5-10% changes"
   - Moderate: "Make 10-20% changes"
   - Aggressive: "Make bold 20-30% changes"
   - Context-aware: "Adjust based on how extreme current values are"

5. **Output format strictness**
   - Strict JSON-only (default)
   - Allow brief explanation before JSON
   - Request confidence scores per variable

6. **Provider-specific tuning** (if applicable)
   - For Gemini: Add explicit "no markdown" instruction
   - For Claude: Leverage system prompt vs user message structure
   - For both: Temperature/token settings

7. **Save and preview**
   - Show the generated prompt
   - Offer to write to LLMPolicyEngine.cs or save as separate .txt for review

When `compare-providers` is invoked:
- Generate two versions of the prompt optimized for Gemini and Claude
- Highlight differences (e.g., Gemini's markdown wrapping, Claude's system prompt structure)
- Suggest testing both to compare output quality

## When Improving

Read `Assets/Scripts/RedRunner/DDA/LLMPolicyEngine.cs` to find the current `m_SystemPrompt` value. Then:
1. Check against the quality checklist above
2. Identify missing sections or unclear instructions
3. Check if examples cover edge cases
4. Suggest concrete rewording with rationale
5. Write the improved prompt back to the file if the student confirms
