package de.normacase.assessment.web;

import de.normacase.assessment.InvalidCaseInputException;
import de.normacase.assessment.UnknownReleaseException;
import de.normacase.common.Problems;
import org.springframework.http.HttpStatus;
import org.springframework.http.ProblemDetail;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;

@RestControllerAdvice
class AssessmentExceptionHandler {

    @ExceptionHandler(UnknownReleaseException.class)
    ProblemDetail unknownRelease(UnknownReleaseException e) {
        return Problems.of(HttpStatus.NOT_FOUND, "unknown-knowledge-release", "Regelwerk unbekannt",
                "Das angegebene Knowledge Release ist nicht geladen.");
    }

    @ExceptionHandler(InvalidCaseInputException.class)
    ProblemDetail invalidInput(InvalidCaseInputException e) {
        ProblemDetail problem = Problems.of(HttpStatus.UNPROCESSABLE_CONTENT, "case-does-not-match-knowledge",
                "Fall passt nicht zum Regelwerk", "Felder oder Belege sind im Regelwerk nicht vorgesehen oder haben den falschen Typ.");
        problem.setProperty("fields", e.fields());
        return problem;
    }
}
