package de.normacase.assessment;

import java.time.LocalDate;
import java.util.Map;
import java.util.TreeMap;

/** Strukturierte, normalisierte Eingabe einer Bewertung. Der Stichtag kommt aus dem Fall, nie aus der Uhr. */
public record CaseInput(LocalDate assessmentDate, Map<String, CaseValue> facts, Map<String, EvidenceStatus> evidence) {

    public CaseInput {
        facts = java.util.Collections.unmodifiableMap(new TreeMap<>(facts));
        evidence = java.util.Collections.unmodifiableMap(new TreeMap<>(evidence));
    }
}
