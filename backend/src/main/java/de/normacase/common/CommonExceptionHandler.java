package de.normacase.common;

import org.springframework.http.HttpStatus;
import org.springframework.http.ProblemDetail;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;

@RestControllerAdvice
class CommonExceptionHandler {

    @ExceptionHandler(InvalidDocumentException.class)
    ProblemDetail invalidDocument(InvalidDocumentException e) {
        ProblemDetail problem = Problems.of(HttpStatus.BAD_REQUEST, "invalid-document", "Ungültiges Dokument",
                "Das Dokument entspricht nicht dem Austauschformat.");
        problem.setProperty("path", e.path());
        problem.setProperty("reason", e.getMessage());
        return problem;
    }
}
