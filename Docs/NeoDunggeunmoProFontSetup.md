# NeoDunggeunmo Pro Font Setup

- Source font: `Assets/Fonts/NeoDunggeunmoPro/NeoDunggeunmoPro-Regular.ttf`
- Generated TMP asset: `Assets/Fonts/NeoDunggeunmoPro/NeoDunggeunmoPro SDF.asset`
- Primary targets:
  - `Assets/Scenes/RMainEscape_Lobby.unity`
  - `Assets/Prefabs/IRHudCanvas.prefab`

Use `Tools/Main Escape/Fonts/Apply NeoDunggeunmo Pro` to re-apply the project font setup after updating the upstream TTF.

For unattended runs, use the Unity batch entry point:

```text
NeoDunggeunmoProFontSetup.RunBatch
```

The setup utility also assigns the generated TMP font as the default font in `TMP Settings` and keeps `LiberationSans SDF` as a fallback for characters that NeoDunggeunmo Pro does not cover.
