"""Draft memo 1 (Shadow Hurry-Up) in Japanese, Simplified and Traditional Chinese, rendered with candidate fonts.
For choosing fonts only; the wording is a first draft to be reviewed by native speakers.

  python sample_cjk.py <fonts_dir> <out_dir>
"""
import json, sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
W, H = 2048, 1152

JA = {
 "customers": "影が遅れるお客さん（よく来る）", "s1": "影がずっと遅れてついてくるって。角を曲がっても影はまだ角にいるらしい",
 "s2": "夕暮れが一番ひどい。昼はまあまあ", "s3": "振り向くと床に何もなくてゾッとするって",
 "s4": "座ると影がワンテンポ遅れて座る", "s5": "敷居をまたいでも影は外に引っかかってるって！",
 "s6": "急いで出かけてからだって", "s7": "熱なし。咳なし。", "s8": "塩をまいてみたけど効かなかったって（そりゃそう）",
 "name": "影せかし薬", "name_note": "今月だけで三回目",
 "i1": "星夜タケ", "i1_a": "軸が真っ青なやつ", "i1_b": "かさに大きな白い点", "i1_c": "てっぺんにかさぶた一つ",
 "i1_d": "かさの裏がナイフで切ったみたいに平ら", "i1_e": "割ると中は白い", "i1_f": "湿ってたら使えない",
 "i1_g": "一本で銀貨二枚（高い）",
 "i2": "リボン羽", "i2_a": "先が二股", "i2_b": "横じま", "i2_c": "いちばん細くて長い子", "i2_d": "オレンジっぽいの",
 "i2_e": "軸がよくしなる", "i2_f": "ほこりを払ってから入れる", "i2_g": "折らないこと",
 "i3": "夕焼け水晶", "i3_a": "先が丸い", "i3_b": "二色", "i3_c": "真ん中に太い柱がいくつか", "i3_d": "小さいかけらがどっさり",
 "i3_e": "叩くと鈍い音", "i3_f": "粉が舞うので注意",
 "cook": "三つとも鍋に入れて煮る", "cook_note": "日が沈む前に飲ませること！！", "aside": "お昼までに終わらせる"}
HANS = {
 "customers": "影子总是慢半拍的客人（常来）", "s1": "影子远远地跟在后面。拐过街角，影子还在原来的角落",
 "s2": "黄昏时最严重。白天还好", "s3": "回头一看地上空空的，吓一跳",
 "s4": "坐下时影子慢一拍才坐下", "s5": "跨过门槛，影子还卡在门外！",
 "s6": "据说是赶路之后开始的", "s7": "不发烧。不咳嗽。", "s8": "撒了盐也没用（当然了）",
 "name": "催影药", "name_note": "这个月第三次了",
 "i1": "星夜菇", "i1_a": "菌柄深蓝的那个", "i1_b": "菌盖上有大白点", "i1_c": "顶上有块痂",
 "i1_d": "菌盖底下像刀切的一样平", "i1_e": "掰开里面是白的", "i1_f": "受潮就不能用",
 "i1_g": "一朵两枚银币（贵）",
 "i2": "缎带羽", "i2_a": "尖端分叉", "i2_b": "横条纹", "i2_c": "最细最长的那根", "i2_d": "带点橙色的",
 "i2_e": "羽轴很容易弯", "i2_f": "掸掉灰再放", "i2_g": "别折断",
 "i3": "晚霞水晶", "i3_a": "尖端圆钝", "i3_b": "两种颜色", "i3_c": "中间几根粗柱子", "i3_d": "一堆小碎块",
 "i3_e": "敲起来声音闷", "i3_f": "会掉粉，小心",
 "cook": "三样都放进锅里煮", "cook_note": "一定要在太阳下山前喝！！", "aside": "午饭前做完"}
HANT = {
 "customers": "影子總是慢半拍的客人（常來）", "s1": "影子遠遠地跟在後面。拐過街角，影子還在原來的角落",
 "s2": "黃昏時最嚴重。白天還好", "s3": "回頭一看地上空空的，嚇一跳",
 "s4": "坐下時影子慢一拍才坐下", "s5": "跨過門檻，影子還卡在門外！",
 "s6": "據說是趕路之後開始的", "s7": "不發燒。不咳嗽。", "s8": "撒了鹽也沒用（當然了）",
 "name": "催影藥", "name_note": "這個月第三次了",
 "i1": "星夜菇", "i1_a": "菌柄深藍的那個", "i1_b": "菌蓋上有大白點", "i1_c": "頂上有塊痂",
 "i1_d": "菌蓋底下像刀切的一樣平", "i1_e": "掰開裡面是白的", "i1_f": "受潮就不能用",
 "i1_g": "一朵兩枚銀幣（貴）",
 "i2": "緞帶羽", "i2_a": "尖端分叉", "i2_b": "橫條紋", "i2_c": "最細最長的那根", "i2_d": "帶點橙色的",
 "i2_e": "羽軸很容易彎", "i2_f": "撣掉灰再放", "i2_g": "別折斷",
 "i3": "晚霞水晶", "i3_a": "尖端圓鈍", "i3_b": "兩種顏色", "i3_c": "中間幾根粗柱子", "i3_d": "一堆小碎塊",
 "i3_e": "敲起來聲音悶", "i3_f": "會掉粉，小心",
 "cook": "三樣都放進鍋裡煮", "cook_note": "一定要在太陽下山前喝！！", "aside": "午飯前做完"}


def wrap(draw, text, font, width):
    rows, row = [], ""
    for ch in text:  # CJK breaks between any characters
        if row and draw.textlength(row + ch, font=font) > width:
            rows.append(row)
            row = ch
        else:
            row += ch
    return rows + [row]


def render(texts, font_path, out):
    layout = json.load(open(ROOT / "Assets/Resources/MemoText/layout.json", encoding="utf-8"))
    img = Image.open(ROOT / "Assets/Resources/MemoText/Sheets/POT_01.png").convert("RGB")
    sw, sh = img.size
    m = layout["margin"]
    draw = ImageDraw.Draw(img)
    missing = set()
    font_any = ImageFont.truetype(str(font_path), 40)
    for p in layout["sheets"]["POT_01"]:
        text = texts[p["key"]]
        for ch in text:
            if font_any.getmask(ch).getbbox() is None and not ch.isspace():
                missing.add(ch)
        x0 = (m + p["box"][0] * (1 - 2 * m)) * sw
        y0 = (m + p["box"][1] * (1 - 2 * m)) * sh
        x1 = (m + p["box"][2] * (1 - 2 * m)) * sw
        y1 = (m + p["box"][3] * (1 - 2 * m)) * sh
        size = int(min(y1 - y0, p["rowHeight"] * (1 - 2 * m) * sh * 1.5))
        while size > 6:
            font = ImageFont.truetype(str(font_path), size)
            rows = wrap(draw, text, font, x1 - x0)
            if len(rows) * size * 1.1 <= (y1 - y0) * 1.12:
                break
            size -= 1
        for i, row in enumerate(rows):
            draw.text((x0, y0 + i * size * 1.1), row, fill=(34, 28, 25), font=font)
    img.save(out)
    return missing


fonts, out = Path(sys.argv[1]), Path(sys.argv[2])
out.mkdir(parents=True, exist_ok=True)
jobs = [("ja", JA, "Yomogi-Regular.ttf"), ("ja", JA, "ZenKurenaido-Regular.ttf"), ("ja", JA, "KleeOne-Regular.ttf"),
        ("ja", JA, "Xiaolai-Regular.ttf"),
        ("zh-Hans", HANS, "Xiaolai-Regular.ttf"), ("zh-Hans", HANS, "LXGWWenKai-Regular.ttf"),
        ("zh-Hant", HANT, "Xiaolai-Regular.ttf"), ("zh-Hant", HANT, "LXGWWenKaiTC-Regular.ttf")]
for lang, texts, font in jobs:
    missing = render(texts, fonts / font, out / f"{lang}_{Path(font).stem}.png")
    print(lang, font, "missing:", "".join(sorted(missing)) or "none")
