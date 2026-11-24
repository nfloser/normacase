"""Erzeugt die fiktiven Vorgänge unter demo/cases/: Falleingabe, Erwartung und eine realistische Fallakte.

Die Dokumente kommen in den Formaten an, mit denen eine Begutachtung tatsächlich arbeitet: erzeugte PDFs mit
Textebene, gescannte Bild-PDFs, Handyfotos (JPEG), Faxe (mehrseitiges TIFF, 1 Bit), Bildschirmausdrucke (PNG)
und Textexporte aus Praxis- oder Pflegesoftware. Jedes Dokument trägt Dokumentart, Ausstellungsdatum und die
Fundstellen der Angaben, die in die Bewertung eingehen.

Aufruf: python demo/generate.py   (braucht reportlab, Pillow und pdftoppm)
Das Ergebnis ist eingecheckt; der Seeder braucht diese Werkzeuge nicht.
"""
import json
import shutil
import subprocess
import sys
from datetime import date, timedelta
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.platypus import PageBreak, Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle
from reportlab import rl_config

sys.path.insert(0, str(Path(__file__).parent))
from cases import CASES, KT, MODULES, PFLEGE, pflege_facts  # noqa: E402
import scans  # noqa: E402

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
    """Seitenbilder für die Vorschau; Palette mit 48 Farben hält das Repository klein."""
    subprocess.run(["pdftoppm", "-png", "-r", "144", str(pdf_path), str(folder / stem)], check=True)
    pages = sorted(folder.glob(f"{stem}-*.png"))
    renamed = []
    for i, p in enumerate(pages, start=1):
        target = folder / f"{stem}.page-{i}.png"
        with Image.open(p) as img:
            img.convert("RGB").quantize(48).save(target, optimize=True)
        p.unlink()
        renamed.append(target.name)
    return renamed


# ------------------------------------------------------------------ Hilfen für Datum und Ablage

def days_before(iso, days):
    return (date.fromisoformat(iso) - timedelta(days=days)).isoformat()


class Folder:
    """Sammelt die Dokumente eines Vorgangs mit Art, Ausstellungsdatum und Fundstellen für die Bewertung."""

    def __init__(self, path):
        self.path, self.docs = path, []

    def add(self, doc_id, title, file, category, issued, references=()):
        self.docs.append({"id": doc_id, "title": title, "path": file, "category": category, "issuedOn": issued,
                          "references": [{"field": f, "page": p} for f, p in references]})


def scan_pdf(path, pages, seed):
    scans.to_image_pdf([scans.scanned(p, seed + i, angle=0.5 - i * 0.7) for i, p in enumerate(pages)], path)


# ------------------------------------------------------------------ Pflege

def pflege_docs(c, folder):
    name, first, born, addr = c["person"]
    clinic, dept, doctor = c["clinic"]
    seed = int(c["id"][-6:])
    upgrade = "Höherstufung" in c["title"]
    f = Folder(folder)
    received = days_before(c["date"], 24)

    # Antrag der versicherten Person: ausgefülltes Formular, vom Sohn/der Tochter mit dem Handy fotografiert.
    form = scans.form_page("Antrag auf Leistungen der Pflegeversicherung", [
        ("Name, Vorname der versicherten Person", f"{name}, {first}", "line"),
        ("Geburtsdatum", born, "line"), ("Anschrift", addr, "line"),
        ("Erstantrag", not upgrade, "box"), ("Antrag auf Höherstufung", upgrade, "box"),
        ("Pflegegeld", True, "box"), ("Pflegesachleistung (ambulanter Pflegedienst)", c.get("service") is not None, "box"),
        ("Die Pflege wird überwiegend erbracht von", c["carer"], "line"),
        ("Einverständnis zur Weitergabe der Unterlagen an den Medizinischen Dienst", True, "box"),
    ], f"{addr.split()[-1]}, {german_date(days_before(c['date'], 30))}   {first[0]}. {name}")
    scans.photo(form, seed).convert("RGB").save(folder / "antrag.jpg", "JPEG", quality=70, optimize=True)
    f.add("antrag", "Antrag der versicherten Person (Foto)", folder / "antrag.jpg", "ORDER", days_before(c["date"], 30))

    p = folder / "auftrag.pdf"
    pdf(p, "Auftrag zur Begutachtung", KASSE[0], [
        *address(["Medizinischer Dienst", "Begutachtung Pflege", "Postfach 10 77 01 · 28077 Bremen"]),
        Paragraph(f"Bremen, {german_date(received)}", BODY), Spacer(1, 6 * mm),
        Paragraph("Auftrag zur Feststellung der Pflegebedürftigkeit nach § 18 SGB XI", H1),
        person_block(c), Spacer(1, 5 * mm),
        Paragraph(f"Anlass: <b>{c['title'].split('·')[1].strip()}</b>. Bitte prüfen Sie die Voraussetzungen der "
                  "Pflegebedürftigkeit nach §§ 14, 15 SGB XI und teilen Sie uns das Ergebnis mit Begründung mit. "
                  "Die beigefügten Unterlagen wurden von der versicherten Person bzw. ihren Angehörigen eingereicht.", BODY),
        Paragraph("Anlagen", H2),
        Paragraph("<br/>".join(["Antrag der versicherten Person", f"Befundbericht {clinic}", "Arztbrief (Kopie)",
                                "Medikationsplan", "Pflegetagebuch (per Fax)"] + (["Stellungnahme des Pflegedienstes"] if c.get("service") else [])
                               + (["Bisheriger Leistungsbescheid"] if upgrade else [])), BODY),
        Spacer(1, 10 * mm), Paragraph("Mit freundlichen Grüßen<br/>Team Pflegeleistungen", BODY)])
    f.add("auftrag", "Auftrag der Pflegekasse", p, "ORDER", received)

    if upgrade:
        p = folder / "bescheid.pdf"
        pdf(p, "Leistungsbescheid", KASSE[0], [
            *address([f"{first} {name}", addr]), Paragraph(f"Bremen, {german_date(c['previous'][0])}", BODY), Spacer(1, 6 * mm),
            Paragraph("Bescheid über Leistungen der Pflegeversicherung", H1), person_block(c), Spacer(1, 4 * mm),
            Paragraph(f"Auf Grundlage des Gutachtens des Medizinischen Dienstes haben wir Ihnen den "
                      f"<b>Pflegegrad {c['previous'][1]}</b> zuerkannt. Gegen diesen Bescheid kann innerhalb eines Monats "
                      "Widerspruch eingelegt werden.", BODY)])
        f.add("bescheid", f"Bisheriger Bescheid Pflegegrad {c['previous'][1]}", p, "PROOF", c["previous"][0])

    p = folder / "befundbericht.pdf"
    flow = [Paragraph(f"{clinic} · {dept}", HEAD), Spacer(1, 4 * mm),
            Paragraph(f"Ärztlicher Befundbericht zur Vorlage bei der Pflegekasse · {german_date(days_before(c['date'], 18))}", H1),
            person_block(c), Paragraph("Diagnosen", H2),
            Table([["ICD-10", "Diagnose"]] + [list(d) for d in c["diagnoses"]], colWidths=[25 * mm, 145 * mm], style=GRID),
            Paragraph("Befund und Verlauf", H2), Paragraph(c["findings"], BODY),
            PageBreak(),
            Paragraph("Funktionelle Einschränkungen im Alltag", H2), Paragraph(c["function"], BODY),
            Paragraph("Hilfsmittel", H2), Paragraph(", ".join(c["aids"]), BODY),
            Paragraph("Prognose", H2), Paragraph(c["prognosis"], BODY),
            Spacer(1, 10 * mm), Paragraph(f"{doctor}<br/>{dept}", BODY)]
    pdf(p, "Befundbericht", clinic, flow)
    f.add("befundbericht", f"Befundbericht {clinic}", p, "MEDICAL", days_before(c["date"], 18))

    # Älterer Arztbrief, vom Angehörigen kopiert und gescannt: nur Bild, keine Textebene.
    letter = scans.letter_page(f"{clinic} · {dept}", c["letter"][0], [
        f"Sehr geehrte Kollegin, sehr geehrter Kollege, wir berichten über {first} {name}, geb. {born}.",
        ("h", "Diagnosen"), "; ".join(f"{d[1]} ({d[0]})" for d in c["diagnoses"][:3]) + ".",
        ("h", "Verlauf"), c["letter"][1],
        ("h", "Empfehlung"), c["letter"][2], f"Mit kollegialen Grüßen · {doctor}"], 1, 1)
    scan_pdf(folder / "arztbrief.pdf", [letter], seed)
    f.add("arztbrief", f"{c['letter'][0]} (Scan)", folder / "arztbrief.pdf", "MEDICAL", c["letter"][3])

    plan = scans.medication_plan(f"{first} {name}", born, c["medication"], doctor, german_date(days_before(c["date"], 12)))
    plan.quantize(32).save(folder / "medikationsplan.png", optimize=True)
    f.add("medikationsplan", "Medikationsplan", folder / "medikationsplan.png", "MEDICAL", days_before(c["date"], 12))

    # Pflegetagebuch: handschriftlich geführt, von der Hausarztpraxis per Fax weitergeleitet (TIFF, 2 Seiten).
    diary = c["diary"]
    half = (len(diary) + 1) // 2
    pages = [scans.letter_page(f"Pflegetagebuch · {first} {name}", "Pflegetagebuch (14 Tage)" if i == 0 else None,
                               [("h", "Typischer Tagesablauf" if i == 0 else "Fortsetzung"), ("rows", chunk),
                                *(["Geführt von: " + c["carer"]] if i == 1 else [])], i + 1, 2, serif=False, accent=None)
             for i, chunk in enumerate([diary[:half], diary[half:] or [("–", "keine weiteren Einträge")]])]
    sent = f"{german_date(days_before(c['date'], 8))} 16:4{seed % 10}"
    faxed = [scans.fax(pg, seed, sent, "Hausarztpraxis", i + 1, 2) for i, pg in enumerate(pages)]
    faxed[0].save(folder / "pflegetagebuch.tiff", "TIFF", compression="group4", save_all=True, append_images=faxed[1:], dpi=(100, 100))
    f.add("pflegetagebuch", "Pflegetagebuch der Angehörigen (Fax)", folder / "pflegetagebuch.tiff", "CARE", days_before(c["date"], 8))

    if c.get("service"):
        service, text = c["service"]
        issued = days_before(c["date"], 6)
        (folder / "pflegedienst.txt").write_text(
            f"{service}\nStellungnahme zur Pflegesituation\nausgegeben am {german_date(issued)} aus der Pflegedokumentation\n"
            f"\nVersicherte Person: {first} {name}, geb. {born}\n\n{text}\n\n"
            "-- \nFiktives Dokument · alle Personen und Angaben frei erfunden · NormaCase-Demonstration\n", encoding="utf-8")
        f.add("pflegedienst", f"Stellungnahme {service} (Textexport)", folder / "pflegedienst.txt", "CARE", issued)

    p = folder / "erhebung.pdf"
    rows = [["Modul", "Bereich", "Summe der Einzelpunkte"]]
    for i, (label, value) in enumerate(zip(MODULES, c["modules"]), start=1):
        rows.append([str(i), Paragraph(label, BODY), "nicht erhoben" if value is None else str(value)])
    flow = [Paragraph("Pflegebegutachtung · Erhebung nach dem Begutachtungsinstrument", H1),
            Paragraph(f"Hausbesuch am {german_date(c['date'])} · Gutachterin: Pflegefachkraft Anja Meyer", SMALL), Spacer(1, 4 * mm),
            person_block(c), Paragraph("Ergebnis der Erhebung je Modul", H2),
            Table(rows, colWidths=[18 * mm, 107 * mm, 45 * mm], style=GRID),
            Paragraph("Die Umrechnung in gewichtete Punkte und die Zuordnung zu den Schwellenwerten erfolgt "
                      "regelbasiert und nachvollziehbar in NormaCase.", SMALL)]
    if None in c["modules"]:
        flow.append(Paragraph("Hinweis: Modul 4 konnte beim Hausbesuch nicht vollständig erhoben werden "
                              "(Off-Phase). Nachbesuch vereinbart.", BODY))
    pdf(p, "Erhebungsbogen", "Pflegebegutachtung", flow)
    f.add("erhebung", "Erhebungsbogen Module 1–6", p, "CARE", c["date"],
          [(f"module_{i}_sum", 1) for i in range(1, 7)])

    if c.get("clarified_module_4") is not None:
        p = folder / "nachtrag.pdf"
        pdf(p, "Nachtrag", "Pflegebegutachtung", [
            Paragraph("Nachtrag zum Erhebungsbogen", H1),
            Paragraph("Nachbesuch am 13.11.2026 in einer On-Phase · Gutachterin: Pflegefachkraft Anja Meyer", SMALL),
            Spacer(1, 4 * mm), person_block(c), Paragraph("Modul 4: Selbstversorgung", H2),
            Paragraph(f"Summe der Einzelpunkte: <b>{c['clarified_module_4']}</b>. Hilfe beim Waschen des Unterkörpers, beim "
                      "An- und Auskleiden und beim Duschen; Essen und Trinken selbstständig mit Hilfsmitteln.", BODY)])
        f.add("nachtrag", "Nachtrag Modul 4", p, "CARE", "2026-11-13", [("module_4_sum", 1)])
    return f.docs


# ------------------------------------------------------------------ Krankenbeförderung

MARKERS = (("aG", "disability_marker_ag"), ("Bl", "disability_marker_bl"), ("H", "disability_marker_h"))


def kt_docs(c, folder):
    name, first, born, addr = c["person"]
    practice, doctor = c["practice"]
    seed = int(c["id"][-6:])
    facts = c["facts"]
    f = Folder(folder)
    prescribed = days_before(c["date"], 9)

    def box(v):
        return "[X]" if v.get("truth") == "YES" else "[ ]"

    p = folder / "verordnung.pdf"
    rows = [["Angabe", "Eintrag"],
            ["Behandlung", Paragraph(c["treatment"], BODY)],
            ["Ambulante Behandlung", box(facts["ambulatory_treatment"])],
            ["Zwingende medizinische Notwendigkeit", box(facts["strict_medical_necessity"]) if facts["strict_medical_necessity"].get("kind") == "TRUTH" else "[ ]  (nicht angekreuzt)"],
            ["Merkzeichen laut Angabe", ", ".join(m for m, k in MARKERS if facts[k].get("truth") == "YES") or "keine"],
            ["Pflegegrad laut Angabe", str(facts["care_grade"]["number"]) if facts["care_grade"].get("kind") == "NUMBER" else "nicht angegeben"],
            ["Dauerhafte Mobilitätsbeeinträchtigung (Pflegegrad 3)", box(facts["care_grade_3_permanent_mobility_transport_need"])],
            ["Beförderungsmittel", "Taxi / Mietwagen"]]
    flow = [Paragraph(f"{practice}", HEAD), Spacer(1, 4 * mm),
            Paragraph(f"Verordnung einer Krankenbeförderung · {german_date(prescribed)}", H1), person_block(c), Spacer(1, 4 * mm),
            Table(rows, colWidths=[70 * mm, 100 * mm], style=GRID)]
    if c.get("note"):
        flow += [Paragraph("Prüfvermerk der Krankenkasse", H2), Paragraph(c["note"], BODY)]
    flow += [Spacer(1, 10 * mm), Paragraph(f"{doctor}<br/>{practice}", BODY)]
    pdf(p, "Verordnung Krankenbeförderung", practice, flow)
    f.add("verordnung", "Verordnung einer Krankenbeförderung", p, "ORDER", prescribed,
          [(k, 1) for k in ("ambulatory_treatment", "strict_medical_necessity", "disability_marker_ag", "disability_marker_bl",
                            "disability_marker_h", "care_grade", "care_grade_3_permanent_mobility_transport_need")])

    p = folder / "auftrag.pdf"
    ordered = days_before(c["date"], 3)
    pdf(p, "Prüfauftrag", KK[0], [
        *address(["Medizinischer Dienst", "Sozialmedizinische Fallberatung", "Postfach 10 77 01 · 28077 Bremen"]),
        Paragraph(f"Bremen, {german_date(ordered)}", BODY), Spacer(1, 6 * mm),
        Paragraph("Prüfauftrag Krankenbeförderung (§ 60 SGB V, Krankentransport-Richtlinie)", H1),
        person_block(c), Spacer(1, 5 * mm),
        Paragraph("Die versicherte Person beantragt die Übernahme von Fahrkosten zu einer ambulanten Behandlung. Bitte "
                  "nehmen Sie Stellung, ob die Voraussetzungen für eine Genehmigung vorliegen.", BODY),
        Paragraph("Anlagen", H2),
        Paragraph("<br/>".join(["Verordnung einer Krankenbeförderung", "Antrag der versicherten Person", "Arztbrief (per Fax)",
                                "Terminübersicht der Praxis"] + (["Pflegegradbescheid"] if c.get("care_grade") else [])
                               + (["Schwerbehindertenausweis (Kopie)"] if c.get("markers") is not None else [])), BODY),
        Spacer(1, 10 * mm), Paragraph("Mit freundlichen Grüßen<br/>Team Fahrkosten", BODY)])
    f.add("auftrag", "Prüfauftrag der Krankenkasse", p, "ORDER", ordered)

    # Antrag auf Kostenübernahme, von der versicherten Person ausgefüllt und fotografiert.
    form = scans.form_page("Antrag auf Übernahme der Fahrkosten", [
        ("Name, Vorname", f"{name}, {first}", "line"), ("Geburtsdatum", born, "line"), ("Anschrift", addr, "line"),
        ("Behandelnde Praxis", practice, "line"), ("Fahrten zur ambulanten Behandlung", True, "box"),
        ("Verordnung liegt bei", True, "box"), ("Kopie Schwerbehindertenausweis liegt bei", c.get("markers") is not None, "box"),
        ("Kopie Pflegegradbescheid liegt bei", bool(c.get("care_grade")), "box"),
    ], f"{addr.split()[-1]}, {german_date(days_before(c['date'], 7))}   {first[0]}. {name}")
    scans.photo(form, seed).save(folder / "antrag.jpg", "JPEG", quality=70, optimize=True)
    f.add("antrag", "Antrag auf Fahrkostenübernahme (Foto)", folder / "antrag.jpg", "ORDER", days_before(c["date"], 7))

    if c.get("care_grade"):
        p = folder / "pflegegradbescheid.pdf"
        issued = c.get("care_grade_since", "2024-05-14")
        flow = [*address([f"{first} {name}", addr]), Paragraph(f"Bremen, {german_date(issued)}", BODY), Spacer(1, 6 * mm),
                Paragraph("Bescheid über Leistungen der Pflegeversicherung", H1), person_block(c), Spacer(1, 4 * mm),
                Paragraph(f"Sie sind pflegebedürftig im Sinne des § 14 SGB XI. Wir haben Ihnen den "
                          f"<b>Pflegegrad {c['care_grade']}</b> zuerkannt.", BODY)]
        refs = [("care_grade", 1)]
        if c.get("transition"):
            flow += [Paragraph("Überleitung", H2), Paragraph(c["transition"] + " (§ 140 SGB XI).", BODY)]
            refs += [("care_level_2_on_2016_12_31", 1), ("care_grade_3_or_higher_since_2017_01_01", 1)]
        pdf(p, "Pflegegradbescheid", KASSE[0], flow)
        f.add("pflegegradbescheid", "Pflegegradbescheid", p, "PROOF", issued, refs)

    if c.get("markers") is not None:
        p = folder / "schwerbehindertenausweis.png"
        ausweis(p, f"{first} {name}", born, c["markers"])
        f.add("schwerbehindertenausweis", "Schwerbehindertenausweis (Scan)", p, "PROOF", "2023-02-01",
              [(k, 1) for _, k in MARKERS])

    # Arztbrief zur Grunderkrankung, von der Praxis per Fax nachgereicht.
    letter = scans.letter_page(practice, "Arztbrief", [
        f"Betreff: {first} {name}, geb. {born}", ("h", "Anlass"), c["treatment"] + ".",
        ("h", "Befund"), c["letter"], f"Mit freundlichen Grüßen · {doctor}"], 1, 1, serif=False, accent=None)
    scans.fax(letter, seed, f"{german_date(days_before(c['date'], 5))} 11:2{seed % 10}", practice[:28]).save(folder / "arztbrief.tiff", "TIFF", compression="group4", dpi=(100, 100))
    f.add("arztbrief", "Arztbrief der verordnenden Praxis (Fax)", folder / "arztbrief.tiff", "MEDICAL", days_before(c["date"], 5))

    issued = days_before(c["date"], 4)
    (folder / "termine.txt").write_text(
        f"{practice}\nTerminübersicht (Export aus dem Praxisverwaltungssystem) · {german_date(issued)}\n\n"
        f"Patient/in: {first} {name}, geb. {born}\nBehandlung: {c['treatment']}\n\n"
        + "\n".join(f"  {german_date(d)}  {t} Uhr" for d, t in c["appointments"])
        + "\n\nHin- und Rückfahrt je Termin.\n\n-- \nFiktives Dokument · alle Personen und Angaben frei erfunden · NormaCase-Demonstration\n",
        encoding="utf-8")
    f.add("termine", "Terminübersicht der Praxis (Textexport)", folder / "termine.txt", "OTHER", issued)
    return f.docs


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

MEDIA = {".pdf": "application/pdf", ".png": "image/png", ".jpg": "image/jpeg", ".tiff": "image/tiff", ".txt": "text/plain"}


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
        for d in sorted(docs, key=lambda d: d["issuedOn"]):
            path = d.pop("path")
            entry = {"id": d["id"], "title": d["title"], "file": path.name, "mediaType": MEDIA[path.suffix],
                     "category": d["category"], "issuedOn": d["issuedOn"], "references": d["references"], "pages": []}
            if path.suffix == ".pdf":
                entry["pages"] = previews(path, folder, d["id"])
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
