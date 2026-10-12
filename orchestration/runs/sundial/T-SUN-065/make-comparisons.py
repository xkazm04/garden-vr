"""Compose untouched DialG1 captures beside the matching framing and paper-card artboard."""
from pathlib import Path
from PIL import Image, ImageDraw

run = Path(__file__).resolve().parent
repo = run.parents[3]
reference = repo / 'shared/assets/art-reference/A2-05-field-notebook-1.png'
artboard = repo / 'docs/art/artboards/field-notebook/3-reflection.jpg'

for capture in ('DialG1-catch-up', 'DialG1-catch-up-late', 'DialG1-catch-up-nine-habits'):
    images = [Image.open(path).convert('RGB') for path in (reference, run / (capture + '.png'))]
    assert all(im.size == (1824, 1024) for im in images)
    board = Image.new('RGB', (3648, 1064), '#f3eee2')
    draw = ImageDraw.Draw(board)
    for i, (im, label) in enumerate(zip(images, ('Owner reference / DialG1 framing', capture + ' / Unity PlayMode'))):
        board.paste(im, (i * 1824, 40))
        draw.text((i * 1824 + 16, 12), label, fill='#2a2622')
    board.save(run / (capture + '-reference.jpg'), quality=94, subsampling=0)

board = Image.new('RGB', (3648, 1064), '#f3eee2')
draw = ImageDraw.Draw(board)
for i, (path, label) in enumerate(((artboard, 'Reflection artboard / paper-card language (no B8 artboard exists)'),
                                  (run / 'DialG1-catch-up.png', 'B8 catch-up card / Unity PlayMode'))):
    board.paste(Image.open(path).convert('RGB'), (i * 1824, 40))
    draw.text((i * 1824 + 16, 12), label, fill='#2a2622')
board.save(run / 'catch-up-artboard.jpg', quality=94, subsampling=0)
print('Wrote four side-by-sides; full 1824 x 1024 panels, no crop or reframing.')
