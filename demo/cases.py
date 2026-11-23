"""Fiktive Vorgänge für Vorführung und Case Study.

Alle Personen, Einrichtungen, Befunde und Kennungen sind frei erfunden. Die fachliche Auswertung
nutzt ausschließlich die vorhandenen Knowledge Packs (öffentliche Referenzen, nicht fachlich freigegeben).
"""

PFLEGE = "public-reference/pflege-adult-score"
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


def pflege_facts(m):
    return {f"module_{i + 1}_sum": (UNK if v is None else num(v)) for i, v in enumerate(m)}


CASES = [
    # ------------------------------------------------------------------ Pflegebegutachtung
    {
        "id": "pg-2026-104233", "pack": PFLEGE, "date": "2026-11-04",
        "person": ("Lehmann", "Gerda", "14.03.1946", "Lindenweg 12, 28215 Bremen"),
        "title": "Pflegegrad · Erstantrag",
        "modules": [9, 4, 1, 22, 3, 8], "expected": "SUPPORTED", "grade": 3,
        "clinic": ("Klinikum am Lindenpark", "Klinik für Neurologie", "Dr. med. Katrin Hartwig"),
        "diagnoses": [("I63.5", "Hirninfarkt durch Verschluss zerebraler Arterien, rechtshemisphärisch"),
                      ("G81.9", "Hemiparese links, armbetont"), ("I10.00", "Arterielle Hypertonie"),
                      ("E11.9", "Diabetes mellitus Typ 2, nicht insulinpflichtig")],
        "findings": "Aufnahme am 02.10.2026 mit akuter Schwäche der linken Körperhälfte. CT/MRT: frischer Infarkt im "
                    "Mediastromgebiet rechts. Systemische Lyse ohne Komplikationen. Bei Entlassung persistierende "
                    "armbetonte Hemiparese links (Kraftgrad 3/5), Gang mit Rollator über kurze Strecken möglich, "
                    "Transfers nur mit Hilfe. Leichte Aufmerksamkeitsstörung, Orientierung erhalten.",
        "medication": ["ASS 100 mg 1-0-0", "Atorvastatin 40 mg 0-0-1", "Ramipril 5 mg 1-0-0", "Metformin 1000 mg 1-0-1"],
        "diary": [("Morgens", "Aufstehen, Waschen am Waschbecken mit Hilfe der Tochter, Ankleiden Oberkörper übernommen"),
                  ("Mittags", "Mahlzeit vorbereitet und zerkleinert, Essen selbstständig mit Hilfsmitteln"),
                  ("Nachmittags", "Begleitung zur Physiotherapie, Toilettengänge mit Hilfe beim Transfer"),
                  ("Abends", "Medikamente gerichtet, Auskleiden und Lagerung im Bett")],
    },
    {
        "id": "pg-2026-104271", "pack": PFLEGE, "date": "2026-11-05",
        "person": ("Brandt", "Helmut", "02.09.1941", "Am Steinbrink 4, 31785 Hameln"),
        "title": "Pflegegrad · Höherstufungsantrag",
        "modules": [6, 24, 9, 30, 2, 13], "expected": "SUPPORTED", "grade": 4,
        "clinic": ("Gedächtnisambulanz Weserbergland", "Gerontopsychiatrie", "Dr. med. Jonas Petersen"),
        "diagnoses": [("F00.1", "Demenz bei Alzheimer-Krankheit mit spätem Beginn"),
                      ("G30.1", "Alzheimer-Krankheit mit spätem Beginn"), ("I48.0", "Vorhofflimmern, paroxysmal")],
        "findings": "Deutliche Progredienz seit der Vorbegutachtung. MMST 14/30 (Vorjahr 19/30). Zeitlich und örtlich "
                    "desorientiert, erkennt nahe Angehörige meist. Nächtliche Unruhe mit Weglauftendenz, abends "
                    "zeitweise abwehrend bei der Körperpflege. Gangbild kleinschrittig, Sturz im September 2026.",
        "medication": ["Donepezil 10 mg 0-0-1", "Apixaban 5 mg 1-0-1", "Melperon 25 mg 0-0-1"],
        "diary": [("Nachts", "Zwei- bis dreimal wach, Begleitung zur Toilette, Beruhigung"),
                  ("Morgens", "Körperpflege vollständig übernommen, Ankleiden mit Anleitung"),
                  ("Tagsüber", "Ständige Beaufsichtigung, Tagespflege dienstags und donnerstags"),
                  ("Abends", "Abendessen anreichen, Medikamente verabreichen")],
    },
    {
        "id": "pg-2026-104308", "pack": PFLEGE, "date": "2026-11-06",
        "person": ("Yilmaz", "Ayşe", "21.11.1954", "Hafenstraße 88, 26382 Wilhelmshaven"),
        "title": "Pflegegrad · Erstantrag",
        "modules": [3, 0, 0, 2, 1, 0], "expected": "NOT_SUPPORTED", "grade": 0,
        "clinic": ("Lungenfachpraxis am Rathaus", "Pneumologie", "Dr. med. Svenja Albers"),
        "diagnoses": [("J44.11", "COPD mit akuter Exazerbation, FEV1 ≥ 50 % und < 70 %"),
                      ("I11.90", "Hypertensive Herzkrankheit ohne Herzinsuffizienz")],
        "findings": "Belastungsdyspnoe ab etwa 200 m Gehstrecke, Treppensteigen eine Etage mit Pause. Kein "
                    "Sauerstoffbedarf in Ruhe. Selbstversorgung im Wesentlichen selbstständig, Hilfe beim Duschen "
                    "(Rückenwäsche) und beim Einkauf schwerer Gegenstände.",
        "medication": ["Tiotropium/Olodaterol 2 Hübe 1-0-0", "Salbutamol bei Bedarf", "Candesartan 8 mg 1-0-0"],
        "diary": [("Morgens", "Duschen selbstständig, Hilfe beim Waschen des Rückens"),
                  ("Tagsüber", "Haushalt mit Pausen, schwere Einkäufe übernimmt der Sohn"),
                  ("Abends", "Keine Unterstützung erforderlich")],
    },
    {
        "id": "pg-2026-104315", "pack": PFLEGE, "date": "2026-11-09",
        "person": ("Kowalski", "Werner", "30.06.1949", "Birkenallee 7, 27570 Bremerhaven"),
        "title": "Pflegegrad · Erstantrag",
        "modules": [5, 3, 0, None, 2, 5], "clarified_module_4": 12, "expected": "INCOMPLETE",
        "expected_after_clarification": "SUPPORTED", "grade": 2,
        "clinic": ("Hausarztpraxis Dr. Behrens", "Allgemeinmedizin", "Dr. med. Ulrike Behrens"),
        "diagnoses": [("G20.10", "Primäres Parkinson-Syndrom mit mäßiger Beeinträchtigung"),
                      ("M17.1", "Gonarthrose beidseits"), ("N40", "Prostatahyperplasie")],
        "findings": "Parkinson-Syndrom seit 2019, zunehmende Akinese und Rigor, On-Off-Schwankungen. Gehen in der "
                    "Wohnung mit Gehstock, Treppen nur mit Festhalten. Feinmotorik deutlich eingeschränkt. Die "
                    "Erhebung zur Selbstversorgung (Modul 4) konnte beim Hausbesuch nicht abgeschlossen werden, "
                    "weil der Versicherte sich in einer Off-Phase befand; Nachbesuch vereinbart.",
        "medication": ["Levodopa/Benserazid 100/25 mg 1-1-1-1", "Pramipexol 0,7 mg 1-0-1", "Tamsulosin 0,4 mg 0-0-1"],
        "diary": [("Morgens", "Ankleiden dauert über 30 Minuten, Hilfe bei Knöpfen und Schuhen"),
                  ("Mittags", "Essen mit Besteck mühsam, Getränke in Bechern mit Deckel"),
                  ("Abends", "Hilfe beim Duschen zweimal wöchentlich durch die Ehefrau")],
    },
    {
        "id": "pg-2026-104342", "pack": PFLEGE, "date": "2026-11-10",
        "person": ("Schulte", "Marie", "08.01.1958", "Am Mühlenteich 3, 49074 Osnabrück"),
        "title": "Pflegegrad · Höherstufungsantrag",
        "modules": [15, 4, 2, 48, 8, 14], "expected": "SUPPORTED", "grade": 4,
        "clinic": ("Querschnittzentrum Nordwest", "Rehabilitationsmedizin", "Prof. Dr. med. Henrik Vogt"),
        "diagnoses": [("G82.22", "Paraplegie, komplett, chronisch"), ("N31.9", "Neurogene Blasenentleerungsstörung"),
                      ("L89.24", "Dekubitus Grad 3, Sakralregion, in Abheilung")],
        "findings": "Komplette Querschnittlähmung sub Th6 nach Verkehrsunfall 2025. Rollstuhlmobilisation, "
                    "sämtliche Transfers mit Lifter und zwei Pflegepersonen. Intermittierender Selbstkatheterismus "
                    "nicht möglich, Fremdkatheterisierung fünfmal täglich. Wundversorgung sakral täglich.",
        "medication": ["Baclofen 10 mg 1-1-1", "Enoxaparin 40 mg s.c. 0-0-1", "Macrogol 1 Beutel 1-0-0"],
        "diary": [("Rund um die Uhr", "Lagerungswechsel alle drei Stunden, auch nachts"),
                  ("Morgens", "Ganzkörperpflege im Bett, Ankleiden, Transfer in den Rollstuhl mit Lifter"),
                  ("Mehrmals täglich", "Katheterisierung, Darmmanagement, Wundversorgung")],
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
