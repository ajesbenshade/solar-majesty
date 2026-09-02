# Copilot 3D input images

**Do not use turnaround sheet crops** — Copilot 3D needs clean single-subject hero shots.

## Generate in Grok Imagine

1. Open [`Docs/GROK_IMAGINE_COPILOT3D_PROMPTS.md`](../Docs/GROK_IMAGINE_COPILOT3D_PROMPTS.md)
2. Paste the **Rules** block + one **subject** block per run
3. Save output here as `SM_Unit_{Name}_Hero.png`

## Upload to Copilot 3D

Upload the `_Hero.png` file (not `*_ThreeQuarter.png` crops).

Download GLB → `Blender/imports/copilot3d/` → run `sm_import_copilot3d.py`.
