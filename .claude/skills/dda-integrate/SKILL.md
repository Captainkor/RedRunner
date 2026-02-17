---
name: dda-integrate
description: Wire LLM difficulty outputs to actual RedRunner game systems. Handles the Effector phase — mapping DifficultyProfile values to TerrainGenerationSettings, RedCharacter fields, and block probabilities.
argument-hint: [wire|verify|debug]
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# DDA Integration Skill

You help students connect their LLM-generated DifficultyProfile values to actual game systems in RedRunner. This is the Effector/Execute phase of MAPE-K.

## Arguments

- `wire <variable-name>` — Write the code to connect a specific difficulty variable to its game system
- `wire all` — Wire all standard difficulty variables
- `verify` — Read current integration code and check all variables are properly connected
- `debug` — Identify and fix issues with difficulty not taking effect in-game
- If no argument, default to `verify`

## Variable-to-System Mapping

Each difficulty variable must be wired to specific game systems. Here is the complete mapping:

### 1. runSpeed → RedCharacter.m_RunSpeed

**Challenge**: `m_RunSpeed` is a `[SerializeField] protected float` — not directly accessible from outside.

**Options** (present to student):
- **Option A** (recommended): Add a public setter to `RedCharacter`:
  ```csharp
  // In RedCharacter.cs, add to the Properties region:
  public void SetRunSpeed(float speed)
  {
      m_RunSpeed = Mathf.Clamp(speed, 3f, 12f);
  }
  ```
- **Option B**: Use `[field: SerializeField]` with auto-property
- **Option C**: Reflection (not recommended for production)

**File**: `Assets/Scripts/RedRunner/Characters/RedCharacter.cs`

### 2. jumpStrength → RedCharacter.m_JumpStrength

Same pattern as runSpeed — needs a public setter added to RedCharacter:
```csharp
public void SetJumpStrength(float strength)
{
    m_JumpStrength = Mathf.Clamp(strength, 6f, 15f);
}
```

**File**: `Assets/Scripts/RedRunner/Characters/RedCharacter.cs`

### 3. enemyDensity → Block Probabilities in TerrainGenerationSettings

**How it works**: `TerrainGenerationSettings.MiddleBlocks` is a `Block[]` where each `Block` has a `m_Probability` field. Enemy-containing blocks should have their probability scaled.

**Approach**:
1. At runtime, identify which blocks contain enemies (check for `Enemy` components in children)
2. Scale their `Probability` relative to `enemyDensity`
3. Must modify `Block.Probability` to have a setter, or track original values and apply multiplier

```csharp
// In DifficultyEffector.cs:
void ApplyEnemyDensity(float density)
{
    foreach (var block in m_TerrainSettings.MiddleBlocks)
    {
        if (block.GetComponentInChildren<Enemy>(true) != null)
        {
            block.Probability = block.BaseProbability * density;
        }
    }
}
```

**Requires adding to Block.cs**:
```csharp
// Store original probability for scaling
private float m_BaseProbability;
public float BaseProbability => m_BaseProbability;

void Awake()
{
    m_BaseProbability = m_Probability;
}

// Add setter
public virtual float Probability
{
    get { return m_Probability; }
    set { m_Probability = value; }
}
```

**Files**: `Assets/Scripts/RedRunner/TerrainGeneration/Block.cs`, `Assets/Scripts/RedRunner/TerrainGeneration/TerrainGenerationSettings.cs`

### 4. gapFrequency → Gap Block Probabilities

Same approach as enemyDensity but targeting blocks that represent gaps/pits. Students need to identify which block prefabs are gap blocks by examining the prefabs in `Assets/Prefabs/Blocks/`.

### 5. sawProbability, spikeProbability → Individual Enemy Block Weights

Within enemy-containing blocks, scale probability of blocks that specifically contain `Saw` vs `Spike` components.

### 6. coinDensity → Coin Spawn in Blocks

**Approach**: Blocks that contain coins should have coin spawn counts scaled.

**Options**:
- Scale number of coin children active in block prefabs
- Add a density parameter to coin-spawning blocks
- Use ObjectPool spawn rates

### 7. platformHeightVariance → Block Y-Offset Randomization

**Approach**: Modify `TerrainGenerator.CreateBlock()` to add random Y offset:

```csharp
// In DefaultTerrainGenerator.cs or TerrainGenerator.cs:
public override bool CreateBlock(Block blockPrefab, Vector3 position)
{
    // Apply height variance from DDA
    float variance = DDAManager.Singleton?.CurrentProfile?.platformHeightVariance ?? 0f;
    if (variance > 0f)
    {
        position.y += Random.Range(-variance, variance);
    }
    return base.CreateBlock(blockPrefab, position);
}
```

**File**: `Assets/Scripts/RedRunner/TerrainGeneration/DefaultTerrainGenerator.cs`

## Integration Checklist

When verifying, check each variable:

- [ ] **runSpeed**: Setter exists on RedCharacter, called from DifficultyEffector
- [ ] **jumpStrength**: Setter exists on RedCharacter, called from DifficultyEffector
- [ ] **enemyDensity**: Block probabilities updated, original values preserved for rescaling
- [ ] **gapFrequency**: Gap blocks identified and probability scaled
- [ ] **sawProbability**: Saw-containing blocks isolated and probability adjusted
- [ ] **spikeProbability**: Spike-containing blocks isolated and probability adjusted
- [ ] **coinDensity**: Coin spawn rate connected to profile value
- [ ] **platformHeightVariance**: Y-offset applied in terrain generation
- [ ] **Values clamped**: All applied values clamped to defined thresholds
- [ ] **Timing**: Changes applied BEFORE terrain generation resumes (between runs, not mid-run)
- [ ] **Logging**: All changes logged with before/after values for debugging

## When to Apply Changes

**Important**: Difficulty changes should be applied:
- After player death, during the death screen delay (`DeathCrt` coroutine has 1.5s wait)
- Before `GameManager.Reset()` is called for the next run
- NOT mid-run (would cause inconsistent terrain)

The integration point is in `DDAManager`, which subscribes to `Character.IsDead`:

```csharp
void OnPlayerDied(bool isDead)
{
    if (isDead)
    {
        // Collect metrics → Analyze → LLM request → Apply
        StartCoroutine(RunDDACycle());
    }
}

IEnumerator RunDDACycle()
{
    string metrics = m_MetricsCollector.GetMetricsJson();
    string symptom = m_Analyzer.GetSymptomString();

    yield return m_PolicyEngine.RequestAdjustment(metrics, symptom, (newProfile) =>
    {
        m_Effector.ApplyProfile(newProfile);
    });
}
```

## Common Debug Issues

When debugging with `debug`:

1. **Changes not visible**: Check if terrain from previous run is still on screen (old blocks persist until destroyed by range check)
2. **Probability changes ignored**: `TerrainGenerator.ChooseFrom()` reads `Block.Probability` at generation time — if blocks are prefabs, changes must be on the prefab instances, not scene instances
3. **Speed feels unchanged**: `RedCharacter.m_CurrentRunSpeed` uses SmoothDamp — may take time to reflect new base speed
4. **Values resetting**: `GameManager.OnReset` may re-initialize values — ensure DDA applies AFTER reset
5. **NullReferenceException**: Singletons may not be initialized yet — use null-conditional (`?.`) or check order of execution
