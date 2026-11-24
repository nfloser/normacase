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
