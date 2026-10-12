from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[4]
run = Path(__file__).resolve().parent
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 23)
reference = root / 'shared/assets/art-reference/A2-05-field-notebook-1.png'
for shot in sorted(run.glob('DialG1-*.png')):
    frame = Image.open(shot).convert('RGB')
    board = root / 'docs/art/artboards/field-notebook' / (
        '4-record.jpg' if 'week' in shot.stem or 'seven-lines' in shot.stem else '3-reflection.jpg')
    for kind, source in [('artboard', board), ('reference', reference)]:
        ref = Image.open(source).convert('RGB')
        assert ref.size == frame.size == (1824, 1024)
        canvas = Image.new('RGB', (frame.width * 2, frame.height + 48), '#211f1b')
        canvas.paste(ref, (0, 48))
        canvas.paste(frame, (frame.width, 48))
        draw = ImageDraw.Draw(canvas)
        draw.text((16, 8), str(source.relative_to(root)), font=font, fill='#eee8da')
        draw.text((frame.width + 16, 8), 'B7 real PlayMode capture: ' + shot.stem, font=font, fill='#eee8da')
        output = run / (shot.stem + '-' + kind + '-side-by-side.jpg')
        canvas.save(output, quality=93, subsampling=0)
        print(output.name, '1824x1024 per panel, no crop or rescale')
