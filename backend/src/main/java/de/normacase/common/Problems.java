package de.normacase.common;

import java.net.URI;
import org.springframework.http.HttpStatus;
import org.springframework.http.ProblemDetail;

/** RFC 9457 Problem Details mit stabilen Typen. Meldungen enthalten nie Fallinhalte. */
public final class Problems {

    public static final String BASE = "https://normacase.local/problems/";

    private Problems() {
    }

    public static ProblemDetail of(HttpStatus status, String type, String title, String detail) {
        ProblemDetail problem = ProblemDetail.forStatusAndDetail(status, detail);
        problem.setType(URI.create(BASE + type));
        problem.setTitle(title);
        return problem;
    }
}
