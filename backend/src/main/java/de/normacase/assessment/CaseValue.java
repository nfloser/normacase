package de.normacase.assessment;

import de.normacase.knowledge.TruthValue;
import java.math.BigDecimal;
import java.util.Objects;

/** Wert eines Falls. {@link Unknown} ist ausdrücklich und wird nie zu ja oder nein umgedeutet. */
public sealed interface CaseValue {

    Unknown UNKNOWN = new Unknown();

    static CaseValue truth(TruthValue value) {
        return new Truth(Objects.requireNonNull(value));
    }

    static CaseValue number(BigDecimal value) {
        return new Number(Objects.requireNonNull(value));
    }

    default boolean isUnknown() {
        return this instanceof Unknown;
    }

    record Unknown() implements CaseValue {
    }

    record Truth(TruthValue value) implements CaseValue {
    }

    record Number(BigDecimal value) implements CaseValue {
    }
}
