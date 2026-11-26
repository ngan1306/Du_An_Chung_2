from pathlib import Path


OUTPUT_DIR = Path(__file__).resolve().parents[1] / "KissAppTussing" / "wwwroot" / "demo-profiles"

BACKGROUNDS = [
    ("#d7f2ed", "#8acdc1"), ("#fae4d2", "#edac83"), ("#dce8ff", "#a2b9ee"),
    ("#f7e0e9", "#dda0b9"), ("#e5f1d4", "#a7c88a"), ("#f8efcc", "#dfca7a"),
    ("#e4dcf7", "#b8a3df"), ("#d4edf2", "#85c3cf"), ("#f6dfd2", "#d98d70"),
    ("#d9eee1", "#86bf9a"), ("#f5e2ba", "#d8a35a"), ("#e2e9ec", "#9eafb9"),
]
SKIN_TONES = ["#f3c9a9", "#e7b18d", "#d99b73", "#f0d0b4", "#c98562", "#e5b996", "#b97858", "#f5d7bd"]
HAIR_COLORS = ["#34261f", "#493329", "#221d1b", "#76523c", "#a86a42", "#332b48", "#5c3d35", "#202d37"]
SHIRT_COLORS = ["#117c73", "#df796d", "#5879b8", "#c26f98", "#608a62", "#d19a39", "#745c9e", "#477d8b", "#bb7041", "#44745d"]

HAIR_BACK = [
    "M91 157 C72 107 104 55 158 54 C216 53 249 98 231 169 L246 287 Q211 315 189 269 L112 286 Q77 281 78 238 Z",
    "M91 151 C83 95 114 57 161 58 C211 58 238 94 231 147 L220 214 Q199 238 185 202 L108 211 Q80 199 83 171 Z",
    "M85 161 C69 100 108 48 162 53 C222 57 249 105 232 171 L226 260 Q202 282 183 238 L109 255 Q80 242 77 205 Z",
    "M87 155 C76 91 117 47 164 54 C214 59 241 98 231 148 L249 246 Q236 282 207 250 L197 209 L104 220 L90 267 Q68 245 76 203 Z",
    "M89 161 C78 103 111 52 163 56 C218 59 242 105 230 162 L236 220 Q211 248 191 207 L108 219 Q81 207 80 179 Z",
    "M88 157 C74 96 112 49 164 54 C222 59 245 105 231 160 L250 281 Q222 299 203 252 L190 214 L104 228 L86 279 Q65 260 76 208 Z",
    "M88 155 C76 95 114 50 165 55 C218 60 242 100 229 157 L241 235 Q217 259 198 220 L104 220 Q80 211 78 182 Z",
    "M90 161 C71 105 107 49 159 54 C216 58 247 105 231 165 L244 278 Q219 301 199 253 L188 216 L105 225 L84 278 Q65 259 77 205 Z",
]

HAIR_TOP = [
    "M91 147 C88 89 122 58 163 58 C207 58 233 91 231 144 Q209 115 185 101 Q157 129 111 129 L104 161 Z",
    "M89 143 C87 88 120 56 163 57 C208 57 234 92 232 145 Q210 123 192 101 Q163 122 126 120 Q108 130 99 155 Z",
    "M91 145 C89 85 122 53 165 56 C207 58 233 91 231 144 Q213 127 203 99 Q177 125 152 120 Q122 119 101 157 Z",
    "M90 143 C86 87 121 57 164 57 C209 57 235 91 232 145 Q215 110 191 99 Q163 110 140 99 Q116 113 100 157 Z",
    "M90 145 C87 90 118 55 163 56 C212 57 236 94 231 146 Q211 129 192 116 Q165 127 141 111 Q119 117 101 158 Z",
    "M90 147 C87 88 119 54 164 56 C207 58 235 91 232 145 Q211 125 198 101 Q181 120 164 111 Q138 132 112 122 L101 159 Z",
    "M90 145 C87 86 121 56 164 57 C210 58 237 92 231 147 Q217 128 207 99 Q181 117 155 118 Q127 119 103 158 Z",
    "M90 145 C88 88 119 53 164 56 C211 59 236 92 232 146 Q213 113 194 104 Q178 129 145 122 Q119 120 101 159 Z",
]


def make_avatar(number: int) -> str:
    background, highlight = BACKGROUNDS[(number * 7 + number // 3) % len(BACKGROUNDS)]
    skin = SKIN_TONES[(number * 3 + number // 5) % len(SKIN_TONES)]
    hair = HAIR_COLORS[(number * 5 + number // 4) % len(HAIR_COLORS)]
    shirt = SHIRT_COLORS[(number * 3 + number // 2) % len(SHIRT_COLORS)]
    hairstyle = number % len(HAIR_BACK)
    glasses = number % 4 == 1
    freckles = number % 7 in (0, 3)
    earrings = number % 5 in (1, 2)
    face_width = 61 + (number % 5) * 2
    eye_y = 151 + (number % 3) * 3
    eye_gap = 24 + (number % 4)
    mouth_y = 205 + (number % 4) * 2
    accent_x = 50 + (number * 17 % 220)

    details = []
    if glasses:
        details.append(
            f'<g fill="none" stroke="#443b39" stroke-width="4" opacity=".88">'
            f'<rect x="{160-eye_gap-15}" y="{eye_y-9}" width="29" height="22" rx="9"/>'
            f'<rect x="{160+eye_gap-14}" y="{eye_y-9}" width="29" height="22" rx="9"/>'
            f'<path d="M{160-eye_gap+14} {eye_y+1} Q160 {eye_y-4} {160+eye_gap-14} {eye_y+1}"/></g>'
        )
    if freckles:
        details.append(
            '<g fill="#9e624b" opacity=".5">'
            '<circle cx="126" cy="181" r="2"/><circle cx="135" cy="185" r="1.7"/>'
            '<circle cx="194" cy="181" r="2"/><circle cx="185" cy="185" r="1.7"/>'
            '</g>'
        )
    if earrings:
        details.append('<circle cx="94" cy="184" r="5" fill="#efc65f"/><circle cx="226" cy="184" r="5" fill="#efc65f"/>')

    return f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 320 360" role="img" aria-label="Ảnh đại diện minh họa {number:03d}">
<defs><linearGradient id="bg" x1="0" y1="0" x2="1" y2="1"><stop stop-color="{background}"/><stop offset="1" stop-color="{highlight}"/></linearGradient></defs>
<rect width="320" height="360" fill="url(#bg)"/>
<circle cx="{accent_x}" cy="68" r="54" fill="#fff" opacity=".16"/>
<circle cx="276" cy="300" r="78" fill="#fff" opacity=".12"/>
<path d="M0 300 Q70 267 132 295 T320 288 V360 H0Z" fill="#fff" opacity=".12"/>
<path d="M36 360 Q43 267 116 255 L160 278 L204 255 Q277 267 284 360Z" fill="{shirt}"/>
<path d="M132 233 H188 V284 Q160 305 132 284Z" fill="{skin}"/>
<path d="{HAIR_BACK[hairstyle]}" fill="{hair}"/>
<path d="M{160-face_width} 147 C{160-face_width-7} 108 {160-face_width+12} 78 160 77 C{160+face_width-12} 78 {160+face_width+7} 108 {160+face_width} 147 L211 190 Q205 241 160 252 Q115 241 109 190Z" fill="{skin}"/>
<ellipse cx="97" cy="174" rx="10" ry="16" fill="{skin}"/><ellipse cx="223" cy="174" rx="10" ry="16" fill="{skin}"/>
<path d="{HAIR_TOP[hairstyle]}" fill="{hair}"/>
<path d="M{160-eye_gap-11} {eye_y-9} Q{160-eye_gap} {eye_y-15} {160-eye_gap+10} {eye_y-9}" fill="none" stroke="{hair}" stroke-width="4" stroke-linecap="round"/>
<path d="M{160+eye_gap-10} {eye_y-9} Q{160+eye_gap} {eye_y-15} {160+eye_gap+11} {eye_y-9}" fill="none" stroke="{hair}" stroke-width="4" stroke-linecap="round"/>
<ellipse cx="{160-eye_gap}" cy="{eye_y}" rx="4" ry="5" fill="#302925"/><ellipse cx="{160+eye_gap}" cy="{eye_y}" rx="4" ry="5" fill="#302925"/>
<path d="M160 {eye_y+7} Q154 179 159 189 Q163 193 169 189" fill="none" stroke="#ad765f" stroke-width="3" stroke-linecap="round"/>
<path d="M145 {mouth_y} Q160 {mouth_y+10+(number%3)*2} 175 {mouth_y}" fill="none" stroke="#a8515c" stroke-width="4" stroke-linecap="round"/>
{''.join(details)}
<path d="M147 257 Q160 266 173 257" fill="none" stroke="#fff" stroke-width="4" opacity=".8"/>
</svg>'''


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    for number in range(1, 81):
        path = OUTPUT_DIR / f"seed-{number:03d}.svg"
        path.write_text(make_avatar(number), encoding="utf-8")
    print(f"Generated 80 unique demo avatars in {OUTPUT_DIR}")


if __name__ == "__main__":
    main()
