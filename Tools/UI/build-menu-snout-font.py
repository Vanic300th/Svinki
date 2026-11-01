"""Build a menu-only face: preserve every metric and outer contour, replace O counters with two holes."""
from pathlib import Path
import sys
sys.path.insert(0, str(Path('Tools/UI/.deps/concept-fonts').resolve()))
sys.path.insert(0, str(Path('ArtSource/UI/Concepts/2026-10-25').resolve()))
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from snout_font import replace_counters

folder = Path('Assets/_Project/Content/UI/ProjectSvin/Fonts')
original = TTFont(folder / 'ClimateCrisis-Regular.ttf')
variant = TTFont(folder / 'ClimateCrisis-Regular.ttf')
replace_counters(variant)
# Keep the derived face's family name distinct from the original font.
for record in variant['name'].names:
    if record.nameID in (1, 3, 4, 6, 16):
        text = 'SvinMenuSnout-Regular' if record.nameID == 6 else 'Svin Menu Snout'
        record.string = text.encode(record.getEncoding())
changed = {original.getBestCmap()[ord(c)] for c in 'oOоО'}
assert original['hmtx'].metrics == variant['hmtx'].metrics
for name in original.getGlyphOrder():
    a, b = original.getGlyphSet()[name], variant.getGlyphSet()[name]
    pa, pb = DecomposingRecordingPen(original.getGlyphSet()), DecomposingRecordingPen(variant.getGlyphSet())
    a.draw(pa); b.draw(pb)
    # Accented composite letters must retain their original O component.
    if name not in changed and pa.value != pb.value:
        pen = TTGlyphPen(None); pa.replay(pen); variant['glyf'][name] = pen.glyph()
        pb = DecomposingRecordingPen(variant.getGlyphSet()); variant.getGlyphSet()[name].draw(pb)
    assert (pa.value != pb.value) == (name in changed), name
variant.save(folder / 'SvinMenuSnout-Regular.ttf')
print('PASS: only o/O/о/О contours changed; all advance widths and other glyphs preserved.')
