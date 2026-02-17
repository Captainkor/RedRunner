# Red Runner

Red Runner, Awesome Platformer Game.

It is now free and open source and always will be. :clap: :tada:

| [:sparkles: Getting Started](#getting-started) | [:rocket: Download](#download) | [:camera: Screenshots](#screenshots) |
| --------------- | -------- | ----------- |

<p align="center">
  <img src="https://img.itch.zone/aW1hZ2UvMTU4NTg4LzcyNzg3Mi5wbmc=/original/AU5pWY.png" />
</p>

[:camera: See Screenshots](#screenshots)

[:movie_camera: **See the Trailer**](https://youtu.be/MO2yJhgtMes)

## Getting Started

Follow the below instructions to get started with Red Runner source code:

1. [Make sure you have all Requirements](#requirements)
2. [Download Source Code](#download)
3. Open Project in Unity and Enjoy!

## Requirements

Make sure you have the below requirements before starting:

- [Unity Game Engine](https://unity3d.com) version **6000.2.6f2 (Unity 6)**
- Basic Knowledge about Unity and C#

## Download

You can get access to Red Runner source code by using one of the following ways:

- [:sparkles: Download Source Code](https://github.com/BayatGames/RedRunner/archive/master.zip)
- [:fire: Download Source Code from Itch.io](https://bayat.itch.io/red-runner)
- Clone the repository locally:

```bash
git clone https://github.com/BayatGames/RedRunner.git
```

Also you can the build version of the Red Runner using the following ways:

- [:star: Download from Itch.io](https://bayat.itch.io/red-runner)

## Screenshots

<p align="center">
  <img src="https://img.itch.zone/aW1hZ2UvMTU4NTg4LzczMjc2NS5wbmc=/original/HipFLL.png" />
</p>

<p align="center">
  <img src="https://img.itch.zone/aW1hZ2UvMTU4NTg4LzczMjc2MC5wbmc=/original/mb636l.png" />
</p>

<p align="center">
  <img src="https://img.itch.zone/aW1hZ2UvMTU4NTg4LzczMjc2OS5wbmc=/original/UyNp4U.png" />
</p>

<p align="center">
  <img src="https://img.itch.zone/aW1hZ2UvMTU4NTg4LzczMjc3My5wbmc=/original/RAoMpO.png" />
</p>

## Credits

- Graphics: [Free Platform Game Assets](https://bayat.itch.io/platform-game-assets)
- Save System: [Save Game Pro - Save Everything](https://bayat.itch.io/save-game-pro-save-everything)
- Game Engine: [Unity](https://unity3d.com/)
- Thanks to all of the game development community for their awesome help.

## Related

- [Awesome Unity](https://github.com/RyanNielson/awesome-unity) - A curated list of awesome Unity assets, resources, and more.
- [Games on GitHub](https://github.com/leereilly/games/) - 🎮 A list of popular/awesome videos games, add-ons, maps, etc. hosted on GitHub. Any genre. Any platform. Any engine.
- [GameDev Resources](https://github.com/Kavex/GameDev-Resources) - 🎮 🎲 A wonderful list of Game Development resources.
- [UnityLibrary](https://github.com/UnityCommunity/UnityLibrary) - 📚 Library of all kind of scripts, snippets & shaders for Unity.

## DDA (Dynamic Difficulty Adjustment) System

This fork includes **LLM-based Dynamic Difficulty Adjustment** using the MAPE-K loop architecture from the DDA-MAPEKit paper (SBGames 2024).

### Features

- **LLM-Powered Adjustments**: Uses Gemini (free) or Claude (paid) to intelligently adjust game difficulty
- **Interactive Configuration**: Custom wizards to configure variables, metrics, prompts, and mappings
- **Multi-Provider Support**: Compare Gemini vs Claude performance side-by-side
- **MAPE-K Architecture**: Monitor → Analyze → Plan → Execute with Knowledge base
- **SPAR Prompting**: Structured prompts following best practices from the paper
- **Offline Testing**: Simulate player profiles without running the game
- **Evaluation Tools**: Compare LLM-based vs rule-based approaches

### Quick Start with DDA

1. **Scaffold components**: `/dda-scaffold configure` (interactive) or `/dda-scaffold all` (defaults)
2. **Configure metrics**: `/dda-metrics add custom` to track player behavior
3. **Generate prompt**: `/dda-prompt generate custom` to customize LLM reasoning
4. **Wire systems**: `/dda-integrate configure-mapping <variable>` for each difficulty variable
5. **Test offline**: `/dda-test simulate custom` with synthetic player profiles
6. **Evaluate**: `/dda-evaluate compare` to compare LLM vs rule-based

See `CLAUDE.md` for detailed documentation on all DDA skills and workflows.

### DDA Skills

| Skill | Purpose |
|-------|---------|
| `dda-scaffold` | Generate MAPE-K components with optional configuration wizard |
| `dda-metrics` | Instrument player behavior metrics with custom collection methods |
| `dda-prompt` | Create and optimize SPAR-format prompts for LLM policy engine |
| `dda-integrate` | Wire difficulty variables to game systems with flexible mappings |
| `dda-test` | Simulate player profiles and test adjustments offline |
| `dda-evaluate` | Compare LLM vs rule-based approaches with detailed reports |

### Configuration

All DDA settings are stored in `Assets/Scripts/RedRunner/DDA/dda_config.json`, allowing you to:
- Version control your DDA strategy
- Share configurations with teammates
- Switch between different setups easily

## Resources

[:rocket: Patreon](https://www.patreon.com/BayatGames)

[:newspaper: Support and News](https://github.com/BayatGames/Support)

## License

MIT @ [Bayat Games](https://github.com/BayatGames)

Made with :heart: by [Bayat Games](https://github.com/BayatGames)
