#!/bin/zsh
set -eu

HERE=${0:A:h}
RAW="$HERE/raw"
OUT="$HERE/t1_layouts_updated.png"
FONT="/System/Library/Fonts/Hiragino Sans GB.ttc"

magick -size 2400x1040 xc:'#f7f9fc' \
  \( "$RAW/01_left_right_front-1.png" -resize '746x582^' -gravity center -extent 746x582 \) -gravity northwest -geometry +40+128 -composite \
  \( "$RAW/02_up_down_front.png" -resize '746x582^' -gravity center -extent 746x582 \) -gravity northwest -geometry +827+128 -composite \
  \( "$RAW/04_up_down_depth_front.png" -resize '746x582^' -gravity center -extent 746x582 \) -gravity northwest -geometry +1614+128 -composite \
  \( "$RAW/03_up_down_oblique.png" -resize '276x216^' -gravity center -extent 276x216 -bordercolor white -border 12 \) -gravity northwest -geometry +1248+442 -composite \
  \( "$RAW/05_up_down_depth_side.png" -resize '276x216^' -gravity center -extent 276x216 -bordercolor white -border 12 \) -gravity northwest -geometry +2035+442 -composite \
  -font "$FONT" -fill '#172033' -gravity north -pointsize 42 -annotate +0+24 'ディスプレイ配置とターゲット候補位置' \
  -fill '#566176' -pointsize 24 -annotate +0+82 '各ディスプレイ：横3列 × 縦2行（合計6点）' \
  -gravity northwest -fill 'rgba(23,32,51,0.90)' -draw 'roundrectangle 1260,454 1386,494 8,8' \
  -draw 'roundrectangle 2047,454 2173,494 8,8' \
  -fill white -pointsize 19 -annotate +1273+463 '斜め視点' -annotate +2060+463 '側面視点' \
  -gravity north -fill '#172033' -pointsize 38 -annotate -787+742 '左右配置' -annotate +0+742 '上下配置' -annotate +787+742 '上下＋奥行き配置' \
  -fill '#566176' -pointsize 23 -annotate -787+794 '同じ距離で左右に配置' -annotate +0+794 '同じ距離で上下に配置' -annotate +787+794 '上下に加えて前後距離を変更' \
  -gravity northwest -fill '#e9f6fc' -stroke '#8bd9f6' -strokewidth 2 -draw 'roundrectangle 380,872 2020,984 20,20' \
  -stroke none -fill '#00b8f5' -draw 'circle 455,928 469,928' \
  -font "$FONT" -fill '#172033' -pointsize 25 -annotate +492+895 '候補位置（正規化座標）' \
  -fill '#37445a' -pointsize 24 -annotate +492+934 'x = 0.10, 0.50, 0.90　／　y = 0.20, 0.80　／　2ディスプレイで合計12点' \
  "$OUT"

echo "$OUT"
