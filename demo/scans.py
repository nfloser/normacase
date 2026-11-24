"""Rasterbilder für die fiktiven Fallakten: Scans, Handyfotos, Faxe und Bild-PDFs.

Sie zeigen, wie NormaCase mit den Formaten umgeht, die in der Begutachtung tatsächlich ankommen. Alle Inhalte
sind frei erfunden; jedes Bild trägt den Hinweis auf die Demonstration. Zufall ist fest geseedet, damit die
Dateien reproduzierbar bleiben.
"""
import random
import time

from PIL import Image, ImageDraw, ImageFilter, ImageFont

DPI = 150
A4 = (1240, 1754)  # A4 bei 150 dpi
FONT_DIR = "/usr/share/fonts/truetype/dejavu/"
NOTICE = "Fiktives Dokument · alle Personen und Angaben frei erfunden · NormaCase-Demonstration"


def font(size, bold=False, serif=False, oblique=False):
    name = "DejaVuSerif" if serif else "DejaVuSans"
    if bold and oblique:
        name += "-BoldOblique" if not serif else "-BoldItalic"
    elif bold:
        name += "-Bold"
    elif oblique:
        name += "-Oblique" if not serif else "-Italic"
    try:
        return ImageFont.truetype(FONT_DIR + name + ".ttf", size)
    except OSError:
        return ImageFont.load_default()


def wrap(draw, text, fnt, width):
    words, lines, line = text.split(), [], ""
    for w in words:
        trial = (line + " " + w).strip()
        if draw.textlength(trial, font=fnt) <= width:
            line = trial
        else:
            lines.append(line)
            line = w
    if line:
        lines.append(line)
    return lines


def letter_page(header, title, blocks, page_no, pages, serif=True, accent="#24496b"):
    """Eine Briefseite als Bild: Kopf, Titel, Absätze oder Tabellen (Liste von (label, text))."""
    img = Image.new("L" if accent is None else "RGB", A4, "white")
    d = ImageDraw.Draw(img)
    body, small, bold = font(26, serif=serif), font(20, serif=serif), font(27, bold=True, serif=serif)
    x, y, width = 120, 110, A4[0] - 240
    d.text((x, y), header, fill=accent or "black", font=font(30, bold=True, serif=serif))
    y += 50
    d.line([(x, y), (x + width, y)], fill=accent or "black", width=3)
    y += 40
    if title:
        for line in wrap(d, title, font(34, bold=True, serif=serif), width):
            d.text((x, y), line, fill="black", font=font(34, bold=True, serif=serif))
            y += 46
        y += 16
    for block in blocks:
        if isinstance(block, tuple) and block[0] == "h":
            y += 10
            d.text((x, y), block[1], fill="black", font=bold)
            y += 42
        elif isinstance(block, tuple) and block[0] == "rows":
            for label, value in block[1]:
                d.text((x, y), label, fill="black", font=bold)
                lines = wrap(d, value, body, width - 420)
                for i, line in enumerate(lines):
                    d.text((x + 420, y + i * 36), line, fill="black", font=body)
                y += max(1, len(lines)) * 36 + 12
                d.line([(x, y - 6), (x + width, y - 6)], fill="#b0b0b0" if accent else 170, width=1)
        else:
            for line in wrap(d, block, body, width):
                d.text((x, y), line, fill="black", font=body)
                y += 36
            y += 18
    footer(d, page_no, pages, "#777777" if accent else 120)
    return img


def footer(d, page_no, pages, fill):
    d.text((A4[0] - 120, A4[1] - 118), f"Seite {page_no} von {pages}", fill=fill, font=font(20), anchor="ra")
    d.text((120, A4[1] - 80), NOTICE, fill=fill, font=font(18))


def scanned(img, seed, angle=0.6, noise=10, gray=True):
    """Leicht schief eingezogen, Graustufen, Rauschen und Kopierschatten am Rand – wie aus dem Flachbettscanner."""
    rnd = random.Random(seed)
    img = img.convert("L") if gray else img.convert("RGB")
    img = img.rotate(angle, resample=Image.BICUBIC, expand=False, fillcolor=255 if gray else (255, 255, 255))
    px = img.load()
    w, h = img.size
    for _ in range(w * h // 60):
        x, y = rnd.randrange(w), rnd.randrange(h)
        v = px[x, y]
        if gray:
            px[x, y] = max(0, min(255, v + rnd.randint(-noise, noise)))
    d = ImageDraw.Draw(img)
    for i in range(18):  # Schatten an der Bindekante
        shade = 255 - (18 - i) * 6
        d.line([(i, 0), (i, h)], fill=shade if gray else (shade, shade, shade))
    return img.filter(ImageFilter.GaussianBlur(0.35))


def fax(img, seed, sent, sender, page_no=1, pages=1):
    """Fax-Qualität: 1 Bit, grobe Auflösung, Kopfzeile des Faxgeräts (Sendezeit, Absender, Seite)."""
    rnd = random.Random(seed)
    img = img.convert("L").resize((A4[0] * 2 // 3, A4[1] * 2 // 3))
    img = img.rotate(rnd.uniform(-0.8, 0.8), fillcolor=255)
    d = ImageDraw.Draw(img)
    d.text((40, 12), f"{sent}   VON: {sender}   +49 421 000{seed % 1000:03d}   S. {page_no}/{pages}", fill=0, font=font(15))
    return img.point(lambda v: 0 if v < 165 else 255, mode="1")


def photo(img, seed):
    """Handyfoto eines Blatts auf dem Tisch: perspektivisch leicht verzogen, warmes Licht, Verlauf, JPEG."""
    rnd = random.Random(seed)
    sheet = img.convert("RGB").resize((A4[0] * 3 // 4, A4[1] * 3 // 4))
    w, h = sheet.size
    canvas = Image.new("RGB", (w + 160, h + 160), (118, 96, 74))
    # Holzmaserung
    d = ImageDraw.Draw(canvas)
    for y in range(0, canvas.size[1], 6):
        tone = 100 + int(18 * ((y // 6) % 5) / 5) + rnd.randint(-4, 4)
        d.line([(0, y), (canvas.size[0], y)], fill=(tone + 18, tone, tone - 20), width=3)
    dx = rnd.randint(-18, 18)
    coeffs = (1, dx / h, 0, rnd.uniform(-0.02, 0.02), 1, 0, rnd.uniform(-0.00004, 0.00004), rnd.uniform(-0.00003, 0.00003))
    sheet = sheet.transform(sheet.size, Image.PERSPECTIVE, coeffs, Image.BICUBIC, fillcolor=(118, 96, 74))
    canvas.paste(sheet, (80, 80))
    canvas = canvas.rotate(rnd.uniform(-2.5, 2.5), resample=Image.BICUBIC, fillcolor=(118, 96, 74))
    # Lichtverlauf von oben nach unten
    light = Image.linear_gradient("L").resize(canvas.size)
    warm = Image.new("RGB", canvas.size, (255, 236, 205))
    canvas = Image.composite(canvas, Image.blend(canvas, warm, 0.18), light)
    return canvas.filter(ImageFilter.GaussianBlur(0.6))


def form_page(title, fields, signature, page_no=1, pages=1):
    """Ausgefülltes Antragsformular mit Kästchen und „handschriftlichen“ Einträgen (kursiv, blau)."""
    img = Image.new("RGB", A4, "white")
    d = ImageDraw.Draw(img)
    x, y, width = 110, 110, A4[0] - 220
    d.rectangle([x, y, x + width, y + 90], outline="black", width=3)
    d.text((x + 24, y + 22), title, fill="black", font=font(34, bold=True))
    y += 130
    hand = font(30, oblique=True)
    for label, value, kind in fields:
        if kind == "box":
            d.rectangle([x, y + 4, x + 30, y + 34], outline="black", width=2)
            if value:
                d.line([(x + 4, y + 8), (x + 26, y + 30)], fill="#1f3f9a", width=4)
                d.line([(x + 26, y + 8), (x + 4, y + 30)], fill="#1f3f9a", width=4)
            d.text((x + 50, y + 4), label, fill="black", font=font(25))
            y += 54
        else:
            d.text((x, y), label, fill="black", font=font(22))
            d.line([(x, y + 70), (x + width, y + 70)], fill="black", width=1)
            d.text((x + 12, y + 30), value, fill="#1f3f9a", font=hand)
            y += 96
    y += 30
    d.text((x, y), "Ort, Datum, Unterschrift", fill="black", font=font(22))
    d.line([(x, y + 90), (x + 640, y + 90)], fill="black", width=1)
    d.text((x + 16, y + 36), signature, fill="#1f3f9a", font=font(32, oblique=True))
    footer(d, page_no, pages, "#777777")
    return img


def medication_plan(person, born, rows, issuer, date):
    """Medikationsplan als Tabelle (Bildschirmfoto/PNG aus der Praxissoftware)."""
    w, h = 1600, 360 + 70 * len(rows)
    img = Image.new("RGB", (w, h), "white")
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w, 90], fill="#2f5d7c")
    d.text((40, 24), "Medikationsplan", fill="white", font=font(38, bold=True))
    d.text((w - 620, 30), f"ausgedruckt am {date} · {issuer}", fill="white", font=font(22))
    d.text((40, 120), f"für: {person}   geb. {born}", fill="black", font=font(26, bold=True))
    cols = [("Wirkstoff / Präparat", 40), ("Stärke", 640), ("früh", 860), ("mittag", 990), ("abend", 1120), ("nacht", 1250), ("Hinweis", 1380)]
    y = 190
    d.rectangle([30, y - 10, w - 30, y + 50], fill="#e4ecf3")
    for label, cx in cols:
        d.text((cx, y), label, fill="black", font=font(24, bold=True))
    y += 70
    for name, strength, dose, note in rows:
        parts = (dose.split("-") + ["0", "0", "0", "0"])[:4]
        values = [name, strength, *parts, note]
        for (_, cx), v in zip(cols, values):
            d.text((cx, y), v, fill="black", font=font(24))
        d.line([(30, y + 52), (w - 30, y + 52)], fill="#c5ced6", width=1)
        y += 70
    d.text((40, h - 60), NOTICE, fill="#7a858c", font=font(18))
    return img


def to_image_pdf(images, path):
    """Bild-PDF wie aus einem Scanner: keine Textebene, nur Seitenbilder."""
    first, rest = images[0], images[1:]
    fixed = time.gmtime(1793491200)  # 01.11.2026, reproduzierbar
    first.save(path, "PDF", resolution=DPI, save_all=True, append_images=rest, title="Scan",
               creationDate=fixed, modDate=fixed)
