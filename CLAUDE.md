# RedRunner - Project Guide

## Project Overview
RedRunner is an open-source 2D platformer game built with Unity (C#). The player controls a red character running through procedurally generated terrain, collecting coins, avoiding enemies, and achieving high scores. Originally by Bayat Games, licensed under MIT.

## Tech Stack
- **Engine**: Unity 6 (6000.3.8f1)
- **Language**: C# (.NET)
- **Key Packages**: Post Processing, Timeline, uGUI (TextMeshPro), CrossPlatformInput (Standard Assets)
- **Save System**: SaveGameFree (binary serialization, in `Assets/SaveGameFree/`)

## Project Structure
```
Assets/
  Scripts/
    RedRunner/
      GameManager.cs          # Singleton. Game state, score, coins, audio, save/load
      UIManager.cs            # Singleton. Screen management, cursor handling
      AudioManager.cs         # Singleton. Sound playback
      Characters/
        Character.cs           # Abstract base class for characters
        RedCharacter.cs        # Player character (movement, jump, die, skeleton ragdoll)
      Enemies/                 # Enemy types: Eye, Mace, Saw, Spike, Water
      Collectables/            # Coin, Chest, Collectable (with Interface/)
      TerrainGeneration/
        TerrainGenerator.cs    # Abstract base. Procedural terrain gen with block system
        DefaultTerrainGenerator.cs
        Block.cs, DefaultBlock.cs, BackgroundBlock.cs, BackgroundLayer.cs
        TerrainGenerationSettings.cs  # ScriptableObject for terrain config
      UI/                      # UI components (buttons, text, screens, windows)
        UIScreen/              # Screen system (Loading, Start, End, Pause, InGame)
      Utilities/               # CameraController, GroundCheck, Path system
      ObjectPool/              # Object pooling system
      Skeleton/                # Ragdoll skeleton system for death animation
    Utils/
      Property.cs              # Generic observable property with auto-cleanup callbacks
      PropertyEvent.cs
      UtilsUI.cs
    CameraControl.cs
  Editor/
    RedRunner/TerrainGeneration/  # Custom editor tools for terrain
  Scenes/
    Play.unity                 # Main gameplay scene
    Creation.unity             # Level creation/editing scene
  Prefabs/                     # Character, enemies, blocks, particles, audio manager
  Sprites/, Animations/, Sounds/, Materials/, Shaders/, Fonts/
```

## Architecture Patterns

### Singletons
Core managers use a manual singleton pattern (static `m_Singleton` field, set in `Awake`, destroyed on duplicate):
- `GameManager.Singleton` - game state, score, save/load
- `UIManager.Singleton` - UI screen management
- `AudioManager.Singleton` - audio playback
- `TerrainGenerator.Singleton` - procedural terrain
- `CameraController.Singleton` - camera following

### Event System
- `GameManager` exposes static events: `OnReset`, `OnScoreChanged`, `OnAudioEnabled`
- `Property<T>` (in `Utils/Property.cs`) is a custom observable value with auto-cleanup tied to MonoBehaviour lifecycle. Used for `Character.IsDead`, `GameManager.m_Coin`, etc.

### Terrain Generation
Procedural infinite runner terrain using a block-based system:
- `TerrainGenerationSettings` (ScriptableObject) defines Start/Middle/End block prefabs with probabilities
- Blocks are generated ahead of the player and destroyed behind
- Background layers generate independently with parallax

### Input
Uses Unity's `CrossPlatformInput` (Standard Assets) for Horizontal, Jump, Guard, Fire, Roll.

## Coding Conventions
- **Namespaces**: `RedRunner`, `RedRunner.Characters`, `RedRunner.Enemies`, `RedRunner.Collectables`, `RedRunner.TerrainGeneration`, `RedRunner.UI`, `RedRunner.Utilities`
- **Field naming**: `m_` prefix for private/serialized fields (e.g., `m_RunSpeed`, `m_MainCharacter`)
- **Properties**: Explicit get-only properties wrapping `m_` fields
- **Serialization**: `[SerializeField]` on private fields, `[Header()]` and `[Space]` for Inspector organization
- **Regions**: Code organized with `#region` blocks (Fields, Properties, MonoBehaviour Messages, Public/Private Methods, Events)
- **Indentation**: Mixed (tabs in some files, spaces in others). Follow the style of the file being edited.

## Build & Run
- Open in Unity 6 (6000.3.8f1+)
- Main scene: `Assets/Scenes/Play.unity`
- No external build scripts; use Unity Editor to build
- No unit test framework in project scripts (Unity Test Framework package is present but unused by project code)

## Common Tasks
- **Add new enemy**: Create script in `Assets/Scripts/RedRunner/Enemies/`, extend `Enemy`, add prefab in `Assets/Prefabs/Enemies/`
- **Add new block type**: Extend `Block` or `DefaultBlock`, configure in `TerrainGenerationSettings` asset
- **Modify player movement**: Edit `Assets/Scripts/RedRunner/Characters/RedCharacter.cs` (speeds, jump, physics)
- **Add UI screen**: Add enum value to `UIScreenInfo`, create screen prefab, register in UIManager's screen list
- **Modify save data**: Edit `GameManager.Awake()` (load) and `OnApplicationQuit()` (save), uses string keys with `SaveGame`

## DDA (Dynamic Difficulty Adjustment) Skills

This project includes Claude Code skills for implementing LLM-based Dynamic Difficulty Adjustment following the MAPE-K loop architecture from the DDA-MAPEKit paper (SBGames 2024).

### LLM Provider Setup

The `LLMPolicyEngine` supports multiple LLM providers via the `LLMProvider` enum. **Gemini Flash is the default** (free tier, ideal for classroom demos).

| Provider | Default Model | Cost | API Key |
|----------|--------------|------|---------|
| **Gemini** (default) | `gemini-2.0-flash` | Free (10 RPM, ~1000 req/day) | [Get key at Google AI Studio](https://aistudio.google.com/apikey) |
| Claude | `claude-sonnet-4-5-20250929` | $3/$15 per 1M tokens | [Get key at Anthropic Console](https://console.anthropic.com/) |

**For classroom use**: Select **Gemini** in the Inspector dropdown and paste your free API key. No billing account required.

### Available Skills

All skills now support **interactive configuration** for customizing your DDA setup.

| Skill | Command | Purpose |
|-------|---------|---------|
| **dda-scaffold** | `/dda-scaffold [component\|all\|configure]` | Scaffold MAPE-K components. Use `configure` for interactive wizard to choose variables, metrics, LLM provider, and wiring preferences |
| **dda-prompt** | `/dda-prompt [generate\|improve\|test\|show\|compare-providers]` | Generate and iterate on SPAR-format prompts. Use `generate custom` for wizard or `compare-providers` for Gemini/Claude optimization |
| **dda-metrics** | `/dda-metrics [add\|list\|debug\|export\|configure-collection]` | Instrument player metrics. Use `add custom` for wizard to design custom metrics with guided collection setup |
| **dda-test** | `/dda-test [simulate\|batch\|validate\|compare-providers]` | Test LLM adjustments offline. Use `simulate custom` for custom player profiles or `compare-providers` to test both LLM providers |
| **dda-evaluate** | `/dda-evaluate [compare\|report\|analyze-logs\|configure-baseline]` | Evaluate DDA quality. Use `configure-baseline` to define rule-based comparison or `report custom` for custom analysis |
| **dda-integrate** | `/dda-integrate [wire\|verify\|debug\|list-mappings]` | Wire LLM output to game systems. Use `configure-mapping <variable>` to choose integration method or `list-mappings` to view config |

### Recommended Workflows

#### Quick Start (Default Setup)
1. `/dda-scaffold all` — Generate all component skeletons
2. `/dda-metrics list` — Review available data sources, then `add` custom metrics
3. `/dda-prompt generate` — Create the SPAR prompt
4. `/dda-integrate wire all` — Connect difficulty variables to game systems
5. `/dda-test batch` — Run simulations with synthetic player profiles
6. `/dda-evaluate compare` — Compare LLM vs rule-based outputs
7. `/dda-prompt improve` — Iterate on prompt based on evaluation results

#### Custom Setup (Full Control)
1. `/dda-scaffold configure` — **Interactive wizard** to choose variables, metrics, LLM provider, thresholds, and wiring methods
2. `/dda-metrics add custom` — Design custom metrics (e.g., "hesitation score", "near-miss count")
3. `/dda-metrics configure-collection <metric>` — Choose how each metric is collected (event, polling, derived)
4. `/dda-prompt generate custom` — Customize SPAR prompt (CoT style, examples, aggressiveness)
5. `/dda-integrate configure-mapping <variable>` — Choose integration method per variable (setter, reflection, custom)
6. `/dda-integrate list-mappings` — Verify all mappings are configured
7. `/dda-test simulate custom` — Create and test custom player profiles
8. `/dda-evaluate configure-baseline` — Define custom rule-based baseline
9. `/dda-evaluate report custom` — Generate custom evaluation report

#### Multi-Provider Comparison
1. Set up both Gemini and Claude API keys in LLMPolicyEngine
2. `/dda-prompt compare-providers` — Generate optimized prompts for each provider
3. `/dda-test compare-providers` — Run same scenarios on both providers
4. `/dda-evaluate compare` — Analyze which provider performs better for your use case

### Configuration File

All custom settings are saved to `Assets/Scripts/RedRunner/DDA/dda_config.json`:
- Selected difficulty variables and their mappings
- Custom metrics and collection methods
- Symptom classification thresholds
- Integration preferences (setters vs reflection vs custom)
- Prompt customizations
- Rule-based baseline configuration

This allows you to:
- Reuse your setup across skill invocations
- Version control your DDA configuration
- Share configurations with teammates
- Quickly switch between different DDA strategies

### DDA File Locations
- DDA scripts: `Assets/Scripts/RedRunner/DDA/`
- DDA namespace: `RedRunner.DDA`
- Test results: `Assets/Scripts/RedRunner/DDA/TestResults/`
- Session logs: `Application.persistentDataPath/DDALogs/`

### Key Difficulty Variables

| Variable | Threshold | Maps To |
|----------|-----------|---------|
| enemyDensity | [0.0, 1.0] | Block probability weights (enemy blocks) |
| gapFrequency | [0.0, 0.8] | Block probability weights (gap blocks) |
| runSpeed | [3.0, 12.0] | RedCharacter.m_RunSpeed |
| jumpStrength | [6.0, 15.0] | RedCharacter.m_JumpStrength |
| sawProbability | [0.0, 1.0] | Saw-block probability weight |
| spikeProbability | [0.0, 1.0] | Spike-block probability weight |
| coinDensity | [0.1, 1.0] | Coin spawn rate in blocks |
| platformHeightVariance | [0.0, 3.0] | Y-offset randomization in terrain gen |

## Important Notes
- The project uses `Rigidbody2D.linearVelocity` (Unity 6 API, replaces deprecated `velocity`)
- Death triggers skeleton ragdoll system (`Skeleton.SetActive`) which disables animator and collider
- Score is based on player's X position (distance traveled)
- Game state is controlled via `Time.timeScale` (0 = paused, 1 = running)
