package de.normacase.assessment;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import de.normacase.assessment.AssessmentResult.NumericTrace;
import de.normacase.assessment.AssessmentResult.OutputTrace;
import de.normacase.knowledge.AssessmentOutcome;
import de.normacase.knowledge.KnowledgePack;
import de.normacase.knowledge.KnowledgePackReader;
import de.normacase.knowledge.KnowledgeValidationException;
import de.normacase.knowledge.TruthValue;
import java.io.IOException;
import java.math.BigDecimal;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.LocalDate;
import java.util.HashMap;
import java.util.Map;
import java.util.Optional;
import org.junit.jupiter.api.Test;

/**
 * Pflegebegutachtung nach den BRi vom 26.08.2026: Einzelkriterien, Gewichtung, Schwellen, Modul 5 mit Häufigkeiten,
 * besondere Bedarfskonstellation, Kinder und Säuglinge. Erwartete Werte stehen in BRi 5.10.1, 6.5 und 6.6.
 */
class PflegeRegelwerk2026Test {

    static final Path DIR = Path.of("../knowledge/public-reference");
    static final KnowledgePack ADULT = read("pflege-2026-erwachsene");
    static final KnowledgePack CHILD = read("pflege-2026-kinder");
    static final KnowledgePack INFANT = read("pflege-2026-saeuglinge");
    final RuleEngine engine = new RuleEngine();

    static KnowledgePack read(String folder) {
        try {
            return new KnowledgePackReader().read(Files.readString(DIR.resolve(folder).resolve("pack.json")));
        } catch (IOException e) {
            throw new IllegalStateException(e);
        }
    }

    /** Alle Kriterien 0, besondere Bedarfskonstellation nein; einzelne Werte überschreibbar. */
    AssessmentResult assess(KnowledgePack pack, Map<String, Object> values) {
        Map<String, CaseValue> facts = new HashMap<>();
        pack.fields().forEach(f -> facts.put(f.id(), "truth".equals(f.type())
                ? CaseValue.truth(TruthValue.NO) : CaseValue.number(BigDecimal.ZERO)));
        if (pack == CHILD) facts.put("alter_monate", CaseValue.number(new BigDecimal("132")));
        values.forEach((k, v) -> facts.put(k, v instanceof TruthValue t ? CaseValue.truth(t)
                : v == null ? CaseValue.UNKNOWN : CaseValue.number(new BigDecimal(v.toString()))));
        return engine.evaluate(pack, new CaseInput(LocalDate.parse("2026-11-04"), facts, Map.of()));
    }

    static OutputTrace output(AssessmentResult r, String id) {
        return r.domainOutputs().stream().filter(o -> o.outputId().equals(id)).findFirst().orElseThrow();
    }

    static String grade(AssessmentResult r) {
        return output(r, "pflegegrad_nach_punkten").value();
    }

    static BigDecimal value(AssessmentResult r, String id) {
        NumericTrace root = output(r, "pflegegrad_nach_punkten").condition().numericExpression();
        return find(root, id).map(t -> ((CaseValue.Number) t.value()).value()).orElseThrow();
    }

    static Optional<NumericTrace> find(NumericTrace t, String id) {
        if (id.equals(t.id())) return Optional.of(t);
        return t.children().stream().map(c -> find(c, id)).flatMap(Optional::stream).findFirst();
    }

    @Test
    void ohneEinschraenkungenKeinPflegegrad() {
        var r = assess(ADULT, Map.of());
        assertThat(r.outcome()).isEqualTo(AssessmentOutcome.NOT_SUPPORTED);
        assertThat(grade(r)).isEqualTo("0");
        assertThat(output(r, "pflegegrad_5_besondere_bedarfskonstellation").value()).isEqualTo("NOT_REACHED");
    }

    @Test
    void schwellenDerPflegegradeGenauAnDerGrenze() {
        // 12,5: Modul 1 mit 2 Einzelpunkten (2,5) + Modul 4 mit 3 Einzelpunkten (10)
        var pg1 = assess(ADULT, Map.of("kf_1_1", 2, "kf_4_1", 3));
        assertThat(value(pg1, "gesamtpunkte")).isEqualByComparingTo("12.5");
        assertThat(grade(pg1)).isEqualTo("1");
        assertThat(pg1.outcome()).isEqualTo(AssessmentOutcome.SUPPORTED);
        assertThat(grade(assess(ADULT, Map.of("kf_4_1", 3)))).isEqualTo("0"); // 10

        // 30: Essen 3×3 + Trinken 3×2 + Toilette 3×2 + Sonde täglich zusätzlich 6 = 27 Einzelpunkte → 30
        var pg2 = assess(ADULT, Map.of("kf_4_8", 3, "kf_4_9", 3, "kf_4_10", 3, "kf_4_13", 1));
        assertThat(value(pg2, "modul_4_einzelpunkte")).isEqualByComparingTo("27");
        assertThat(grade(pg2)).isEqualTo("2");

        // 47,5 = 30 + Modul 5 (Medikation 3×/Tag + Injektion 7×/Woche = 4/Tag → 2 Punkte → 10) + Modul 1 (6 → 7,5)
        var pg3 = assess(ADULT, Map.of("kf_4_8", 3, "kf_4_9", 3, "kf_4_10", 3, "kf_4_13", 1,
                "kf_5_1_tag", 3, "kf_5_2_woche", 7, "kf_1_1", 3, "kf_1_2", 3));
        assertThat(value(pg3, "gesamtpunkte")).isEqualByComparingTo("47.5");
        assertThat(grade(pg3)).isEqualTo("3");

        Map<String, Object> severe = new HashMap<>(Map.of("kf_5_12_tag", 1));
        for (String c : new String[] {"1_1", "1_2", "1_3", "1_4", "1_5", "4_1", "4_2", "4_3", "4_4", "4_5", "4_6", "4_7",
                "4_8", "4_9", "4_10"}) {
            severe.put("kf_" + c, 3);
        }
        var pg4 = assess(ADULT, severe); // 10 + 40 + 20 = 70
        assertThat(value(pg4, "gesamtpunkte")).isEqualByComparingTo("70");
        assertThat(grade(pg4)).isEqualTo("4");

        for (int i = 1; i <= 11; i++) severe.put("kf_2_" + i, 3); // Modul 2: 33 → 15
        severe.put("kf_6_1", 1); // 3,75 → 88,75
        assertThat(grade(assess(ADULT, severe))).isEqualTo("4");
        severe.put("kf_6_2", 3); // 4 Einzelpunkte → 7,5 → 92,5
        var pg5 = assess(ADULT, severe);
        assertThat(value(pg5, "gesamtpunkte")).isEqualByComparingTo("92.5");
        assertThat(grade(pg5)).isEqualTo("5");
    }

    @Test
    void vonModul2Und3ZaehltDerHoehereWert() {
        var r = assess(ADULT, Map.of("kf_2_1", 3, "kf_2_2", 3, "kf_2_3", 3, "kf_2_4", 2, "kf_3_1", 5, "kf_3_2", 3));
        assertThat(value(r, "modul_2")).isEqualByComparingTo("11.25"); // 11 Einzelpunkte
        assertThat(value(r, "modul_3")).isEqualByComparingTo("15"); // 8 Einzelpunkte
        assertThat(value(r, "modul_2_3")).isEqualByComparingTo("15");
    }

    @Test
    void ernaehrungUeberSondeZaehltAusschliesslichNiedriger() {
        assertThat(value(assess(ADULT, Map.of("kf_4_13", 1)), "modul_4_einzelpunkte")).isEqualByComparingTo("6");
        assertThat(value(assess(ADULT, Map.of("kf_4_13", 2)), "modul_4_einzelpunkte")).isEqualByComparingTo("3");
    }

    @Test
    void modul5RechnetHaeufigkeitenOhneZwischenrundung() {
        assertThat(value(assess(ADULT, Map.of("kf_5_1_tag", 3)), "modul_5_a")).isEqualByComparingTo("1");
        assertThat(value(assess(ADULT, Map.of("kf_5_1_tag", 3, "kf_5_1_monat", 1)), "modul_5_a")).isEqualByComparingTo("2");
        assertThat(value(assess(ADULT, Map.of("kf_5_1_woche", 6)), "modul_5_a")).isEqualByComparingTo("0"); // 0,8571
        // Sieben getrennte Wochenwerte ergeben genau einen zusätzlichen Vorgang pro Tag.
        var weekly = new HashMap<String, Object>();
        for (int i = 1; i <= 7; i++) weekly.put("kf_5_" + i + "_woche", 1);
        weekly.put("kf_5_1_tag", 2);
        assertThat(value(assess(ADULT, weekly), "modul_5_a")).isEqualByComparingTo("1");
        assertThat(value(assess(ADULT, Map.of("kf_5_8_woche", 1)), "modul_5_b")).isEqualByComparingTo("1"); // 0,1429
        assertThat(value(assess(ADULT, Map.of("kf_5_8_monat", 4)), "modul_5_b")).isEqualByComparingTo("0"); // 0,1333
        assertThat(value(assess(ADULT, Map.of("kf_5_8_tag", 3)), "modul_5_b")).isEqualByComparingTo("3");
        assertThat(value(assess(ADULT, Map.of("kf_5_13_woche", 1)), "modul_5_c")).isEqualByComparingTo("1"); // 4,3
        assertThat(value(assess(ADULT, Map.of("kf_5_13_monat", 4)), "modul_5_c")).isEqualByComparingTo("0"); // 4
        assertThat(value(assess(ADULT, Map.of("kf_5_15_woche", 1, "kf_5_13_woche", 1)), "modul_5_c")).isEqualByComparingTo("3");
        var daily = assess(ADULT, Map.of("kf_5_12_tag", 1));
        assertThat(value(daily, "modul_5_c")).isEqualByComparingTo("6");
        assertThat(value(daily, "modul_5")).isEqualByComparingTo("20");
    }

    @Test
    void besondereBedarfskonstellationBegruendetPflegegrad5UnabhaengigVonPunkten() {
        var r = assess(ADULT, Map.of("kf_1_b", TruthValue.YES));
        assertThat(r.outcome()).isEqualTo(AssessmentOutcome.SUPPORTED);
        assertThat(grade(r)).isEqualTo("0"); // Punkte bleiben sichtbar, die Zuordnung kommt aus F 4.1.B
        assertThat(output(r, "pflegegrad_5_besondere_bedarfskonstellation").value()).isEqualTo("REACHED");
    }

    @Test
    void unzulaessigeWerteWerdenNichtGeraten() {
        var r = assess(ADULT, Map.of("kf_3_1", 2)); // Modul 3 kennt nur 0, 1, 3, 5
        assertThat(r.outcome()).isEqualTo(AssessmentOutcome.HUMAN_REVIEW);
        assertThat(grade(r)).isNull();
        assertThat(assess(ADULT, Map.of("kf_4_1", 4)).outcome()).isEqualTo(AssessmentOutcome.HUMAN_REVIEW);
        assertThat(assess(ADULT, Map.of("kf_4_1", "1.5")).outcome()).isEqualTo(AssessmentOutcome.HUMAN_REVIEW);
    }

    @Test
    void fehlendesKriteriumMachtDenFallUnvollstaendig() {
        Map<String, Object> missing = new HashMap<>();
        missing.put("kf_4_4", null);
        var r = assess(ADULT, missing);
        assertThat(r.outcome()).isEqualTo(AssessmentOutcome.INCOMPLETE);
        assertThat(r.missingRequiredFields()).containsExactly("kf_4_4");
    }

    @Test
    void kinderZaehlenFruehfoerderungMit() {
        assertThat(ADULT.fields()).noneMatch(f -> f.id().startsWith("kf_5_k"));
        assertThat(value(assess(CHILD, Map.of("kf_5_k_woche", 1)), "modul_5_c")).isEqualByComparingTo("1");
        assertThat(grade(assess(CHILD, Map.of("kf_1_1", 2, "kf_4_1", 3)))).isEqualTo("1");
    }

    @Test
    void kinderWertenAltersvergleichUndMehrfachpunkteAus() {
        // KF 4.4.8 ist ab 20 Monaten überwiegend selbständig, ab 30 Monaten selbständig.
        var younger = assess(CHILD, Map.of("alter_monate", 19, "kf_4_8", 3, "kf_4_11", 3));
        assertThat(value(younger, "modul_4_einzelpunkte")).isEqualByComparingTo("3");
        var example = assess(CHILD, Map.of("alter_monate", 20, "kf_4_8", 3));
        assertThat(value(example, "modul_4_einzelpunkte")).isEqualByComparingTo("6");
        var older = assess(CHILD, Map.of("alter_monate", 30, "kf_4_8", 3));
        assertThat(value(older, "modul_4_einzelpunkte")).isEqualByComparingTo("9");
        // Ab 5 Jahren ist KF 4.4.11 bei gegebenen Voraussetzungen bewertbar.
        assertThat(value(assess(CHILD, Map.of("alter_monate", 60, "kf_4_11", 3)), "modul_4_einzelpunkte"))
                .isEqualByComparingTo("3");
        assertThat(assess(CHILD, Map.of("alter_monate", 17)).outcome()).isEqualTo(AssessmentOutcome.HUMAN_REVIEW);
        assertThat(assess(CHILD, Map.of("alter_monate", 216)).outcome()).isEqualTo(AssessmentOutcome.HUMAN_REVIEW);
    }

    @Test
    void saeuglingeWerdenEineStufeHoeherEingestuft() {
        assertThat(INFANT.fields()).noneMatch(f -> f.id().startsWith("kf_1_1") || f.id().startsWith("kf_2_") || f.id().startsWith("kf_6_"));
        // KF 4.4.0: 20 Einzelpunkte → 30; Modul 3 mit 10 Einzelpunkten → 15; 45 → Pflegegrad 3
        var r = assess(INFANT, Map.of("kf_4_0", 1, "kf_3_1", 5, "kf_3_2", 5));
        assertThat(value(r, "gesamtpunkte")).isEqualByComparingTo("45");
        assertThat(grade(r)).isEqualTo("3");
        // 12,5 = Modul 3 (3 Einzelpunkte → 7,5) + Modul 5 (1 Punkt → 5): bei Erwachsenen Pflegegrad 1, hier 2
        var low = assess(INFANT, Map.of("kf_3_1", 3, "kf_5_1_tag", 1));
        assertThat(value(low, "gesamtpunkte")).isEqualByComparingTo("12.5");
        assertThat(grade(low)).isEqualTo("2");
        assertThat(grade(assess(INFANT, Map.of()))).isEqualTo("0");
        var special = assess(INFANT, Map.of("kf_1_b", TruthValue.YES));
        assertThat(output(special, "pflegegrad_5_besondere_bedarfskonstellation").value()).isEqualTo("REACHED");
    }

    @Test
    void zahlenausgabenUndSkalierungWerdenStrengGeprueft() {
        String base = """
                {"manifest": {"formatVersion": 1, "packId": "p", "releaseId": "r-1", "lifecycleStatus": "ACTIVE",
                  "validationLevel": "SYNTHETIC", "entryRuleId": "R"},
                 "fields": [{"id": "n", "type": "number", "required": true}],
                 "sources": [{"id": "S", "authority": "Test", "title": "T", "documentType": "SYNTHETIC", "status": "ACTIVE"}],
                 "outputs": [OUT],
                 "rules": [{"id": "R", "version": 1, "validFrom": "2026-01-01", "validUntil": null, "sourceId": "S",
                   "condition": {"kind": "number_gte", "threshold": 1, "numericExpression": EXPR},
                   "onMatch": "SUPPORTED", "onNoMatch": "NOT_SUPPORTED", "onUnknown": "HUMAN_REVIEW"}]}""";
        String out = """
                {"id": "o", "version": 1, "validFrom": "2026-01-01", "validUntil": null, "sourceId": "S", "type": "number",
                 "choices": ["0", "1"], "numericExpression": {"kind": "field", "field": "n"}}""";
        String divide = "{\"kind\": \"scale\", \"divisor\": 3, \"decimals\": 4, \"input\": {\"kind\": \"field\", \"field\": \"n\"}}";
        KnowledgePack pack = new KnowledgePackReader().read(base.replace("OUT", out).replace("EXPR", divide));
        var r = engine.evaluate(pack, new CaseInput(LocalDate.parse("2026-05-01"), Map.of("n", CaseValue.number(BigDecimal.ONE)), Map.of()));
        assertThat(((CaseValue.Number) r.ruleTrace().condition().numericExpression().value()).value()).isEqualByComparingTo("0.3333");
        assertThat(r.domainOutputs().getFirst().value()).isEqualTo("1");
        var two = engine.evaluate(pack, new CaseInput(LocalDate.parse("2026-05-01"), Map.of("n", CaseValue.number(BigDecimal.TWO)), Map.of()));
        assertThat(two.domainOutputs().getFirst().value()).isNull(); // 2 ist kein deklarierter Wert

        assertThatThrownBy(() -> new KnowledgePackReader().read(base.replace("OUT", out)
                .replace("EXPR", "{\"kind\": \"scale\", \"divisor\": 3, \"input\": {\"kind\": \"field\", \"field\": \"n\"}}")))
                .isInstanceOfSatisfying(KnowledgeValidationException.class,
                        e -> assertThat(e.errors()).anyMatch(x -> x.code().equals("invalid_scale")));
        assertThatThrownBy(() -> new KnowledgePackReader().read(base.replace("OUT", out.replace("\"1\"]", "\"eins\"]")).replace("EXPR", divide)))
                .isInstanceOfSatisfying(KnowledgeValidationException.class,
                        e -> assertThat(e.errors()).anyMatch(x -> x.code().equals("invalid_number_output_choice")));
    }
}
