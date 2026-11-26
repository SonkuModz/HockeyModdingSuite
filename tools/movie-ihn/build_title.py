"""Builds a WebM in the NHL 24 title movie's own track layout (nhl/data/common/movie/bootflow/press_start_reveal).

usage: python build_title.py SOURCE.mp4 OUT.webm [--quality 9 --audio-seconds 8.44 --size 23128851]

EA's file has three tracks and the MovieTexture2Asset has a block table for each, with fixed counts:
  1  VP8 picture   1920x1080, 30 fps, 260 frames         language und
  2  VP8 alpha     260 frames, one flat level (255 = opaque)       language eaa
  3  Vorbis        8 channels, 48 kHz, exactly 622 packets (one per block, no lacing)
The packet count is hit by the Vorbis quality and the sound's length (q 9 for 8.44 s gives 622 for clip 1125); the
script checks every count and the size, then `nhl27kit movie-replace NAME --webm OUT.webm --apply` pads it to the
movie's size and rewrites all three tables (and the sound's CodecPrivate offset/size).
"""
import argparse, os, subprocess, sys, tempfile

ap = argparse.ArgumentParser()
ap.add_argument('source'); ap.add_argument('out')
ap.add_argument('--quality', type=float, default=9)
ap.add_argument('--audio-seconds', type=float, default=8.44)
ap.add_argument('--size', type=int, default=23_128_851)
ap.add_argument('--frames', type=int, default=260)
ap.add_argument('--packets', type=int, default=622)
ap.add_argument('--alpha', type=int, default=255)
a = ap.parse_args()
here = os.path.dirname(os.path.abspath(__file__))
tmp = tempfile.mkdtemp(prefix='ihntitle-')
F, FPS, SEC = a.frames, 30, a.frames / 30


def ff(*args):
    r = subprocess.run(['ffmpeg', '-v', 'error', '-y', *args], capture_output=True, text=True)
    if r.returncode: sys.exit(r.stderr)


def count(path, sel, what):
    r = subprocess.run(['ffprobe', '-v', 'error', '-select_streams', sel, '-count_packets', '-show_entries',
                        'stream=nb_read_packets', '-of', 'csv=p=0', path], capture_output=True, text=True)
    return int(r.stdout.strip().splitlines()[0])


# 3: sound, 8 channels (the clip's stereo on the front pair), exactly N packets
aud = os.path.join(tmp, 'a.webm')
ff('-i', a.source, '-vn', '-filter_complex', '[0:a]aresample=48000,pan=7.1|FL=c0|FR=c1[a]', '-map', '[a]',
   '-c:a', 'libvorbis', '-q:a', str(a.quality), '-t', str(a.audio_seconds), aud)
n = count(aud, 'a:0', 'audio')
print('sound packets', n, os.path.getsize(aud), 'B')
if n != a.packets: sys.exit(f'sound has {n} packets, the movie needs {a.packets}: change --audio-seconds / --quality')

# 2: alpha, one flat level, 3 key frames like EA's
alp = os.path.join(tmp, 'alpha.webm')
lv = f'{a.alpha:02X}' * 3
ff('-f', 'lavfi', '-i', f'color=c=0x{lv}:s=1920x1080:r={FPS}', '-frames:v', str(F), '-pix_fmt', 'yuv420p',
   '-c:v', 'libvpx', '-b:v', '60k', '-g', str(F // 3 + 1), '-auto-alt-ref', '0', alp)
print('alpha', count(alp, 'v:0', 'v'), 'frames', os.path.getsize(alp), 'B')

# 1: picture, two-pass VP8 to the room that's left
room = a.size - os.path.getsize(aud) - os.path.getsize(alp) - 120_000
vid = os.path.join(tmp, 'v.webm')
rate = int(room * 8 / SEC * 0.93)
for attempt in range(5):
    flt = f'[0:v]scale=1920:1080:force_original_aspect_ratio=increase,crop=1920:1080,setsar=1,fps={FPS},tpad=stop_mode=clone:stop_duration={SEC + 1},format=yuv420p[v]'
    for p in (1, 2):
        ff('-i', a.source, '-filter_complex', flt, '-map', '[v]', '-an', '-frames:v', str(F), '-c:v', 'libvpx', '-b:v', str(rate),
           '-qmin', '0', '-qmax', '63', '-undershoot-pct', '95', '-overshoot-pct', '0', '-deadline', 'good', '-cpu-used', '4' if p == 1 else '1',
           '-auto-alt-ref', '0', '-g', str(F // 3 + 1), '-pass', str(p), '-passlogfile', os.path.join(tmp, 'pl'),
           *(['-f', 'null', 'NUL'] if p == 1 else [vid]))
    if os.path.getsize(vid) <= room: break
    rate = int(rate * room / os.path.getsize(vid) * 0.95)
print('picture', count(vid, 'v:0', 'v'), 'frames', os.path.getsize(vid), 'B at', rate // 1000, 'kbps')

# mux: picture 1, alpha 2 ("eaa"), sound 3
ff('-i', vid, '-i', alp, '-i', aud, '-map', '0:v', '-map', '1:v', '-map', '2:a', '-c', 'copy',
   '-metadata:s:v:0', 'language=und', '-metadata:s:v:1', 'language=eaa', '-metadata:s:a:0', 'language=und',
   '-map_metadata', '-1', '-f', 'webm', a.out)
counts = [count(a.out, s, s) for s in ('v:0', 'v:1', 'a:0')]
size = os.path.getsize(a.out)
print('out', a.out, size, 'B, blocks', counts)
if counts != [F, F, a.packets] or size > a.size: sys.exit('does not fit the movie')
