"""Fiktive Vorgänge für Vorführung und Case Study.

Alle Personen, Einrichtungen, Befunde und Kennungen sind frei erfunden. Die fachliche Auswertung
nutzt ausschließlich die vorhandenen Knowledge Packs (öffentliche Referenzen, nicht fachlich freigegeben).
"""

PFLEGE = "public-reference/pflege-2026-erwachsene"
PFLEGE_KIND = "public-reference/pflege-2026-kinder"
PFLEGE_SAEUGLING = "public-reference/pflege-2026-saeuglinge"
PFLEGE_PACKS = (PFLEGE, PFLEGE_KIND, PFLEGE_SAEUGLING)
KT = "public-reference/kt-rl-8-3"

T, N, U = "TRUTH", "NUMBER", "UNKNOWN"


def truth(v):
    return {"kind": T, "truth": v}


def num(v):
    return {"kind": N, "number": v}


UNK = {"kind": U}

MODULES = ["Mobilität", "Kognitive und kommunikative Fähigkeiten", "Verhaltensweisen und psychische Problemlagen",
           "Selbstversorgung", "Bewältigung von krankheits- und therapiebedingten Anforderungen",
           "Gestaltung des Alltagslebens und sozialer Kontakte"]


def pflege_facts(fields, criteria, override=None):
    """Alle Felder des Regelwerks: nicht genannte Kriterien 0 bzw. nein, None = nicht erhoben."""
    values = {**criteria, **(override or {})}
    out = {}
    for f in fields:
        v = values.get(f["id"], 0 if f["type"] == "number" else "NO")
        out[f["id"]] = UNK if v is None else (truth(v) if f["type"] == "truth" else num(v))
    return out


CASES = [
    # ------------------------------------------------------------------ Pflegebegutachtung
    {
        "id": "pg-2026-104233", "pack": PFLEGE, "date": "2026-11-04",
        "person": ("Lehmann", "Gerda", "14.03.1946", "Lindenweg 12, 28215 Bremen"),
        "title": "Pflegegrad · Erstantrag",
        "criteria": {"kf_1_1": 1, "kf_1_2": 1, "kf_1_3": 2, "kf_1_4": 2, "kf_1_5": 3,
                     "kf_2_4": 1, "kf_2_5": 1, "kf_2_6": 1, "kf_2_8": 1, "kf_3_10": 1,
                     "kf_4_1": 1, "kf_4_2": 1, "kf_4_3": 2, "kf_4_4": 3, "kf_4_5": 2, "kf_4_6": 3, "kf_4_7": 2, "kf_4_8": 1, "kf_4_10": 1,
                     "kf_5_1_tag": 2, "kf_5_6_tag": 2, "kf_5_7_tag": 2, "kf_5_13_monat": 2, "kf_5_16": 1,
                     "kf_6_1": 2, "kf_6_2": 1, "kf_6_3": 1, "kf_6_4": 2, "kf_6_6": 2},
        "expected": "SUPPORTED", "grade": 3,
        "clinic": ("Klinikum am Lindenpark", "Klinik für Neurologie", "Dr. med. Katrin Hartwig"),
        "diagnoses": [("I63.5", "Hirninfarkt durch Verschluss zerebraler Arterien, rechtshemisphärisch"),
                      ("G81.9", "Hemiparese links, armbetont"), ("I10.00", "Arterielle Hypertonie"),
                      ("E11.9", "Diabetes mellitus Typ 2, nicht insulinpflichtig")],
        "findings": "Aufnahme am 02.10.2026 mit akuter Schwäche der linken Körperhälfte. CT/MRT: frischer Infarkt im "
                    "Mediastromgebiet rechts. Systemische Lyse ohne Komplikationen. Bei Entlassung persistierende "
                    "armbetonte Hemiparese links (Kraftgrad 3/5), Gang mit Rollator über kurze Strecken möglich, "
                    "Transfers nur mit Hilfe. Leichte Aufmerksamkeitsstörung, Orientierung erhalten.",
        "diary": [("Morgens", "Aufstehen, Waschen am Waschbecken mit Hilfe der Tochter, Ankleiden Oberkörper übernommen"),
                  ("Mittags", "Mahlzeit vorbereitet und zerkleinert, Essen selbstständig mit Hilfsmitteln"),
                  ("Nachmittags", "Begleitung zur Physiotherapie, Toilettengänge mit Hilfe beim Transfer"),
                  ("Abends", "Medikamente gerichtet, Auskleiden und Lagerung im Bett")],
    },
    {
        "id": "pg-2026-104271", "pack": PFLEGE, "date": "2026-11-05",
        "person": ("Brandt", "Helmut", "02.09.1941", "Am Steinbrink 4, 31785 Hameln"),
        "title": "Pflegegrad · Höherstufungsantrag",
        "criteria": {"kf_1_1": 1, "kf_1_3": 1, "kf_1_4": 1, "kf_1_5": 3,
                     "kf_2_1": 1, "kf_2_2": 3, "kf_2_3": 3, "kf_2_4": 3, "kf_2_5": 3, "kf_2_6": 3, "kf_2_7": 2, "kf_2_8": 3,
                     "kf_2_9": 1, "kf_2_10": 1, "kf_2_11": 1,
                     "kf_3_1": 3, "kf_3_2": 3, "kf_3_8": 1, "kf_3_10": 1, "kf_3_13": 1,
                     "kf_4_1": 2, "kf_4_2": 2, "kf_4_3": 2, "kf_4_4": 3, "kf_4_5": 2, "kf_4_6": 2, "kf_4_7": 3, "kf_4_8": 2, "kf_4_9": 1,
                     "kf_4_10": 2, "kf_4_11": 2,
                     "kf_5_1_tag": 4, "kf_5_13_monat": 1,
                     "kf_6_1": 3, "kf_6_2": 3, "kf_6_3": 2, "kf_6_4": 3, "kf_6_5": 1, "kf_6_6": 2},
        "expected": "SUPPORTED", "grade": 4,
        "clinic": ("Gedächtnisambulanz Weserbergland", "Gerontopsychiatrie", "Dr. med. Jonas Petersen"),
        "diagnoses": [("F00.1", "Demenz bei Alzheimer-Krankheit mit spätem Beginn"),
                      ("G30.1", "Alzheimer-Krankheit mit spätem Beginn"), ("I48.0", "Vorhofflimmern, paroxysmal")],
        "findings": "Deutliche Progredienz seit der Vorbegutachtung. MMST 14/30 (Vorjahr 19/30). Zeitlich und örtlich "
                    "desorientiert, erkennt nahe Angehörige meist. Nächtliche Unruhe mit Weglauftendenz, abends "
                    "zeitweise abwehrend bei der Körperpflege. Gangbild kleinschrittig, Sturz im September 2026.",
        "diary": [("Nachts", "Zwei- bis dreimal wach, Begleitung zur Toilette, Beruhigung"),
                  ("Morgens", "Körperpflege vollständig übernommen, Ankleiden mit Anleitung"),
                  ("Tagsüber", "Ständige Beaufsichtigung, Tagespflege dienstags und donnerstags"),
                  ("Abends", "Abendessen anreichen, Medikamente verabreichen")],
    },
    {
        "id": "pg-2026-104308", "pack": PFLEGE, "date": "2026-11-06",
        "person": ("Yilmaz", "Ayşe", "21.11.1954", "Hafenstraße 88, 26382 Wilhelmshaven"),
        "title": "Pflegegrad · Erstantrag",
        "criteria": {"kf_1_4": 1, "kf_1_5": 2, "kf_4_4": 1, "kf_5_13_monat": 1},
        "expected": "NOT_SUPPORTED", "grade": 0,
        "clinic": ("Lungenfachpraxis am Rathaus", "Pneumologie", "Dr. med. Svenja Albers"),
        "diagnoses": [("J44.11", "COPD mit akuter Exazerbation, FEV1 ≥ 50 % und < 70 %"),
                      ("I11.90", "Hypertensive Herzkrankheit ohne Herzinsuffizienz")],
        "findings": "Belastungsdyspnoe ab etwa 200 m Gehstrecke, Treppensteigen eine Etage mit Pause. Kein "
                    "Sauerstoffbedarf in Ruhe. Selbstversorgung im Wesentlichen selbstständig, Hilfe beim Duschen "
                    "(Rückenwäsche) und beim Einkauf schwerer Gegenstände.",
        "diary": [("Morgens", "Duschen selbstständig, Hilfe beim Waschen des Rückens"),
                  ("Tagsüber", "Haushalt mit Pausen, schwere Einkäufe übernimmt der Sohn"),
                  ("Abends", "Keine Unterstützung erforderlich")],
    },
    {
        "id": "pg-2026-104315", "pack": PFLEGE, "date": "2026-11-09",
        "person": ("Kowalski", "Werner", "30.06.1949", "Birkenallee 7, 27570 Bremerhaven"),
        "title": "Pflegegrad · Erstantrag",
        "criteria": {"kf_1_3": 1, "kf_1_4": 2, "kf_1_5": 2, "kf_2_4": 1, "kf_2_5": 1, "kf_2_6": 1, "kf_3_10": 1, "kf_3_11": 1,
                     "kf_4_1": 1, "kf_4_2": 1, "kf_4_3": 1, "kf_4_4": None, "kf_4_5": 1, "kf_4_6": 2, "kf_4_7": 2, "kf_4_10": 1,
                     "kf_5_1_tag": 4, "kf_5_13_monat": 1,
                     "kf_6_1": 1, "kf_6_3": 1, "kf_6_4": 1, "kf_6_6": 2},
        "clarified": {"kf_4_4": 2}, "expected": "INCOMPLETE",
        "expected_after_clarification": "SUPPORTED", "grade": 2,
        "clinic": ("Hausarztpraxis Dr. Behrens", "Allgemeinmedizin", "Dr. med. Ulrike Behrens"),
        "diagnoses": [("G20.10", "Primäres Parkinson-Syndrom mit mäßiger Beeinträchtigung"),
                      ("M17.1", "Gonarthrose beidseits"), ("N40", "Prostatahyperplasie")],
        "findings": "Parkinson-Syndrom seit 2019, zunehmende Akinese und Rigor, On-Off-Schwankungen. Gehen in der "
                    "Wohnung mit Gehstock, Treppen nur mit Festhalten. Feinmotorik deutlich eingeschränkt. Die "
                    "Erhebung zur Selbstversorgung (Modul 4) konnte beim Hausbesuch nicht abgeschlossen werden, "
                    "weil der Versicherte sich in einer Off-Phase befand; Nachbesuch vereinbart.",
        "diary": [("Morgens", "Ankleiden dauert über 30 Minuten, Hilfe bei Knöpfen und Schuhen"),
                  ("Mittags", "Essen mit Besteck mühsam, Getränke in Bechern mit Deckel"),
                  ("Abends", "Hilfe beim Duschen zweimal wöchentlich durch die Ehefrau")],
    },
    {
        "id": "pg-2026-104342", "pack": PFLEGE, "date": "2026-11-10",
        "person": ("Schulte", "Marie", "08.01.1958", "Am Mühlenteich 3, 49074 Osnabrück"),
        "title": "Pflegegrad · Höherstufungsantrag",
        "criteria": {"kf_1_1": 2, "kf_1_2": 1, "kf_1_3": 3, "kf_1_4": 1, "kf_1_5": 3, "kf_3_10": 1,
                     "kf_4_3": 3, "kf_4_4": 3, "kf_4_6": 3, "kf_4_10": 3, "kf_4_11": 3, "kf_4_12": 3,
                     "kf_5_1_tag": 1, "kf_5_2_tag": 1, "kf_5_8_tag": 1, "kf_5_10_tag": 5, "kf_5_13_monat": 2,
                     "kf_6_1": 1, "kf_6_2": 3, "kf_6_4": 1, "kf_6_6": 2},
        "expected": "SUPPORTED", "grade": 4,
        "clinic": ("Querschnittzentrum Nordwest", "Rehabilitationsmedizin", "Prof. Dr. med. Henrik Vogt"),
        "diagnoses": [("G82.22", "Paraplegie, komplett, chronisch"), ("N31.9", "Neurogene Blasenentleerungsstörung"),
                      ("L89.24", "Dekubitus Grad 3, Sakralregion, in Abheilung")],
        "findings": "Komplette Querschnittlähmung sub Th6 nach Verkehrsunfall 2025. Rollstuhlmobilisation, "
                    "sämtliche Transfers mit Lifter und zwei Pflegepersonen. Intermittierender Selbstkatheterismus "
                    "nicht möglich, Fremdkatheterisierung fünfmal täglich. Wundversorgung sakral täglich.",
        "diary": [("Rund um die Uhr", "Lagerungswechsel alle drei Stunden, auch nachts"),
                  ("Morgens", "Ganzkörperpflege im Bett, Ankleiden, Transfer in den Rollstuhl mit Lifter"),
                  ("Mehrmals täglich", "Katheterisierung, Darmmanagement, Wundversorgung")],
    },
    {
        "id": "pg-2026-104366", "pack": PFLEGE_SAEUGLING, "date": "2026-11-11",
        "person": ("Petersen", "Mila", "14.02.2026", "Am Deich 3, 25813 Husum"),
        "title": "Pflegegrad · Erstantrag (Kind bis 18 Monate)",
        "criteria": {"kf_4_0": 1, "kf_5_1_tag": 3, "kf_5_4_tag": 6, "kf_5_13_woche": 1, "kf_5_k_woche": 1},
        "expected": "SUPPORTED", "grade": 3,
        "clinic": ("Kinderklinik Nordfriesland", "Neonatologie und Kinderneurologie", "Dr. med. Hauke Jensen"),
        "diagnoses": [("P07.3", "Frühgeborenes, 31. Schwangerschaftswoche"), ("R13.9", "Schluckstörung"),
                      ("Q87.8", "Angeborenes Fehlbildungssyndrom, genetische Abklärung laufend")],
        "findings": "Seit der Geburt ausgeprägte Schluckstörung mit wiederholten Aspirationen. Ernährung überwiegend über "
                    "nasogastrale Sonde, kleine Mengen oral unter logopädischer Anleitung. Häufiges Absaugen der oberen "
                    "Atemwege nötig. Motorische Entwicklung verzögert.",
        "diary": [("Nachts", "Zwei Sondenmahlzeiten, Absaugen bei Sekretstau"),
                  ("Morgens", "Sondenmahlzeit, orale Trinkversuche mit Andicken, Medikamente über Sonde"),
                  ("Tagsüber", "Absaugen etwa sechsmal, Frühförderung einmal wöchentlich"),
                  ("Abends", "Sondenmahlzeit, Lagerung zur Aspirationsprophylaxe")],
    },
    {
        "id": "pg-2026-104373", "pack": PFLEGE_KIND, "date": "2026-11-12",
        "person": ("Aydin", "Elif", "03.05.2019", "Lindenstraße 41, 49074 Osnabrück"),
        "title": "Pflegegrad · Erstantrag (Kind)",
        "criteria": {"alter_monate": 90, "kf_2_2": 2, "kf_2_3": 2, "kf_2_4": 1, "kf_2_6": 2, "kf_2_7": 2, "kf_2_8": 3, "kf_2_9": 1, "kf_2_11": 2,
                     "kf_3_1": 3, "kf_3_3": 3, "kf_3_5": 1, "kf_3_6": 1, "kf_3_8": 3, "kf_3_10": 1, "kf_3_12": 1,
                     "kf_4_1": 1, "kf_4_3": 1, "kf_4_4": 3, "kf_4_5": 1, "kf_4_6": 1, "kf_4_7": 2, "kf_4_10": 1, "kf_4_11": 1,
                     "kf_5_13_monat": 2, "kf_5_k_woche": 2, "kf_5_16": 1,
                     "kf_6_1": 2, "kf_6_3": 2, "kf_6_5": 2, "kf_6_6": 2},
        "expected": "SUPPORTED", "grade": 3,
        "clinic": ("Sozialpädiatrisches Zentrum Osnabrück", "Entwicklungsneurologie", "Dr. med. Miriam Kastner"),
        "diagnoses": [("F84.0", "Frühkindlicher Autismus"), ("F70.1", "Leichte Intelligenzminderung mit Verhaltensstörung"),
                      ("G40.3", "Generalisierte Epilepsie, medikamentös eingestellt")],
        "findings": "Siebenjähriges Mädchen mit frühkindlichem Autismus. Sprache in Zwei- bis Dreiwortsätzen, "
                    "Aufforderungen werden in vertrauter Umgebung verstanden. Täglich selbstverletzendes Verhalten "
                    "(Kopfschlagen) bei Überforderung, häufige Abwehr bei Körperpflege. Keine Gefahreneinschätzung im "
                    "Straßenverkehr. Bewertung im Vergleich mit altersentsprechend entwickelten Kindern.",
        "diary": [("Morgens", "Waschen und Anziehen mit Anleitung und Übernahme, Abwehr beim Haarewaschen"),
                  ("Schule", "Förderschule mit Schulbegleitung, Abholung wegen Krisen etwa einmal pro Woche"),
                  ("Nachmittags", "Ständige Aufsicht, zweimal wöchentlich Autismustherapie"),
                  ("Abends", "Beruhigungsrituale, Einschlafbegleitung bis zu einer Stunde")],
    },
    {
        "id": "pg-2026-104380", "pack": PFLEGE, "date": "2026-11-13",
        "person": ("Krüger", "Bernd", "07.09.1961", "Schillerstraße 5, 30159 Hannover"),
        "title": "Pflegegrad · Höherstufungsantrag",
        "criteria": {"kf_1_b": "YES", "kf_1_1": 3, "kf_1_2": 3, "kf_1_3": 3, "kf_1_4": 3, "kf_1_5": 3,
                     "kf_4_1": 3, "kf_4_2": 3, "kf_4_3": 3, "kf_4_4": 3, "kf_4_5": 3, "kf_4_6": 3, "kf_4_7": 3, "kf_4_8": 3,
                     "kf_4_9": 3, "kf_4_10": 3, "kf_4_11": 3, "kf_4_12": 3,
                     "kf_5_1_tag": 3, "kf_5_4_tag": 4, "kf_5_11_woche": 2, "kf_5_13_monat": 1,
                     "kf_6_1": 1, "kf_6_3": 2, "kf_6_6": 1},
        "expected": "SUPPORTED", "grade": 5,
        "clinic": ("Medizinische Hochschule am Maschsee", "Neurologie, ALS-Ambulanz", "Prof. Dr. med. Ines Brehm"),
        "diagnoses": [("G12.2", "Amyotrophe Lateralsklerose"), ("R13.9", "Dysphagie"), ("J96.1", "Chronische respiratorische Insuffizienz")],
        "findings": "Fortgeschrittene ALS mit vollständiger Lähmung beider Arme und Beine. Greifen, Stehen und Gehen nicht "
                    "möglich, auch nicht mit Hilfsmitteln. Kognition und Kommunikation über Augensteuerung erhalten. "
                    "Nächtliche nicht-invasive Beatmung, häufiges Absaugen.",
        "diary": [("Morgens", "Vollständige Übernahme der Körperpflege, Transfer mit Lifter"),
                  ("Mittags", "Essen anreichen in angedickter Form, Absaugen"),
                  ("Nachmittags", "Physiotherapie zweimal wöchentlich zu Hause, Kommunikation per Augensteuerung"),
                  ("Nachts", "Maskenbeatmung anlegen, Lagerung alle drei Stunden")],
    },
    # ------------------------------------------------------------------ Krankenbeförderung
    {
        "id": "kt-2026-208114", "pack": KT, "date": "2026-11-03",
        "person": ("Berg", "Marlene", "17.05.1938", "Kastanienweg 21, 26121 Oldenburg"),
        "title": "Krankenbeförderung · ambulante Behandlung",
        "facts": {"ambulatory_treatment": truth("YES"), "strict_medical_necessity": truth("YES"),
                  "disability_marker_ag": truth("NO"), "disability_marker_bl": truth("NO"), "disability_marker_h": truth("NO"),
                  "care_grade": num(4), "care_grade_3_permanent_mobility_transport_need": truth("NO"),
                  "care_level_2_on_2016_12_31": truth("NO"), "care_grade_3_or_higher_since_2017_01_01": truth("NO")},
        "evidence": {"care_grade_notice": "PRESENT", "severe_disability_card": "MISSING", "care_transition_classification_proof": "MISSING"},
        "expected": "SUPPORTED", "care_grade": 4, "markers": None,
        "treatment": "Ambulante Wundversorgung, chronisches Ulcus cruris venosum rechts, 2x wöchentlich für 6 Wochen",
        "practice": ("Gefäßpraxis am Schlossplatz", "Dr. med. Florian Neumann"),
    },
    {
        "id": "kt-2026-208157", "pack": KT, "date": "2026-11-04",
        "person": ("Hartmann", "Ulrich", "11.12.1950", "Grüner Weg 9, 33602 Bielefeld"),
        "title": "Krankenbeförderung · ambulante Behandlung",
        "facts": {"ambulatory_treatment": truth("YES"), "strict_medical_necessity": truth("YES"),
                  "disability_marker_ag": truth("YES"), "disability_marker_bl": truth("NO"), "disability_marker_h": truth("NO"),
                  "care_grade": num(2), "care_grade_3_permanent_mobility_transport_need": truth("NO"),
                  "care_level_2_on_2016_12_31": truth("NO"), "care_grade_3_or_higher_since_2017_01_01": truth("NO")},
        "evidence": {"severe_disability_card": "CONFLICTING", "care_grade_notice": "PRESENT", "care_transition_classification_proof": "MISSING"},
        "expected": "HUMAN_REVIEW", "care_grade": 2, "markers": "G",
        "treatment": "Ambulante Chemotherapie-Nachsorge, onkologische Kontrolle alle 3 Wochen",
        "practice": ("Onkologische Schwerpunktpraxis Bielefeld-Mitte", "Dr. med. Sabine Köhler"),
        "note": "Laut Verordnung Merkzeichen aG; der beigefügte Ausweis weist nur das Merkzeichen G aus.",
    },
    {
        "id": "kt-2026-208190", "pack": KT, "date": "2026-11-05",
        "person": ("Neumann", "Ingrid", "29.02.1944", "Marktstraße 2, 49377 Vechta"),
        "title": "Krankenbeförderung · ambulante Behandlung",
        "facts": {"ambulatory_treatment": truth("YES"), "strict_medical_necessity": truth("YES"),
                  "disability_marker_ag": truth("NO"), "disability_marker_bl": truth("NO"), "disability_marker_h": truth("NO"),
                  "care_grade": num(2), "care_grade_3_permanent_mobility_transport_need": truth("NO"),
                  "care_level_2_on_2016_12_31": truth("NO"), "care_grade_3_or_higher_since_2017_01_01": truth("NO")},
        "evidence": {"care_grade_notice": "PRESENT", "severe_disability_card": "PRESENT", "care_transition_classification_proof": "PRESENT"},
        "expected": "NOT_SUPPORTED", "care_grade": 2, "markers": "",
        "transition": "Pflegestufe 1 bis 31.12.2016, übergeleitet in Pflegegrad 2",
        "treatment": "Augenärztliche Kontrolle nach Katarakt-Operation links",
        "practice": ("Augenzentrum Vechta", "Dr. med. Tobias Rieger"),
    },
    {
        "id": "kt-2026-208203", "pack": KT, "date": "2026-11-06",
        "person": ("Krüger", "Dieter", "03.04.1947", "Ringstraße 15, 26603 Aurich"),
        "title": "Krankenbeförderung · ambulante Behandlung",
        "facts": {"ambulatory_treatment": truth("YES"), "strict_medical_necessity": UNK,
                  "disability_marker_ag": truth("NO"), "disability_marker_bl": truth("NO"), "disability_marker_h": truth("NO"),
                  "care_grade": num(4), "care_grade_3_permanent_mobility_transport_need": truth("NO"),
                  "care_level_2_on_2016_12_31": truth("NO"), "care_grade_3_or_higher_since_2017_01_01": truth("NO")},
        "evidence": {"care_grade_notice": "PRESENT", "severe_disability_card": "MISSING", "care_transition_classification_proof": "MISSING"},
        "expected": "INCOMPLETE", "care_grade": 4, "markers": None,
        "treatment": "Urologische Kontrolle, Katheterwechsel",
        "practice": ("Urologie am Ems-Jade-Kanal", "Dr. med. Martin Saathoff"),
        "note": "Das Feld zur medizinischen Notwendigkeit ist auf der Verordnung nicht angekreuzt.",
    },
    {
        "id": "kt-2026-208226", "pack": KT, "date": "2026-11-07",
        "person": ("Albers", "Johanna", "25.08.1961", "Deichweg 40, 26721 Emden"),
        "title": "Krankenbeförderung · ambulante Behandlung",
        "facts": {"ambulatory_treatment": truth("YES"), "strict_medical_necessity": truth("YES"),
                  "disability_marker_ag": truth("NO"), "disability_marker_bl": truth("YES"), "disability_marker_h": truth("NO"),
                  "care_grade": UNK, "care_grade_3_permanent_mobility_transport_need": truth("NO"),
                  "care_level_2_on_2016_12_31": truth("NO"), "care_grade_3_or_higher_since_2017_01_01": truth("NO")},
        "evidence": {"severe_disability_card": "PRESENT", "care_grade_notice": "MISSING", "care_transition_classification_proof": "MISSING"},
        "expected": "SUPPORTED", "care_grade": None, "markers": "Bl",
        "treatment": "Intravitreale Injektionstherapie, monatlich",
        "practice": ("Netzhautzentrum Emden", "Dr. med. Lena Fokken"),
    },
]


# ------------------------------------------------------------------ Angaben für die Fallakte
# Ergänzt die Vorgänge um das, was in einer echten Akte zusätzlich vorliegt. Alles frei erfunden.

FILE = {
    "pg-2026-104233": {
        "carer": "Tochter Sabine Lehmann (wohnt im Haus)",
        "service": ("Ambulanter Pflegedienst Weserblick", "Seit dem 20.10.2026 täglich morgens Unterstützung bei der Grundpflege "
                    "(Waschen am Waschbecken, Ankleiden, Kompressionsstrümpfe). Transfer Bett–Rollstuhl mit Hilfe einer Person. "
                    "Blutzuckermessung zweimal täglich durch den Dienst. Keine Druckstellen."),
        "function": "Feinmotorik der linken Hand aufgehoben, Greifen nur mit rechts. Stehen ohne Halt wenige Sekunden. "
                    "Treppensteigen nicht möglich. Medikamente werden von der Tochter gerichtet.",
        "aids": ["Rollator", "Toilettensitzerhöhung", "Duschhocker", "Greifzange"],
        "prognosis": "Unter ambulanter Physio- und Ergotherapie leichte Besserung möglich; dauerhafte Hilfe bei der "
                     "Selbstversorgung über mindestens sechs Monate zu erwarten.",
        "letter": ("Entlassungsbericht", "Stationäre Behandlung vom 02.10. bis 16.10.2026 auf der Stroke Unit. Unter Lyse rasche "
                   "Rückbildung der Sprachstörung, Hemiparese links persistierend.",
                   "Anschlussheilbehandlung beantragt, Fortsetzung der Sekundärprophylaxe.", "2026-10-16"),
        "medication": [("ASS", "100 mg", "1-0-0-0", "zum Frühstück"), ("Atorvastatin", "40 mg", "0-0-1-0", ""),
                       ("Ramipril", "5 mg", "1-0-0-0", ""), ("Metformin", "1000 mg", "1-0-1-0", "zu den Mahlzeiten")],
    },
    "pg-2026-104271": {
        "carer": "Ehefrau Renate Brandt, unterstützt durch den Sohn",
        "service": ("Tagespflege Am Klüt", "Besuch dienstags und donnerstags von 8 bis 16 Uhr. Herr Brandt benötigt "
                    "ständige Begleitung, verlässt unbeaufsichtigt die Gruppe. Essen wird angereicht, Toilettengänge "
                    "nach Plan. Zunehmende Unruhe am Nachmittag."),
        "function": "Kann Handlungen nicht mehr planen, braucht für jede Verrichtung Anleitung und meist Übernahme. "
                    "Gefahren werden nicht erkannt (Herd, Straßenverkehr).",
        "aids": ["Gehstock", "Hausnotruf", "Bettgitter (nachts)"],
        "prognosis": "Fortschreitende Erkrankung; weitere Zunahme des Hilfebedarfs zu erwarten.",
        "letter": ("Arztbrief Gedächtnisambulanz", "Verlaufskontrolle. MMST von 19 auf 14 Punkte gefallen, "
                   "Verhaltensauffälligkeiten am Abend deutlich zugenommen.",
                   "Fortführung Donepezil, Melperon abends; Entlastung der Ehefrau dringend empfohlen.", "2026-09-22"),
        "previous": ("2025-03-11", 3),
        "medication": [("Donepezil", "10 mg", "0-0-1-0", ""), ("Apixaban", "5 mg", "1-0-1-0", "Blutverdünner"),
                       ("Melperon", "25 mg", "0-0-1-0", "bei Unruhe")],
    },
    "pg-2026-104308": {
        "carer": "Sohn Emre Yilmaz (wohnt in der Nähe)",
        "function": "Gehstrecke etwa 200 m, Treppe eine Etage mit Pause. Selbstversorgung selbstständig, "
                    "Hilfe nur beim Rückenwaschen und bei schweren Einkäufen.",
        "aids": ["Inhalationsgerät"],
        "prognosis": "Unter leitliniengerechter Therapie stabil; keine wesentliche Zunahme des Hilfebedarfs erwartet.",
        "letter": ("Befund Lungenfunktion", "Spirometrie: FEV1 61 % des Sollwerts, Tiffeneau-Index 58 %. "
                   "Blutgase in Ruhe unauffällig.", "Fortführung der Inhalationstherapie, Lungensport empfohlen.", "2026-08-27"),
        "medication": [("Tiotropium/Olodaterol", "2,5/2,5 µg", "2-0-0-0", "Hübe"), ("Salbutamol", "100 µg", "0-0-0-0", "bei Bedarf"),
                       ("Candesartan", "8 mg", "1-0-0-0", "")],
    },
    "pg-2026-104315": {
        "carer": "Ehefrau Brigitte Kowalski",
        "service": ("Pflegedienst Nordsee mobil", "Zweimal wöchentlich Unterstützung beim Duschen. Ausgeprägte "
                    "On-Off-Schwankungen: in der Off-Phase kaum Eigenbewegung, in der On-Phase Ankleiden mit Hilfe bei "
                    "Knöpfen möglich. Sturz im Bad am 28.10.2026 ohne Verletzung."),
        "function": "Feinmotorik beidseits deutlich eingeschränkt, Schreiben kaum möglich. Freezing an Türschwellen. "
                    "Leistungsfähigkeit schwankt stark mit der Medikamentenwirkung.",
        "aids": ["Gehstock", "Haltegriffe im Bad", "Trinkbecher mit Deckel"],
        "prognosis": "Langsam fortschreitend; Anpassung der Medikation zur Glättung der Wirkschwankungen geplant.",
        "letter": ("Arztbrief Neurologie", "Ambulante Vorstellung. Zunehmende Wirkfluktuationen unter Levodopa, "
                   "Off-Phasen bis zu zwei Stunden täglich.", "Pramipexol eindosiert, Kontrolle in drei Monaten.", "2026-07-14"),
        "medication": [("Levodopa/Benserazid", "100/25 mg", "1-1-1-1", "30 Min. vor dem Essen"), ("Pramipexol", "0,7 mg", "1-0-1-0", ""),
                       ("Tamsulosin", "0,4 mg", "0-0-1-0", "")],
    },
    "pg-2026-104342": {
        "carer": "Ehemann Thomas Schulte und Pflegedienst",
        "service": ("Intensivpflege Hasetal", "Dreimal täglich Einsatz, Fremdkatheterisierung fünfmal täglich (zweimal durch "
                    "den Ehemann). Wundversorgung sakral täglich, Wunde 2 x 1,5 cm, sauber granulierend. Lagerung nachts "
                    "durch den Ehemann."),
        "function": "Keine Eigenbewegung unterhalb Th6. Oberkörper und Arme frei beweglich; Rollstuhl wird selbst "
                    "angetrieben. Alle Transfers mit Lifter.",
        "aids": ["Aktivrollstuhl", "Deckenlifter", "Wechseldruckmatratze", "Duschrollstuhl"],
        "prognosis": "Lähmung dauerhaft; Dekubitus in Abheilung, weiterer Pflegebedarf auf Dauer.",
        "letter": ("Entlassungsbericht", "Stationäre Behandlung des Dekubitus sakral vom 01.09. bis 29.09.2026 mit "
                   "plastischer Deckung.", "Konsequente Druckentlastung, Lagerung alle drei Stunden.", "2026-09-29"),
        "previous": ("2025-11-20", 3),
        "medication": [("Baclofen", "10 mg", "1-1-1-0", ""), ("Enoxaparin", "40 mg", "0-0-1-0", "s.c."),
                       ("Macrogol", "13,1 g", "1-0-0-0", "1 Beutel")],
    },
    "pg-2026-104366": {
        "carer": "Eltern Jana und Sönke Petersen",
        "service": ("Kinderkrankenpflege Westküste", "Dreimal wöchentlich Unterstützung bei Sondenernährung und Absaugen. "
                    "Eltern sicher in Sondengabe und Absaugtechnik geschult. Gewichtsverlauf stabil an der 3. Perzentile."),
        "function": "Altersentsprechende Selbständigkeit nicht beurteilbar; Nahrungsaufnahme mit außergewöhnlich "
                    "pflegeintensivem Hilfebedarf (Sonde, Aspirationsgefahr, Absaugen).",
        "aids": ["Ernährungspumpe", "Absauggerät", "Pulsoxymeter"],
        "prognosis": "Schluckstörung voraussichtlich über mindestens sechs Monate; PEG-Anlage wird erwogen.",
        "letter": ("Entlassungsbericht", "Stationäre Behandlung von der Geburt bis zum 02.05.2026, anschließend "
                   "Wiederaufnahme wegen Aspirationspneumonie vom 10.08. bis 24.08.2026.",
                   "Fortführung der Sondenernährung, Logopädie und Frühförderung.", "2026-08-24"),
        "medication": [("Colecalciferol", "500 I.E.", "1-0-0-0", "über Sonde"), ("Omeprazol", "5 mg", "1-0-0-0", "über Sonde"),
                       ("Levetiracetam", "50 mg", "1-0-1-0", "über Sonde")],
    },
    "pg-2026-104373": {
        "carer": "Mutter Derya Aydin (Hauptpflegeperson), Vater berufstätig",
        "function": "Im Vergleich mit gleichaltrigen Kindern deutlich mehr Anleitung und Beaufsichtigung bei Körperpflege, "
                    "An- und Auskleiden und Toilettengang; nächtliches Einnässen. Keine Einschätzung von Gefahren.",
        "aids": ["Autismusspezifische Visualisierungshilfen", "GPS-Tracker"],
        "prognosis": "Dauerhafter Unterstützungsbedarf; Verbesserung einzelner Alltagsfähigkeiten unter Therapie möglich.",
        "letter": ("Arztbrief Sozialpädiatrie", "Verlaufsuntersuchung. Zunahme selbstverletzenden Verhaltens bei "
                   "Übergängen und in lauter Umgebung. EEG ohne neue Auffälligkeiten.",
                   "Fortführung der Autismustherapie, Schulbegleitung, Antrag auf Pflegeleistungen unterstützt.", "2026-09-30"),
        "medication": [("Valproinsäure", "300 mg", "1-0-1-0", "Epilepsie")],
    },
    "pg-2026-104380": {
        "carer": "Ehefrau Martina Krüger und Intensivpflegedienst",
        "service": ("Intensivpflege Leinebogen", "Zweimal täglich Einsatz zur Grundpflege, Absaugen bei Bedarf, Anlage der "
                    "Maskenbeatmung nachts durch die Ehefrau. Keine Eigenbewegung der Extremitäten, Kommunikation über "
                    "Augensteuerung zuverlässig."),
        "function": "Gebrauchsunfähigkeit beider Arme und beider Beine mit vollständigem Verlust der Greif-, Steh- und "
                    "Gehfunktionen, nicht durch Hilfsmittel kompensierbar. Kognition erhalten.",
        "aids": ["Elektrorollstuhl mit Kopfsteuerung", "Deckenlifter", "Augensteuerung", "Heimbeatmungsgerät", "Absauggerät"],
        "prognosis": "Fortschreitende Erkrankung; vollständige Pflegeabhängigkeit auf Dauer.",
        "letter": ("Arztbrief ALS-Ambulanz", "Verlaufskontrolle. Vollständige Tetraparese, Schluckstörung mit Aspirationsgefahr, "
                   "nächtliche Hypoventilation.", "Fortführung der nicht-invasiven Beatmung, PEG-Anlage besprochen.", "2026-10-21"),
        "previous": ("2025-12-02", 3),
        "medication": [("Riluzol", "50 mg", "1-0-1-0", ""), ("Glycopyrroniumbromid", "1 mg", "1-1-1-0", "gegen Speichelfluss"),
                       ("Macrogol", "13,1 g", "1-0-0-0", "1 Beutel")],
    },
    "kt-2026-208114": {
        "letter": "Chronisches Ulcus cruris venosum rechts, 4 x 3 cm. Kompressionstherapie, Wundkontrolle zweimal wöchentlich. "
                  "Patientin wegen Gangunsicherheit nicht in der Lage, öffentliche Verkehrsmittel zu nutzen.",
        "appointments": [("2026-11-10", "09:30"), ("2026-11-13", "09:30"), ("2026-11-17", "09:30"), ("2026-11-20", "09:30")],
        "care_grade_since": "2024-06-03",
    },
    "kt-2026-208157": {
        "letter": "Zustand nach Chemotherapie bei Kolonkarzinom. Onkologische Kontrolle mit Blutbild alle drei Wochen. "
                  "Gehstrecke unter 100 m, Polyneuropathie beider Füße.",
        "appointments": [("2026-11-11", "11:00"), ("2026-12-02", "11:00"), ("2026-12-23", "10:30")],
        "care_grade_since": "2025-08-19",
    },
    "kt-2026-208190": {
        "letter": "Katarakt-Operation links am 22.10.2026 komplikationslos. Kontrolle des Augeninnendrucks und des Visus. "
                  "Patientin gehfähig, fährt nicht selbst Auto.",
        "appointments": [("2026-11-12", "14:15"), ("2026-11-26", "14:15")],
        "care_grade_since": "2017-01-01",
    },
    "kt-2026-208203": {
        "letter": "Suprapubischer Dauerkatheter bei Harnverhalt. Wechsel alle vier Wochen in der Praxis. "
                  "Patient nach Schlaganfall auf Rollstuhl angewiesen.",
        "appointments": [("2026-11-14", "08:45"), ("2026-12-12", "08:45")],
        "care_grade_since": "2023-04-17",
    },
    "kt-2026-208226": {
        "letter": "Feuchte altersbedingte Makuladegeneration beidseits, Visus 0,05. Intravitreale Injektionen monatlich. "
                  "Patientin hochgradig sehbehindert (Merkzeichen Bl).",
        "appointments": [("2026-11-18", "13:00"), ("2026-12-16", "13:00"), ("2027-01-13", "13:00")],
    },
}

for _case in CASES:
    _case.update(FILE[_case["id"]])
