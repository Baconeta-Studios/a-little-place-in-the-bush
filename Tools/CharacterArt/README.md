# Character Art

`paint_pekapeka.py` paints `Assets/Images/Characters/Pekapeka.png` in code, in the style of the hand-painted characters: grainy watercolour shapes, watercolour pooling at the edges, darker patches, chalky white crayon marks and a chalky white outline.

```bash
pip install numpy scipy pillow
python Tools/CharacterArt/paint_pekapeka.py Assets/Images/Characters/Pekapeka.png
```

Shapes are lists of points near the top (body, head, ears, wings, feet), and colours are named just above them. Change one and run it again. A hand-painted version can replace the PNG at any time, and anything pointing at it keeps working.

`paint_firefly.py` paints `Assets/Images/Characters/Firefly.png`, the firefly companion's picture on the forage screen. Just a dark dot, a glowing tail and a soft glow, kept as simple as possible on purpose.

```bash
python Tools/CharacterArt/paint_firefly.py Assets/Images/Characters/Firefly.png
```
