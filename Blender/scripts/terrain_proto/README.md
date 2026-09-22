# Terrain prototype (numpy + Blender)

Python mirror of `Assets/Scripts/Runtime/World/TerrainNoise.cs` + `TerrainDataBake.cs` for fast look
iteration outside Unity. Same hash, gradient noise, eroded fBm, crater population and landmarks.

```bash
pip install numpy pillow bpy          # bpy = Blender as a Python module (4.4+)
python hill.py Mars,Luna,Earth,Europa,Belt hill.png      # relief hillshades, seconds
python render_terrain.py Mars,Luna out 7                 # Cycles renders: close (ortho ~play) + wide
VIEWS=close,mid,wide RES=1280 python render_terrain.py Earth out 7
```

`shade.py` approximates `SM_PlanetGround` v2 (palette per body matches `PlanetaryMapDressing.ApplyNaturalPalette`).
If you change a generator in C#, change it here too (and vice versa).
