package de.normacase.workflow.web;

import de.normacase.common.Problems;
import de.normacase.documents.InvalidDocumentContentException;
import de.normacase.workflow.WorkflowException;
import org.springframework.http.HttpStatus;
import org.springframework.http.ProblemDetail;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;

@RestControllerAdvice
class WorkflowExceptionHandler {

    @ExceptionHandler(WorkflowException.class)
    ProblemDetail workflow(WorkflowException e) {
        return switch (e.kind()) {
            case NOT_FOUND -> Problems.of(HttpStatus.NOT_FOUND, "not-found", "Nicht gefunden", "Vorgang nicht gefunden.");
            case CONFLICT -> Problems.of(HttpStatus.CONFLICT, "invalid-state", "Aktion nicht möglich", e.getMessage() + ".");
            case FOUR_EYES -> Problems.of(HttpStatus.CONFLICT, "four-eyes", "Vier-Augen-Prinzip",
                    "Wer bestätigt hat, darf nicht selbst freigeben.");
        };
    }

    @ExceptionHandler(InvalidDocumentContentException.class)
    ProblemDetail document(InvalidDocumentContentException e) {
        ProblemDetail problem = Problems.of(HttpStatus.UNPROCESSABLE_CONTENT, "invalid-document-content",
                "Dokument abgelehnt", e.getMessage() + ".");
        problem.setProperty("documentId", e.documentId());
        return problem;
    }
}
