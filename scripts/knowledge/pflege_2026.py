"""Erzeugt die Knowledge Packs der Pflegebegutachtung nach den BRi vom 26.08.2026 (gültig ab 01.10.2026).

Die Packs selbst sind deklarative Daten (knowledge/AGENTS.md); dieses Skript hält nur die Tabellen der Richtlinie an
einer Stelle, damit Erwachsene, Kinder und Säuglinge dieselben Kriterien und Bewertungsstufen verwenden. Jede Zahl ist
mit dem Abschnitt der Richtlinie kommentiert. Ausgabe ist byte-genau reproduzierbar.

    python3 scripts/knowledge/pflege_2026.py
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2] / "knowledge" / "public-reference"
VALID_FROM = "2026-10-01"
SOURCE_ID = "MD-BUND-PFLEGE-BRI-2026-08-26"
SOURCE = {  # identisch mit pflege-adult-score: dieselbe gepinnte Richtlinie
    "id": SOURCE_ID, "authority": "Medizinischer Dienst Bund (KöR)",
    "title": "Richtlinien des Medizinischen Dienstes Bund zur Feststellung der Pflegebedürftigkeit nach dem XI. Buch des Sozialgesetzbuches",
    "documentType": "RICHTLINIE", "publicationDate": None, "validFrom": VALID_FROM, "validUntil": None, "version": "2026-08-26",
    "sourceLocation": "https://md-bund.de/fileadmin/dokumente/Publikationen/SPV/Begutachtungsgrundlagen/BRi_Pflege_26_08_2026.pdf",
    "retrievedAt": "2026-10-02",
    "contentHash": "sha256:f39b25b55a30cd2dcf9b5ac1547f73be3f9d077b12c87f69ded38e6fac104e4b", "status": "ACTIVE",
}

# ---------------------------------------------------------------- Kriterien (BRi 5.9, F 4.1–F 4.6; Kinder 6.5)
M1 = [("1_1", "Positionswechsel im Bett"), ("1_2", "Halten einer stabilen Sitzposition"), ("1_3", "Umsetzen"),
      ("1_4", "Fortbewegen innerhalb des Wohnbereichs"), ("1_5", "Treppensteigen")]
M2 = [("2_1", "Erkennen von Personen aus dem näheren Umfeld"), ("2_2", "Örtliche Orientierung"), ("2_3", "Zeitliche Orientierung"),
      ("2_4", "Erinnern an wesentliche Ereignisse oder Beobachtungen"), ("2_5", "Steuern von mehrschrittigen Alltagshandlungen"),
      ("2_6", "Treffen von Entscheidungen im Alltag"), ("2_7", "Verstehen von Sachverhalten und Informationen"),
      ("2_8", "Erkennen von Risiken und Gefahren"), ("2_9", "Mitteilen von elementaren Bedürfnissen"),
      ("2_10", "Verstehen von Aufforderungen"), ("2_11", "Beteiligen an einem Gespräch")]
M3 = [("3_1", "Motorisch geprägte Verhaltensauffälligkeiten"), ("3_2", "Nächtliche Unruhe"),
      ("3_3", "Selbstschädigendes und autoaggressives Verhalten"), ("3_4", "Beschädigen von Gegenständen"),
      ("3_5", "Physisch aggressives Verhalten gegenüber anderen Personen"), ("3_6", "Verbale Aggression"),
      ("3_7", "Andere pflegerelevante vokale Auffälligkeiten"), ("3_8", "Abwehr pflegerischer und anderer unterstützender Maßnahmen"),
      ("3_9", "Wahnvorstellungen"), ("3_10", "Ängste"), ("3_11", "Antriebslosigkeit bei depressiver Stimmungslage"),
      ("3_12", "Sozial inadäquate Verhaltensweisen"), ("3_13", "Sonstige pflegerelevante inadäquate Handlungen")]
# Modul 4 mit Einzelpunkten je Stufe 0–3 (BRi 5.10.1): Essen dreifach, Trinken und Toilette doppelt gewichtet.
M4 = [("4_1", "Waschen des vorderen Oberkörpers", 1), ("4_2", "Körperpflege im Bereich des Kopfes", 1),
      ("4_3", "Waschen des Intimbereichs", 1), ("4_4", "Duschen und Baden einschließlich Waschen der Haare", 1),
      ("4_5", "An- und Auskleiden des Oberkörpers", 1), ("4_6", "An- und Auskleiden des Unterkörpers", 1),
      ("4_7", "Mundgerechtes Zubereiten der Nahrung und Eingießen von Getränken", 1), ("4_8", "Essen", 3), ("4_9", "Trinken", 2),
      ("4_10", "Benutzen einer Toilette oder eines Toilettenstuhls", 2),
      ("4_11", "Bewältigen der Folgen einer Harninkontinenz und Umgang mit Dauerkatheter und Urostoma", 1),
      ("4_12", "Bewältigen der Folgen einer Stuhlinkontinenz und Umgang mit Stoma", 1)]
# 4.13: 0 = keine, nicht täglich oder nicht auf Dauer (oder selbständig) → 0; 1 = täglich zusätzlich zu oraler Nahrung → 6;
# 2 = ausschließlich oder nahezu ausschließlich → 3 (BRi 5.10.1, Tabelle zu F 4.4.13).
M4_13 = ("4_13", "Ernährung parenteral oder über Sonde")
M5_A = [("5_1", "Medikation"), ("5_2", "Injektionen"), ("5_3", "Versorgung intravenöser Zugänge"),
        ("5_4", "Absaugen und Sauerstoffgabe"), ("5_5", "Einreibungen sowie Kälte- und Wärmeanwendungen"),
        ("5_6", "Messung und Deutung von Körperzuständen"), ("5_7", "Körpernahe Hilfsmittel")]
M5_B = [("5_8", "Verbandswechsel und Wundversorgung"), ("5_9", "Versorgung mit Stoma"),
        ("5_10", "Regelmäßige Einmalkatheterisierung und Nutzung von Abführmethoden"),
        ("5_11", "Therapiemaßnahmen in häuslicher Umgebung")]
# 5.12–5.15 (und 5.K bei Kindern): Punkte je Häufigkeit (BRi 5.10.1): täglich, pro Woche, pro Monat.
M5_C = [("5_12", "Zeit- und technikintensive Maßnahmen in häuslicher Umgebung", "60", "8.6", "2"),
        ("5_13", "Arztbesuche", None, "4.3", "1"),
        ("5_14", "Besuch anderer medizinischer oder therapeutischer Einrichtungen (bis zu drei Stunden)", None, "4.3", "1"),
        ("5_15", "Zeitlich ausgedehnte Besuche medizinischer oder therapeutischer Einrichtungen (länger als drei Stunden)", None, "8.6", "2")]
M5_K = ("5_k", "Besuche von Einrichtungen zur Frühförderung bei Kindern", None, "4.3", "1")
M5_16 = ("5_16", "Einhalten einer Diät und anderer krankheits- oder therapiebedingter Verhaltensvorschriften")
M6 = [("6_1", "Gestaltung des Tagesablaufs und Anpassung an Veränderungen"), ("6_2", "Ruhen und Schlafen"),
      ("6_3", "Sichbeschäftigen"), ("6_4", "Vornehmen von in die Zukunft gerichteten Planungen"),
      ("6_5", "Interaktion mit Personen im direkten Kontakt"), ("6_6", "Kontaktpflege zu Personen außerhalb des direkten Umfelds")]

# Summe der Einzelpunkte je Modul → gewichtete Punkte (BRi 5.10.1, § 15 Abs. 3 SGB XI mit Anlage 2)
WEIGHTS = {
    1: [(0, 1, 0), (2, 3, 2.5), (4, 5, 5), (6, 9, 7.5), (10, 15, 10)],
    2: [(0, 1, 0), (2, 5, 3.75), (6, 10, 7.5), (11, 16, 11.25), (17, 33, 15)],
    3: [(0, 0, 0), (1, 2, 3.75), (3, 4, 7.5), (5, 6, 11.25), (7, 65, 15)],
    4: [(0, 2, 0), (3, 7, 10), (8, 18, 20), (19, 36, 30), (37, 54, 40)],
    5: [(0, 0, 0), (1, 1, 5), (2, 3, 10), (4, 5, 15), (6, 15, 20)],
    6: [(0, 0, 0), (1, 3, 3.75), (4, 6, 7.5), (7, 11, 11.25), (12, 18, 15)],
}
# Altersgrenzen in vollendeten Monaten: Übergänge zu Stufe 2, 1 und 0 (BRi 6.5, Tabellen zu Modulen 1, 2, 4 und 6).
# Frühere Grenzen unter 18 Monaten werden im Kinder-Pack nicht erreicht. Bei 4.11/4.12 gibt es erst ab 60 Monaten eine Wertung.
CHILD_AGE = {
    "1_1": (1, 3, 9), "1_2": (6, 8, 9), "1_3": (8, 9, 11), "1_4": (12, 13, 18), "1_5": (15, 18, 30),
    "2_1": (1, 9, 15), "2_2": (13, 18, 72), "2_3": (30, 60, 84), "2_4": (9, 36, 66),
    "2_5": (5, 12, 15), "2_6": (18, 30, 54), "2_7": (48, 60, 72), "2_8": (30, 78, 120),
    "2_9": (3, 13, 48), "2_10": (16, 18, 30), "2_11": (15, 24, 48),
    "4_1": (24, 48, 72), "4_2": (18, 42, 60), "4_3": (24, 48, 72), "4_4": (42, 48, 96),
    "4_5": (18, 42, 72), "4_6": (18, 42, 72), "4_7": (24, 66, 96), "4_8": (7, 20, 30),
    "4_9": (8, 11, 24), "4_10": (18, 42, 72), "4_11": (60, 60, 60), "4_12": (60, 60, 60),
    "6_1": (30, 60, 84), "6_2": (6, 60, 132), "6_3": (6, 36, 60),
    "6_4": (30, 36, 60), "6_5": (1, 9, 12), "6_6": (12, 36, 60),
}
MAX = 1000000  # obere Grenze für Häufigkeiten; größere Werte gelten als unplausibel und führen zu HUMAN_REVIEW


def num(v):
    """Zahlen als JSON-Zahl ohne unnötige Nachkommastellen."""
    f = float(v)
    return int(f) if f == int(f) else f


def field(fid):
    return {"kind": "field", "field": fid}


def lookup(inp, bands, eid=None):
    e = {"kind": "range_lookup", "input": inp, "bands": [{"minimum": num(a), "maximum": num(b), "value": num(v)} for a, b, v in bands]}
    if eid:
        e["id"] = eid
    return e


def total(operands, eid=None):
    e = {"kind": "sum", "operands": operands}
    if eid:
        e["id"] = eid
    return e


def scale(inp, factor=None, divisor=None, decimals=None):
    e = {"kind": "scale", "input": inp}
    if factor is not None:
        e["factor"] = num(factor)
    if divisor is not None:
        e["divisor"] = num(divisor)
    if decimals is not None:
        e["decimals"] = decimals
    return e


def level(fid, points=(0, 1, 2, 3)):
    """Stufe 0–3 eines Kriteriums → Einzelpunkte. Andere Werte treffen kein Band und bleiben UNKNOWN (fail-closed)."""
    return lookup(field(fid), [(i, i, p) for i, p in enumerate(points)])


def child_level(c, multiplier=1):
    """Einzelpunkte aus beobachteter Stufe und altersüblicher Stufe, analog zur Matrix in BRi 6.5."""
    first, second, third = CHILD_AGE[c]
    limits = (0, first, second, third, 216)
    age_bands = [(max(18, a), b - 1, -3 + i)
                 for i, (a, b) in enumerate(zip(limits, limits[1:])) if max(18, a) < b]
    baseline = lookup(field("alter_monate"), age_bands)
    comparison = total([level(f"kf_{c}"), baseline])
    return lookup(comparison, [(i, i, max(0, i) * multiplier) for i in range(-3, 4)], f"kf_{c}_vergleich")


def freq_per_210_days(c):
    """Exakte Summe im gemeinsamen Bezugszeitraum: 210 Tage = 30 Wochen = 7 Rechenmonate."""
    return total([scale(field(f"kf_{c}_tag"), factor=210), scale(field(f"kf_{c}_woche"), factor=30),
                  scale(field(f"kf_{c}_monat"), factor=7)], f"kf_{c}")


def module5(children):
    group_a = lookup(total([freq_per_210_days(c) for c, _ in M5_A], "modul_5_a_haeufigkeit"),
                     [(0, 209, 0), (210, 630, 1), (631, 1680, 2), (1681, MAX, 3)], "modul_5_a")
    group_b = lookup(total([freq_per_210_days(c) for c, _ in M5_B], "modul_5_b_haeufigkeit"),
                     [(0, 29, 0), (30, 209, 1), (210, 629, 2), (630, MAX, 3)], "modul_5_b")
    parts = []
    for c, _, daily, weekly, monthly in M5_C + ([M5_K] if children else []):
        terms = []
        if daily:
            terms.append(lookup(field(f"kf_{c}_tag"), [(0, 0, 0), (1, MAX, daily)]))
        terms.append(scale(field(f"kf_{c}_woche"), factor=weekly))
        terms.append(scale(field(f"kf_{c}_monat"), factor=monthly))
        parts.append(total(terms, f"kf_{c}"))
    group_c = lookup(total(parts, "modul_5_c_punkte"),
                     [(0, 4.2999, 0), (4.3, 8.5999, 1), (8.6, 12.8999, 2), (12.9, 59.9999, 3), (60, MAX, 6)], "modul_5_c")
    return lookup(total([group_a, group_b, group_c, level(f"kf_{M5_16[0]}")], "modul_5_einzelpunkte"), WEIGHTS[5], "modul_5")


def frequency(fid):
    """Modul 3: Einzelpunkte 0 (nie/sehr selten), 1 (selten), 3 (häufig), 5 (täglich) – BRi F 4.3; andere Werte → UNKNOWN."""
    return lookup(field(fid), [(p, p, p) for p in (0, 1, 3, 5)])


def module(n, criteria, children=False):
    if n == 3:
        items = [frequency(f"kf_{c}") for c, _ in criteria]
    elif children:
        items = [child_level(c) for c, _ in criteria]
    else:
        items = [level(f"kf_{c}") for c, _ in criteria]
    return lookup(total(items, f"modul_{n}_einzelpunkte"), WEIGHTS[n], f"modul_{n}")


def module4(children):
    parts = [child_level(c, w) if children else level(f"kf_{c}", tuple(i * w for i in range(4))) for c, _, w in M4]
    parts.append(lookup(field(f"kf_{M4_13[0]}"), [(0, 0, 0), (1, 1, 6), (2, 2, 3)]))
    return lookup(total(parts, "modul_4_einzelpunkte"), WEIGHTS[4], "modul_4")


def score_regular(children):
    return total([module(1, M1, children),
                  {"kind": "max", "id": "modul_2_3", "operands": [module(2, M2, children), module(3, M3)]},
                  module4(children), module5(children), module(6, M6, children)], "gesamtpunkte")


def score_infant():
    """Bis 18 Monate: nur Modul 3, Modul 5 und KF 4.4.0 (BRi 6.6); Module 1, 2, 4 (übrige) und 6 entfallen."""
    m3 = module(3, M3)
    m4 = lookup(lookup(field("kf_4_0"), [(0, 0, 0), (1, 1, 20)], "modul_4_einzelpunkte"), WEIGHTS[4], "modul_4")
    return total([m3, m4, module5(True)], "gesamtpunkte")


def gte(expr, threshold):
    return {"kind": "number_gte", "threshold": num(threshold), "numericExpression": expr}


def special():
    return {"kind": "field_equals", "field": "kf_1_b", "expected": "YES"}


def choice_output(oid, condition):
    return {"id": oid, "version": 1, "validFrom": VALID_FROM, "validUntil": None, "sourceId": SOURCE_ID, "type": "choice",
            "choices": ["REACHED", "NOT_REACHED"], "condition": condition, "onMatch": "REACHED", "onNoMatch": "NOT_REACHED"}


def grade_output(score, bands):
    """Pflegegrad nach Gesamtpunkten als Zahl 0–5 (0 = kein Pflegegrad); andere Werte sind nicht zulässig."""
    return {"id": "pflegegrad_nach_punkten", "version": 1, "validFrom": VALID_FROM, "validUntil": None, "sourceId": SOURCE_ID,
            "type": "number", "choices": ["0", "1", "2", "3", "4", "5"], "numericExpression": lookup(score, bands, "pflegegrad")}


def fields(kind):
    out = [{"id": "kf_1_b", "type": "truth", "required": True}]
    def nums(ids):
        out.extend({"id": i, "type": "number", "required": True} for i in ids)
    if kind == "kind":
        nums(["alter_monate"])
    if kind != "saeugling":
        nums(f"kf_{c}" for c, _ in M1)
        nums(f"kf_{c}" for c, _ in M2)
    nums(f"kf_{c}" for c, _ in M3)
    if kind == "saeugling":
        nums(["kf_4_0"])
    else:
        nums(f"kf_{c}" for c, _, _ in M4)
        nums([f"kf_{M4_13[0]}"])
    for c, _ in M5_A + M5_B:
        nums([f"kf_{c}_tag", f"kf_{c}_woche", f"kf_{c}_monat"])
    for c, _, daily, _, _ in M5_C + ([M5_K] if kind != "erwachsen" else []):
        nums(([f"kf_{c}_tag"] if daily else []) + [f"kf_{c}_woche", f"kf_{c}_monat"])
    nums([f"kf_{M5_16[0]}"])
    if kind != "saeugling":
        nums(f"kf_{c}" for c, _ in M6)
    return out


SCALE_LEVEL = "0 = selbständig, 1 = überwiegend selbständig, 2 = überwiegend unselbständig, 3 = unselbständig"
SCALE_M2 = "0 = vorhanden/unbeeinträchtigt, 1 = größtenteils vorhanden, 2 = in geringem Maße vorhanden, 3 = nicht vorhanden"
SCALE_M3 = "0 = nie oder sehr selten, 1 = selten, 3 = häufig, 5 = täglich (Einzelpunkte)"
CHILD_NOTE = " Beobachtete Stufe angeben. Die Altersmatrix berechnet die Einzelpunkte. Falls altersbedingt kein Kriterium erhoben wird, 0 angeben."


def presentation(kind, pack_id, name, description):
    f = {"kf_1_b": {"label": "F 4.1.B Besondere Bedarfskonstellation: Gebrauchsunfähigkeit beider Arme und beider Beine",
                    "hint": "Vollständiger Verlust der Greif-, Steh- und Gehfunktionen, nicht durch Hilfsmittel kompensierbar (BRi F 4.1.B)."}}
    child = CHILD_NOTE if kind == "kind" else ""
    def put(fid, label, hint):
        f[fid] = {"label": label, "hint": hint}
    if kind == "kind":
        put("alter_monate", "Alter am Begutachtungstag in vollendeten Monaten",
            "Ganze Monate, 18 bis 215. Dieses Pack gilt nach dem letzten Tag der Säuglingsregel bis zum 18. Geburtstag (BRi Kapitel 6).")
    if kind != "saeugling":
        for c, t in M1:
            put(f"kf_{c}", f"{c.replace('_', '.')} {t}", SCALE_LEVEL + child)
        for c, t in M2:
            put(f"kf_{c}", f"{c.replace('_', '.')} {t}", SCALE_M2 + child)
    for c, t in M3:
        put(f"kf_{c}", f"{c.replace('_', '.')} {t}", SCALE_M3)
    if kind == "saeugling":
        put("kf_4_0", "KF 4.4.0 Gravierende Probleme bei der Nahrungsaufnahme",
            "0 = nein, 1 = ja (außergewöhnlich pflegeintensiver Hilfebedarf; ergibt 20 Einzelpunkte, BRi 6.6)")
    else:
        for c, t, w in M4:
            extra = {3: " Dreifach gewichtet.", 2: " Doppelt gewichtet."}.get(w, "")
            if c in ("4_11", "4_12"):
                extra = " Nur bei überwiegender oder kompletter Inkontinenz bzw. künstlicher Ableitung zu bewerten, sonst 0."
            put(f"kf_{c}", f"{c.replace('_', '.')} {t}", SCALE_LEVEL + extra + child)
        put(f"kf_{M4_13[0]}", f"4.13 {M4_13[1]}",
            "0 = keine, nicht täglich, nicht auf Dauer oder selbständig; 1 = täglich zusätzlich zu oraler Nahrung; 2 = ausschließlich oder nahezu ausschließlich")
    for c, t in M5_A + M5_B:
        for unit, word in (("tag", "pro Tag"), ("woche", "pro Woche"), ("monat", "pro Monat")):
            put(f"kf_{c}_{unit}", f"{c.replace('_', '.')} {t} – {word}", "Anzahl; nur Maßnahmen, die voraussichtlich mindestens sechs Monate nötig sind")
    for c, t, daily, _, _ in M5_C + ([M5_K] if kind != "erwachsen" else []):
        units = ([("tag", "täglich (1 = ja)")] if daily else []) + [("woche", "pro Woche"), ("monat", "pro Monat")]
        for unit, word in units:
            put(f"kf_{c}_{unit}", f"{c.replace('_', '.').upper() if c == '5_k' else c.replace('_', '.')} {t} – {word}", "Anzahl")
    put(f"kf_{M5_16[0]}", f"5.16 {M5_16[1]}", SCALE_LEVEL)
    if kind != "saeugling":
        for c, t in M6:
            put(f"kf_{c}", f"{c.replace('_', '.')} {t}", SCALE_LEVEL + child)
    numerics = {"gesamtpunkte": "Gesamtpunkte", "modul_1": "Modul 1: Mobilität", "modul_2": "Modul 2: Kognitive und kommunikative Fähigkeiten",
                "modul_3": "Modul 3: Verhaltensweisen und psychische Problemlagen", "modul_2_3": "Modul 2 oder 3 (höherer Wert zählt)",
                "modul_4": "Modul 4: Selbstversorgung", "modul_5": "Modul 5: Krankheits- oder therapiebedingte Anforderungen",
                "modul_6": "Modul 6: Gestaltung des Alltagslebens und sozialer Kontakte",
                "modul_5_a": "5.1–5.7 (Häufigkeit pro Tag → Punkte)", "modul_5_a_haeufigkeit": "5.1–5.7 Anzahl pro 210 Rechentage",
                "modul_5_b": "5.8–5.11 (Häufigkeit pro Tag → Punkte)", "modul_5_b_haeufigkeit": "5.8–5.11 Anzahl pro 210 Rechentage",
                "modul_5_c": "5.12–5.15 (Zeitaufwand → Punkte)", "modul_5_c_punkte": "5.12–5.15 Summe",
                "modul_4_einzelpunkte": "Einzelpunkte", "modul_5_einzelpunkte": "Einzelpunkte"}
    for n in (1, 2, 3, 6):
        numerics[f"modul_{n}_einzelpunkte"] = "Einzelpunkte"
    for c, t in M5_A + M5_B:
        numerics[f"kf_{c}"] = f"{c.replace('_', '.')} {t}"
    for c, t, *_ in M5_C + [M5_K]:
        numerics[f"kf_{c}"] = f"{c.replace('_', '.')} {t}"
    grade_names = {"0": "kein Pflegegrad", **{str(i): f"Pflegegrad {i}" for i in range(1, 6)}}
    outputs = {"pflegegrad_nach_punkten": {"label": "Pflegegrad nach Gesamtpunkten", "choices": grade_names},
               "pflegegrad_5_besondere_bedarfskonstellation": {
                   "label": "Pflegegrad 5 wegen besonderer Bedarfskonstellation (F 4.1.B)",
                   "choices": {"REACHED": "Zutreffend", "NOT_REACHED": "Nicht zutreffend"}}}
    if kind == "kind":
        for c, label in [(c, t) for c, t in M1 + M2 + M6] + [(c, t) for c, t, _ in M4]:
            numerics[f"kf_{c}_vergleich"] = f"{label}: Punkte nach Altersvergleich"
    numerics["pflegegrad"] = "Pflegegrad nach Gesamtpunkten"
    return {"formatVersion": 1, "locale": "de-DE", "packId": pack_id, "name": name, "description": description,
            "fields": f, "evidenceRequirements": {}, "numerics": numerics,
            "outputs": outputs,
            "examples": []}


def pack(kind, folder, pack_id, release, rule_id, name, description):
    score = score_infant if kind == "saeugling" else (lambda: score_regular(kind == "kind"))
    # Schwellen: Erwachsene und Kinder ab 19 Monaten (BRi 5.10.1, § 15 Abs. 3 SGB XI);
    # bis 18 Monate eine Stufe höher (§ 15 Abs. 7 SGB XI, BRi 6.6.1). Gesamtpunkte haben höchstens zwei Nachkommastellen.
    if kind == "saeugling":
        bands = [(0, 12.4999, 0), (12.5, 26.9999, 2), (27, 47.4999, 3), (47.5, 69.9999, 4), (70, 100, 5)]
    else:
        bands = [(0, 12.4999, 0), (12.5, 26.9999, 1), (27, 47.4999, 2), (47.5, 69.9999, 3), (70, 89.9999, 4), (90, 100, 5)]
    outputs = [grade_output(score(), bands), choice_output("pflegegrad_5_besondere_bedarfskonstellation", special())]
    rule = {"id": rule_id, "version": 1, "validFrom": VALID_FROM, "validUntil": None, "sourceId": SOURCE_ID,
            "condition": {"kind": "any", "conditions": [gte(score(), 12.5), special()]},
            "onMatch": "SUPPORTED", "onNoMatch": "NOT_SUPPORTED", "onUnknown": "HUMAN_REVIEW"}
    data = {"manifest": {"formatVersion": 1, "packId": pack_id, "releaseId": release, "lifecycleStatus": "IN_REVIEW",
                         "validationLevel": "PUBLIC_REFERENCE", "entryRuleId": rule_id},
            "fields": fields(kind), "sources": [SOURCE], "outputs": outputs, "rules": [rule]}
    target = ROOT / folder
    target.mkdir(parents=True, exist_ok=True)
    (target / "pack.json").write_text(json.dumps(data, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    (target / "presentation.de-DE.json").write_text(
        json.dumps(presentation(kind, pack_id, name, description), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main():
    pack("erwachsen", "pflege-2026-erwachsene", "public-reference.md-bund-pflege-2026-erwachsene",
         "pflege-2026-erwachsene-2026-10-01-public-reference.1", "MD-BUND-PFLEGE-2026-ERWACHSENE",
         "Öffentliche Referenz – Pflege 2026: Erwachsene (Einzelkriterien)",
         "Berechnet aus den Einzelkriterien der Module 1–6 die Gesamtpunkte und den Pflegegrad nach den BRi vom 26.08.2026. "
         "Öffentliche Referenz, fachlich nicht freigegeben.")
    pack("kind", "pflege-2026-kinder", "public-reference.md-bund-pflege-2026-kinder",
         "pflege-2026-kinder-2026-10-01-public-reference.1", "MD-BUND-PFLEGE-2026-KINDER",
         "Öffentliche Referenz – Pflege 2026: Kinder und Jugendliche ab 19 Monaten",
         "Wie Erwachsene, zusätzlich Frühförderung (5.K); unter 11 Jahren Berechnung der Einzelpunkte mit Altersvergleich (BRi 6.5). "
         "Öffentliche Referenz, fachlich nicht freigegeben.")
    pack("saeugling", "pflege-2026-saeuglinge", "public-reference.md-bund-pflege-2026-saeuglinge",
         "pflege-2026-saeuglinge-2026-10-01-public-reference.1", "MD-BUND-PFLEGE-2026-SAEUGLINGE",
         "Öffentliche Referenz – Pflege 2026: Kinder bis 18 Monate",
         "Module 3 und 5 sowie KF 4.4.0; Einstufung eine Stufe höher (§ 15 Abs. 7 SGB XI). Gewichtung nach BRi 5.10.1 "
         "abgeleitet, fachliche Prüfung offen.")


if __name__ == "__main__":
    main()
