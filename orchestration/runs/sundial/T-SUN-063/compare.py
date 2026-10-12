from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

run = Path(__file__).resolve().parent
repo = run.parents[3]
reference = Image.open(repo / 'docs/art/artboards/field-notebook/2-breaks.jpg').convert('RGB')
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 30)
for part in ('eye', 'palm', 'pinch'):
    preview = Image.open(run / f'DialG1-break-{part}-preview.png').convert('RGB')
    assert reference.size == preview.size == (1824, 1024)
    board = Image.new('RGB', (3648, 1084), '#eee8db')
    board.paste(reference, (0, 60))
    board.paste(preview, (1824, 60))
    draw = ImageDraw.Draw(board)
    draw.text((24, 14), 'Artboard moment 2 | Field Notebook', fill='#2a2622', font=font)
    draw.text((1848, 14), f'B6 {part} presentation preview | controller NOT integrated', fill='#2a2622', font=font)
    board.save(run / f'break-{part}-side-by-side.png')
    print(f'Wrote break-{part}-side-by-side.png: unchanged 1824x1024 panels')
