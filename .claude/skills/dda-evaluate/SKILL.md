---
name: dda-evaluate
description: Compare rule-based vs LLM-based DDA outputs, generate evaluation reports, and analyze adjustment quality following the paper's experimental methodology.
argument-hint: [compare|report|analyze-logs]
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# DDA Evaluation Skill

You help students evaluate their LLM-based DDA implementation by comparing it against rule-based baselines and analyzing adjustment quality, following the methodology from Section 3.2 of the DDA-MAPEKit paper.

## Arguments

- `compare` — Compare LLM outputs against rule-based outputs for the same inputs
- `report` — Generate a full evaluation report from batch test results or session logs
- `analyze-logs` — Read session logs from `Application.persistentDataPath/DDALogs/` and analyze trends
- If no argument, default to `compare`

## Rule-Based Baseline

Students must first define a rule-based baseline for comparison. Help them create one at `Assets/Scripts/RedRunner/DDA/RuleBasedBaseline.cs`:

```csharp
// Simple rule-based adjustments for comparison
// symptom: very.low → decrease ALL harming vars by 30%, increase helping by 30%
// symptom: low → decrease harming by 20%, increase helping by 20%
// symptom: slightly.low → decrease harming by 10%, increase helping by 10%
// symptom: normal → no change
// symptom: slightly.high → increase harming by 10%, decrease helping by 10%
// symptom: high → increase harming by 20%, decrease helping by 20%
// symptom: sharply.high → increase harming by 30%, decrease helping by 30%
```

This creates the "static rules" column matching Table 1 in the paper.

## Comparison Methodology

### For `compare`

1. Read batch test results from `Assets/Scripts/RedRunner/DDA/TestResults/batch_results.json`
2. If no batch results exist, run `/dda-test batch` first
3. Compute rule-based output for each simulation
4. Present side-by-side comparison in table format:

```
=== Comparison Table ===

| #  | Symptom       | Variable         | Input | Rules | LLM  | Delta(R) | Delta(L) | Direction Match |
|----|---------------|------------------|-------|-------|------|----------|----------|-----------------|
| 1  | sharply.high  | enemyDensity     | 0.4   | 0.52  | 0.55 | +0.12    | +0.15    | YES             |
| 1  | sharply.high  | gapFrequency     | 0.2   | 0.26  | 0.30 | +0.06    | +0.10    | YES             |
| 1  | sharply.high  | jumpStrength     | 10.0  | 7.0   | 8.5  | -3.0     | -1.5     | YES             |
...
```

### Metrics to Compute

For each simulation, calculate:

1. **Direction Match Rate**: % of variables where LLM adjustment direction matches rules
   - e.g., both increase enemyDensity for high symptom → match
2. **Mean Absolute Difference**: Average |LLM_value - Rule_value| across all variables
3. **Context Sensitivity Score**: Did the LLM consider current values? (LLM should make smaller changes when values are already extreme)
4. **Threshold Compliance Rate**: % of LLM outputs within defined thresholds
5. **Smoothness Score**: Average % change per variable (lower = smoother, paper prefers gradual)

### For `report`

Generate a markdown report including:

```markdown
# DDA Evaluation Report
Date: {date}
Model: {model_id}
Simulations Run: {count}

## Summary Statistics
- Direction Match Rate: X%
- Mean Absolute Difference: X
- Threshold Compliance: X%
- Average Smoothness: X%

## Per-Simulation Results
[Table 1 format from paper]

## Key Findings
1. [Auto-generated observation about where LLM agreed/disagreed with rules]
2. [Auto-generated observation about context sensitivity]
3. [Auto-generated observation about any hallucinations or threshold violations]

## Comparison with Paper Results
[Compare findings to the paper's 5 simulations if applicable]
```

Save to `Assets/Scripts/RedRunner/DDA/TestResults/evaluation_report.md`

### For `analyze-logs`

1. Read all JSON files from the DDA logs directory
2. Track across sessions:
   - **Difficulty trajectory**: How profile values change over time
   - **Death rate convergence**: Is death rate stabilizing toward a target?
   - **Session length trend**: Are runs getting longer for struggling players?
   - **Adjustment frequency**: How often is DDA triggered?
   - **Oscillation detection**: Is the system ping-ponging between easy and hard?

3. Generate charts as ASCII art or suggest visualization code:

```
Death Rate Over Sessions:
Session 1: ████████████████████ 0.80
Session 2: ████████████████     0.65
Session 3: ████████████         0.50
Session 4: ██████████           0.42
Session 5: █████████            0.38  ← converging to target 0.35
```

## Quality Criteria (from the paper)

The paper identifies these positive outcomes to check for:

1. **Pertinent adjustments**: Changes are relevant to the symptom
2. **No hallucinations**: Output format and values are valid
3. **Values coupled to context**: LLM considers current variable values, not just symptom
4. **Finer-grained than rules**: LLM can produce nuanced adjustments rules can't anticipate
5. **Smooth transitions**: Changes don't cause jarring difficulty spikes

Flag any simulation where these criteria are violated.
