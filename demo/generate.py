"""Erzeugt die fiktiven Vorgänge unter demo/cases/: Falleingabe, Erwartung, Dokumente (PDF + Seitenvorschau).

Aufruf: python demo/generate.py   (braucht reportlab, Pillow und pdftoppm)
Das Ergebnis ist eingecheckt; der Seeder braucht diese Werkzeuge nicht.
"""
import json
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.platypus import Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle
from reportlab import rl_config

sys.path.insert(0, str(Path(__file__).parent))
from cases import CASES, KT, MODULES, PFLEGE, pflege_facts  # noqa: E402

rl_config.invariant = 1  # reproduzierbare PDFs
OUT = Path(__file__).parent / "cases"
KASSE = ("Pflegekasse Nordlicht", "Postfach 10 44 20 · 28044 Bremen")
KK = ("Krankenkasse Nordlicht", "Postfach 10 44 20 · 28044 Bremen")
FOOTER = "Fiktives Dokument · alle Personen und Angaben frei erfunden · NormaCase-Demonstration"

styles = getSampleStyleSheet()
BODY = ParagraphStyle("body", parent=styles["Normal"], fontName="Helvetica", fontSize=10, leading=14)
SMALL = ParagraphStyle("small", parent=BODY, fontSize=8.5, leading=11, textColor=colors.HexColor("#4a5560"))
H1 = ParagraphStyle("h1", parent=BODY, fontName="Helvetica-Bold", fontSize=14, leading=18, spaceAfter=4)
H2 = ParagraphStyle("h2", parent=BODY, fontName="Helvetica-Bold", fontSize=10.5, leading=14, spaceBefore=8, spaceAfter=3)
HEAD = ParagraphStyle("head", parent=BODY, fontName="Helvetica-Bold", fontSize=11, textColor=colors.HexColor("#1d4f7a"))
ACCENT = colors.HexColor("#1d4f7a")
GRID = TableStyle([("FONT", (0, 0), (-1, -1), "Helvetica", 9.5), ("FONT", (0, 0), (-1, 0), "Helvetica-Bold", 9.5),
                   ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#e9eef4")), ("VALIGN", (0, 0), (-1, -1), "TOP"),
                   ("GRID", (0, 0), (-1, -1), 0.4, colors.HexColor("#b8c2cc")), ("LEFTPADDING", (0, 0), (-1, -1), 5),
                   ("RIGHTPADDING", (0, 0), (-1, -1), 5), ("TOPPADDING", (0, 0), (-1, -1), 3), ("BOTTOMPADDING", (0, 0), (-1, -1), 3)])


def german_date(iso):
    y, m, d = iso.split("-")
    return f"{d}.{m}.{y}"


def pdf(path, title, sender, flow):
    def page(canvas, doc):
        canvas.saveState()
        canvas.setFillColor(ACCENT)
        canvas.rect(0, A4[1] - 9 * mm, A4[0], 9 * mm, stroke=0, fill=1)
        canvas.setFillColor(colors.white)
        canvas.setFont("Helvetica-Bold", 9)
        canvas.drawString(20 * mm, A4[1] - 6 * mm, sender)
        canvas.setFillColor(colors.HexColor("#8a949e"))
        canvas.setFont("Helvetica", 7)
        canvas.drawString(20 * mm, 10 * mm, FOOTER)
        canvas.drawRightString(A4[0] - 20 * mm, 10 * mm, f"Seite {doc.page}")
        canvas.restoreState()

    doc = SimpleDocTemplate(str(path), pagesize=A4, leftMargin=20 * mm, rightMargin=20 * mm, topMargin=20 * mm,
                            bottomMargin=18 * mm, title=title, author=sender, creator="NormaCase Demo")
    doc.build(flow, onFirstPage=page, onLaterPages=page)


def address(lines):
    return [Paragraph("<br/>".join(lines), BODY), Spacer(1, 8 * mm)]


def person_block(c):
    name, first, born, addr = c["person"]
    return Table([["Versicherte Person", f"{first} {name}"], ["Geburtsdatum", born], ["Anschrift", addr],
                  ["Versichertennummer", "X" + c["id"][-6:].upper() + "4711"], ["Vorgang", c["id"].upper()]],
                 colWidths=[45 * mm, 125 * mm], style=GRID)


def previews(pdf_path, folder, stem):
    subprocess.run(["pdftoppm", "-png", "-r", "110", str(pdf_path), str(folder / stem)], check=True)
    pages = sorted(folder.glob(f"{stem}-*.png"))
    renamed = []
    for i, p in enumerate(pages, start=1):
        target = folder / f"{stem}.page-{i}.png"
        p.rename(target)
        renamed.append(target.name)
    return renamed


# ------------------------------------------------------------------ Pflege

def pflege_docs(c, folder):
    name, first, born, _ = c["person"]
    docs = []
    clinic, dept, doctor = c["clinic"]
    date = german_date(c["date"])

    p = folder / "auftrag.pdf"
    pdf(p, "Auftrag zur Begutachtung", KASSE[0], [
        *address([KASSE[0], KASSE[1]]),
        Paragraph(f"Bremen, {date}", BODY), Spacer(1, 6 * mm),
        Paragraph("Auftrag zur Feststellung der Pflegebedürftigkeit nach § 18 SGB XI", H1),
        person_block(c), Spacer(1, 5 * mm),
        Paragraph(f"Anlass: <b>{c['title'].split('·')[1].strip()}</b>. Bitte prüfen Sie die Voraussetzungen der "
                  "Pflegebedürftigkeit nach §§ 14, 15 SGB XI und teilen Sie uns das Ergebnis mit Begründung mit. "
                  "Die beigefügten Unterlagen wurden von der versicherten Person bzw. ihren Angehörigen eingereicht.", BODY),
        Paragraph("Anlagen", H2),
        Paragraph(f"Befundbericht {clinic}<br/>Pflegetagebuch der Angehörigen", BODY),
        Spacer(1, 10 * mm), Paragraph("Mit freundlichen Grüßen<br/>Team Pflegeleistungen", BODY)])
    docs.append(("auftrag", "Auftrag der Pflegekasse", p))

    p = folder / "befundbericht.pdf"
    flow = [Paragraph(f"{clinic} · {dept}", HEAD), Spacer(1, 4 * mm),
            Paragraph(f"Ärztlicher Befundbericht zur Vorlage bei der Pflegekasse · {date}", H1), person_block(c),
            Paragraph("Diagnosen", H2),
            Table([["ICD-10", "Diagnose"]] + [list(d) for d in c["diagnoses"]], colWidths=[25 * mm, 145 * mm], style=GRID),
            Paragraph("Befund und Verlauf", H2), Paragraph(c["findings"], BODY),
            Paragraph("Aktuelle Medikation", H2), Paragraph("<br/>".join(c["medication"]), BODY),
            Spacer(1, 10 * mm), Paragraph(f"{doctor}<br/>{dept}", BODY)]
    pdf(p, "Befundbericht", clinic, flow)
    docs.append(("befundbericht", f"Befundbericht {clinic}", p))

    p = folder / "pflegetagebuch.pdf"
    pdf(p, "Pflegetagebuch", f"Pflegetagebuch · {first} {name}", [
        Paragraph(f"Pflegetagebuch für {first} {name}", H1),
        Paragraph("Geführt von Angehörigen über 14 Tage vor der Begutachtung. Zusammenfassung typischer Tage.", SMALL),
        Spacer(1, 4 * mm),
        Table([["Tageszeit", "Unterstützung"]] + [[a, Paragraph(b, BODY)] for a, b in c["diary"]],
              colWidths=[35 * mm, 135 * mm], style=GRID)])
    docs.append(("pflegetagebuch", "Pflegetagebuch der Angehörigen", p))

    p = folder / "erhebung.pdf"
    rows = [["Modul", "Bereich", "Einzelpunkte"]]
    for i, (label, value) in enumerate(zip(MODULES, c["modules"]), start=1):
        rows.append([str(i), Paragraph(label, BODY), "nicht erhoben" if value is None else str(value)])
    flow = [Paragraph("Pflegebegutachtung · Erhebung nach dem Begutachtungsinstrument", H1),
            Paragraph(f"Hausbesuch am {date} · Gutachterin: Pflegefachkraft Anja Meyer", SMALL), Spacer(1, 4 * mm),
            person_block(c), Paragraph("Ergebnis der Erhebung je Modul", H2),
            Table(rows, colWidths=[18 * mm, 117 * mm, 35 * mm], style=GRID),
            Paragraph("Die Umrechnung in gewichtete Punkte und die Zuordnung zu den Schwellenwerten erfolgt "
                      "regelbasiert und nachvollziehbar in NormaCase.", SMALL)]
    if None in c["modules"]:
        flow.append(Paragraph("Hinweis: Modul 4 konnte beim Hausbesuch nicht vollständig erhoben werden "
                              "(Off-Phase). Nachbesuch vereinbart.", BODY))
    pdf(p, "Erhebungsbogen", "Pflegebegutachtung", flow)
    docs.append(("erhebung", "Erhebungsbogen Module 1–6", p))

    if c.get("clarified_module_4") is not None:
        p = folder / "nachtrag.pdf"
        pdf(p, "Nachtrag", "Pflegebegutachtung", [
            Paragraph("Nachtrag zum Erhebungsbogen", H1),
            Paragraph("Nachbesuch am 13.11.2026 in einer On-Phase · Gutachterin: Pflegefachkraft Anja Meyer", SMALL),
            Spacer(1, 4 * mm), person_block(c), Paragraph("Modul 4: Selbstversorgung", H2),
            Paragraph(f"Einzelpunkte: <b>{c['clarified_module_4']}</b>. Hilfe beim Waschen des Unterkörpers, beim "
                      "An- und Auskleiden und beim Duschen; Essen und Trinken selbstständig mit Hilfsmitteln.", BODY)])
        docs.append(("nachtrag", "Nachtrag Modul 4", p))
    return docs


# ------------------------------------------------------------------ Krankenbeförderung

def kt_docs(c, folder):
    name, first, born, addr = c["person"]
    practice, doctor = c["practice"]
    date = german_date(c["date"])
    f = c["facts"]
    docs = []

    def box(v):
        return "[X]" if v.get("truth") == "YES" else "[ ]"

    p = folder / "verordnung.pdf"
    rows = [["Angabe", "Eintrag"],
            ["Behandlung", Paragraph(c["treatment"], BODY)],
            ["Ambulante Behandlung", box(f["ambulatory_treatment"])],
            ["Zwingende medizinische Notwendigkeit", box(f["strict_medical_necessity"]) if f["strict_medical_necessity"].get("kind") == "TRUTH" else "[ ]  (nicht angekreuzt)"],
            ["Merkzeichen laut Angabe", ", ".join(m for m, k in (("aG", "disability_marker_ag"), ("Bl", "disability_marker_bl"), ("H", "disability_marker_h")) if f[k].get("truth") == "YES") or "keine"],
            ["Pflegegrad laut Angabe", str(f["care_grade"]["number"]) if f["care_grade"].get("kind") == "NUMBER" else "nicht angegeben"],
            ["Beförderungsmittel", "Taxi / Mietwagen"]]
    flow = [Paragraph(f"{practice}", HEAD), Spacer(1, 4 * mm),
            Paragraph(f"Verordnung einer Krankenbeförderung · {date}", H1), person_block(c), Spacer(1, 4 * mm),
            Table(rows, colWidths=[70 * mm, 100 * mm], style=GRID)]
    if c.get("note"):
        flow += [Paragraph("Prüfvermerk der Krankenkasse", H2), Paragraph(c["note"], BODY)]
    flow += [Spacer(1, 10 * mm), Paragraph(f"{doctor}<br/>{practice}", BODY)]
    pdf(p, "Verordnung Krankenbeförderung", practice, flow)
    docs.append(("verordnung", "Verordnung einer Krankenbeförderung", p))

    if c.get("care_grade"):
        p = folder / "pflegegradbescheid.pdf"
        flow = [*address([f"{first} {name}", addr]), Paragraph(f"Bremen, {date}", BODY), Spacer(1, 6 * mm),
                Paragraph("Bescheid über Leistungen der Pflegeversicherung", H1), person_block(c), Spacer(1, 4 * mm),
                Paragraph(f"Sie sind pflegebedürftig im Sinne des § 14 SGB XI. Wir haben Ihnen den "
                          f"<b>Pflegegrad {c['care_grade']}</b> zuerkannt.", BODY)]
        if c.get("transition"):
            flow += [Paragraph("Überleitung", H2), Paragraph(c["transition"] + " (§ 140 SGB XI).", BODY)]
        pdf(p, "Pflegegradbescheid", KASSE[0], flow)
        docs.append(("pflegegradbescheid", "Pflegegradbescheid", p))

    if c.get("markers") is not None:
        p = folder / "schwerbehindertenausweis.png"
        ausweis(p, f"{first} {name}", born, c["markers"])
        docs.append(("schwerbehindertenausweis", "Schwerbehindertenausweis (Scan)", p))
    return docs


def ausweis(path, holder, born, markers):
    w, h = 1000, 630
    img = Image.new("RGB", (w, h), "#e8eef0")
    d = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 30)
        big = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 44)
        small = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 20)
    except OSError:
        font = big = small = ImageFont.load_default()
    d.rounded_rectangle([20, 20, w - 20, h - 20], radius=28, fill="#f6f2e7", outline="#9aa7ad", width=3)
    d.rectangle([20, 60, w - 20, 130], fill="#3f7f5f")
    d.text((50, 72), "Schwerbehindertenausweis", fill="white", font=big)
    d.text((50, 170), f"Name: {holder}", fill="#222", font=font)
    d.text((50, 220), f"Geburtsdatum: {born}", fill="#222", font=font)
    d.text((50, 270), "Grad der Behinderung: 80", fill="#222", font=font)
    d.text((50, 340), "Merkzeichen:", fill="#222", font=font)
    d.text((280, 325), markers or "–", fill="#7a1f1f", font=big)
    d.text((50, 430), "Gültig bis: unbefristet", fill="#222", font=font)
    d.text((50, h - 70), "Fiktiver Musterausweis · NormaCase-Demonstration · keine echte Person", fill="#7a858c", font=small)
    img = img.rotate(-1.2, expand=False, fillcolor="#d9dde0")
    img.save(path, optimize=True)


# ------------------------------------------------------------------ Ausgabe

def main():
    knowledge = Path(__file__).resolve().parent.parent / "knowledge"
    if OUT.exists():
        shutil.rmtree(OUT)
    for c in CASES:
        folder = OUT / c["id"]
        folder.mkdir(parents=True)
        release = json.loads((knowledge / c["pack"] / "pack.json").read_text(encoding="utf-8"))["manifest"]["releaseId"]
        if c["pack"] == PFLEGE:
            facts, evidence, docs = pflege_facts(c["modules"]), {}, pflege_docs(c, folder)
        else:
            facts, evidence, docs = c["facts"], c["evidence"], kt_docs(c, folder)
        case_input = {"formatVersion": 1, "assessmentDate": c["date"], "facts": facts, "evidence": evidence}
        documents = []
        for doc_id, title, path in docs:
            entry = {"id": doc_id, "title": title, "file": path.name,
                     "mediaType": "application/pdf" if path.suffix == ".pdf" else "image/png", "pages": []}
            if path.suffix == ".pdf":
                entry["pages"] = previews(path, folder, doc_id)
            documents.append(entry)
        name, first, _, _ = c["person"]
        meta = {"caseNumber": c["id"].upper(), "displayName": f"{name}, {first} · {c['title']}", "title": c["title"],
                "knowledgeRelease": release, "expectedOutcome": c["expected"], "documents": documents}
        if c.get("clarified_module_4") is not None:
            clarified = dict(case_input, facts=pflege_facts([*c["modules"][:3], c["clarified_module_4"], *c["modules"][4:]]))
            (folder / "clarification.json").write_text(json.dumps(clarified, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
            meta["expectedOutcomeAfterClarification"] = c["expected_after_clarification"]
        (folder / "input.json").write_text(json.dumps(case_input, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        (folder / "case.json").write_text(json.dumps(meta, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{len(CASES)} Vorgänge unter {OUT}")


if __name__ == "__main__":
    main()
