---
name: dda-metrics
description: Add, modify, or debug player metrics instrumentation in the PlayerMetricsCollector for RedRunner DDA. Use when you need to track new player behaviors or fix metric collection.
argument-hint: [add|list|debug|export]
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# DDA Metrics Instrumentation Skill

You help students instrument player behavior metrics for the MAPE-K Monitor phase.

## Arguments

- `add <metric-name>` — Add a new metric to PlayerMetricsCollector (e.g., `add hesitationScore`)
- `list` — Show all currently tracked metrics and where they hook into game systems
- `debug` — Read PlayerMetricsCollector.cs, identify potential bugs (missing unsubscribes, null refs, division by zero)
- `export` — Add or update JSON export functionality for session logs
- If no argument, default to `list`

## Where Metrics Hook Into RedRunner

These are the available data sources in the existing codebase:

### Events to Subscribe To
| Source | Event/Property | Data Available |
|--------|---------------|----------------|
| `GameManager.OnScoreChanged` | Static event | `(float newScore, float highScore, float lastScore)` |
| `GameManager.OnReset` | Static event | Run reset signal |
| `GameManager.OnAudioEnabled` | Static event | Audio toggle |
| `Character.IsDead` | `Property<bool>` | Death state changes |
| `GameManager.Singleton.m_Coin` | `Property<int>` | Coin count changes |
| `GroundCheck.OnGrounded` | Instance event | Player landed |

### Pollable State (check in Update)
| Source | Field/Property | Data |
|--------|---------------|------|
| `GameManager.Singleton.gameRunning` | bool | Is game active |
| `GameManager.Singleton.gameStarted` | bool | Has game started |
| Character transform.position | Vector3 | Current player position |
| `RedCharacter.Speed` | Vector2 | Current velocity magnitude |
| `RedCharacter.GroundCheck.IsGrounded` | bool | Grounded state |
| `TerrainGenerator.Singleton.CurrentX` | float | Terrain generation frontier |

### Derived Metrics Students Can Compute
| Metric | Derivation |
|--------|-----------|
| `distanceTraveled` | Max X position reached in current run |
| `deathCount` | Increment on `IsDead` transition to true |
| `totalRunTime` | `Time.time` delta from run start to death |
| `avgTimeBetweenDeaths` | `totalRunTime / deathCount` |
| `coinsCollected` | Delta of `m_Coin.Value` from run start |
| `jumpsCount` | Count Jump button presses (poll `CrossPlatformInputManager.GetButtonDown("Jump")`) |
| `jumpsPerSecond` | `jumpsCount / totalRunTime` |
| `avgSpeed` | Accumulate `Speed.x` per frame, divide by frame count |
| `hesitationScore` | Time spent with near-zero horizontal velocity |
| `deathPositionVariance` | Stddev of X positions where player dies across runs |
| `maxHeightReached` | Max Y position in a run |
| `enemiesEncountered` | Count enemy collisions (both fatal and non-fatal) |

## Coding Conventions for Metrics

```csharp
namespace RedRunner.DDA
{
    public class PlayerMetricsCollector : MonoBehaviour
    {
        // Fields use m_ prefix
        [Header("Metrics")]
        [SerializeField] private float m_DistanceTraveled;

        // Subscribe in Awake or Start
        void Start()
        {
            GameManager.OnScoreChanged += OnScoreChanged;
            GameManager.OnReset += OnReset;
            // Use Property<T>.AddEvent for observable values
            m_Character.IsDead.AddEvent(OnDeathStateChanged, this);
        }

        // Always unsubscribe in OnDestroy
        void OnDestroy()
        {
            GameManager.OnScoreChanged -= OnScoreChanged;
            GameManager.OnReset -= OnReset;
        }

        // JSON export matching paper format
        public string GetMetricsJson()
        {
            return JsonUtility.ToJson(new MetricsData
            {
                distanceTraveled = m_DistanceTraveled,
                deathCount = m_DeathCount,
                // ...
            });
        }
    }

    [System.Serializable]
    public class MetricsData
    {
        public float distanceTraveled;
        public int deathCount;
        // ...
    }
}
```

## When Adding a New Metric

1. Read `Assets/Scripts/RedRunner/DDA/PlayerMetricsCollector.cs` to see existing metrics
2. Identify the data source from the tables above
3. Add the `m_` field with `[SerializeField]` if it should be visible in Inspector
4. Add subscription in `Start()` or polling in `Update()`
5. Add unsubscription in `OnDestroy()` if using events
6. Add to `MetricsData` serializable class
7. Add to `GetMetricsJson()` output
8. Add to `ResetMetrics()` to clear on new run

## When Debugging

Check for these common issues:
- Missing `OnDestroy()` unsubscribe (causes null ref after scene reload)
- Division by zero in averages (check `deathCount > 0` and `totalRunTime > 0`)
- Metrics not resetting between runs (must subscribe to `GameManager.OnReset`)
- Polling in `Update()` when game is paused (`Time.timeScale = 0` means `Time.deltaTime = 0`)
- Use `Time.unscaledDeltaTime` for time tracking if needed during pause

## Session Log Export Format

When implementing export, use this JSON structure per session:

```json
{
    "sessionId": "guid",
    "timestamp": "ISO-8601",
    "runNumber": 1,
    "symptomClassification": "slightly.high",
    "metrics": {
        "distanceTraveled": 142.5,
        "deathCount": 3,
        "totalRunTime": 45.2,
        "avgTimeBetweenDeaths": 15.07,
        "coinsCollected": 12,
        "jumpsCount": 34,
        "jumpsPerSecond": 0.75
    },
    "difficultyProfileBefore": { ... },
    "difficultyProfileAfter": { ... }
}
```

Save to `Application.persistentDataPath + "/DDALogs/"` directory.
