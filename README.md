# Nicotine

Smokable cigarettes for SPT 4.1 (EFT 0.16.9), with Fika support. The four cigarette packs (Apollo Soyuz, Malboro, Wilston, Strike) hold 20 cigarettes each, with a first-person animation: light up, one or two drags, flick it away.

## What a cigarette does

- **Item:** +3 energy, -0.5 hydration, 3 min painkiller.
- **Rush:** heals 60 HP over 1 min. Stress Resistance +15 and Attention, Perception and Endurance +10 for 4 min, then a 1 min comedown. One rush at a time (5 min cooldown).
- **Relief:** ends hand tremor and tunnel vision, shortens contusion and stun, and cuts other stimulants' side effects. Positive buffs already running last a little longer.
- **Overdoing it:** a 4th cigarette within 2 minutes starts a coughing fit.

## Addiction

Each cigarette adds addiction. From 20%, going without brings cravings and withdrawal episodes (restlessness, shakes, headaches, tunnel vision and worse), which grow over raid time. Smoking ends them, and addiction slowly drops while you stay off.

## Install

Copy into your SPT folder:

- `BepInEx/plugins/Nicotine/`: `Nicotine.Client.dll` and `nicotine.bundle` (every player)
- `SPT_Runtime/user/mods/Nicotine/`: `Nicotine.Server.dll` (the server)

All numbers are in `SPT_Runtime/user/mods/Nicotine/config.json`, written on first start. Restart SPT after editing it.

## Build

The repo goes inside the SPT folder (`<SPT>/Nicotine`), or pass `-p:SptRoot=<SPT folder>`.

```
dotnet build Nicotine.sln -c Release
```

This deploys the plugin, the bundle (`unity/Build/nicotine.bundle`) and the server mod into the SPT install. Add `-p:DeployToSpt=false` to only build.
