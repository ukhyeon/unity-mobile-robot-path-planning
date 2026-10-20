#!/usr/bin/env bash
# Recordings/<clip>/f_%04d.png -> docs/images/demo_<clip>.gif
# 프레임은 Tools > Path Planning Benchmark > Record Demo Frames (또는 RecordDemoBatch)로 만든다.
set -euo pipefail
cd "$(dirname "$0")/.."
OUT=docs/images
FONT=/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf
SIZE=${SIZE:-480}
mkdir -p "$OUT"

label() { # text color y
  echo "drawtext=fontfile=$FONT:text='$1':fontcolor=$2:fontsize=26:x=14:y=$3:box=1:boxcolor=0xfcfcfb@0.85:boxborderw=6"
}

make_gif() { # clip filters
  local clip=$1 filters=$2
  ffmpeg -loglevel error -y -framerate 20 -i "Recordings/$clip/f_%04d.png" \
    -vf "$filters,scale=$SIZE:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=96:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle" \
    "$OUT/demo_$clip.gif"
  echo "$OUT/demo_$clip.gif $(du -h "$OUT/demo_$clip.gif" | cut -f1)"
}

make_gif astar    "$(label 'A*' 0x2a78d6 12)"
make_gif dijkstra "$(label 'Dijkstra' 0xeb6834 12)"
make_gif rrt      "$(label 'RRT' 0x1baf7a 12)"
make_gif rrtstar  "$(label 'RRT*' 0xeda100 12)"
make_gif bug      "$(label 'Bug0' 0xe87ba4 12),$(label 'Bug1' 0x008300 52),$(label 'Bug2' 0x6250d6 92)"
