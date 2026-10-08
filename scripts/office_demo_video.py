"""Deterministic 10 fps timeline export using FFmpeg; no Rhino dependency."""
import json
from pathlib import Path
import shutil
import subprocess

FPS = 10
WIDTH = 800


def timeline_frames(beats):
    if not beats:
        raise ValueError('Empty timeline')
    for beat in beats:
        ticks = beat['ticks']
        if not isinstance(ticks, int) or ticks < 1:
            raise ValueError('Each beat must have positive integer ticks')
        for _ in range(ticks):
            yield beat


def ffmpeg(args):
    subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', *args], check=True)


def render_frame(source, output, caption):
    """Lay out the captured viewport and a separate caption strip."""
    from PIL import Image, ImageDraw, ImageFont
    with Image.open(source) as captured:
        width, height = captured.size
        top = 0
        crop_height = height
        view = captured.crop((0, top, width, top + crop_height)).convert('RGB')
        out_height = round(crop_height * WIDTH / width / 2) * 2
        view = view.resize((WIDTH, out_height), Image.Resampling.LANCZOS)
    frame = Image.new('RGB', (WIDTH, out_height + 80), '#f8faf8')
    frame.paste(view, (0, 0))
    draw = ImageDraw.Draw(frame)
    draw.text((28, out_height+16), caption, fill='#283b38', font=ImageFont.load_default(size=24))
    draw.text((WIDTH-24, out_height+59), 'MCP4Rhino / Rhino 8 / Time-lapse',
              fill='#667872', font=ImageFont.load_default(size=12), anchor='ra')
    frame.save(output)


def encode(directory):
    beats = json.loads((directory / 'timeline.json').read_text())
    frames = list(timeline_frames(beats))
    work = directory / 'encoded_frames'
    work.mkdir(exist_ok=True)
    rendered = {}
    for i, beat in enumerate(frames):
        key = (beat['file'], beat['caption'])
        output = work / f'frame_{i:04d}.png'
        if key not in rendered:
            source = directory / beat['file']
            if not source.is_file():
                raise FileNotFoundError(source)
            render_frame(source, output, beat['caption'])
            rendered[key] = output
        elif rendered[key] != output:
            shutil.copyfile(rendered[key], output)
    inputs = ['-framerate', str(FPS), '-i', str(work / 'frame_%04d.png'), '-frames:v', str(len(frames))]
    ffmpeg([*inputs, '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p',
            '-movflags', '+faststart', str(directory / 'demo.mp4')])
    palette = 'split[a][b];[a]palettegen=max_colors=96[p];[b][p]paletteuse=dither=none'
    ffmpeg([*inputs, '-filter_complex', palette, '-loop', '0', str(directory / 'demo.gif')])
    if (directory / 'demo.gif').stat().st_size > 5 * 1024 * 1024:
        raise RuntimeError('GIF exceeds 5 MiB budget; reduce resolution before publishing')
    print(f'Exported {len(frames)/FPS:g}s GIF and MP4 to {directory}', flush=True)
