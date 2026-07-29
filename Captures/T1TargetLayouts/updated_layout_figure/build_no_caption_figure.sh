#!/bin/zsh
set -eu

HERE=${0:A:h}
SRC="$HERE/final"
PUB="$HERE/publication"
mkdir -p "$PUB"

for file in \
  01_left_right.png \
  02_up_down_front.png \
  03_up_down_side.png \
  04_up_down_depth_front.png \
  05_up_down_depth_side.png
do
  magick "$SRC/$file" -resize '1200x675^' -gravity center -extent 1200x675 "$PUB/$file"
done

magick -size 2400x600 xc:white \
  \( "$SRC/01_left_right.png" -resize '800x600^' -gravity center -extent 800x600 \) -gravity northwest -geometry +0+0 -composite \
  \( "$SRC/02_up_down_front.png" -resize '800x600^' -gravity center -extent 800x600 \) -gravity northwest -geometry +800+0 -composite \
  \( "$SRC/04_up_down_depth_front.png" -resize '800x600^' -gravity center -extent 800x600 \) -gravity northwest -geometry +1600+0 -composite \
  \( "$SRC/03_up_down_side.png" -resize '300x225^' -gravity center -extent 300x225 -bordercolor white -border 8 \) -gravity northwest -geometry +1268+343 -composite \
  \( "$SRC/05_up_down_depth_side.png" -resize '300x225^' -gravity center -extent 300x225 -bordercolor white -border 8 \) -gravity northwest -geometry +2068+343 -composite \
  "$HERE/t1_layouts_no_captions.png"

echo "$HERE/t1_layouts_no_captions.png"
