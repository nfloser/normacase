package de.normacase.http;

import jakarta.servlet.http.HttpServletRequest;
import java.util.Base64;
import java.util.Collections;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;

/** Shared handling of the single synthetic bearer credential at the local adapter boundary. */
final class BearerRequests {
    private BearerRequests() { }

    /** Returns the credential only when exactly one well-formed bearer header is present. */
    static String credential(HttpServletRequest request) {
        var headers = Collections.list(request.getHeaders("Authorization"));
        if (headers.size() != 1 || !headers.getFirst().startsWith("Bearer ")) return null;
        String credential = headers.getFirst().substring(7);
        return validCredential(credential) ? credential : null;
    }
    private static boolean validCredential(String credential) {
        if (credential.length() != 44) return false;
        try { byte[] decoded = Base64.getDecoder().decode(credential); return decoded.length == 32 && Base64.getEncoder().encodeToString(decoded).equals(credential); }
        catch (IllegalArgumentException ex) { return false; }
    }
    static ResponseEntity<String> unauthorized() {
        return ResponseEntity.status(401).header("WWW-Authenticate", "Bearer").contentType(MediaType.APPLICATION_JSON)
            .body(Messages.UNAUTHORIZED);
    }
    static ResponseEntity<String> error(int status) {
        return ResponseEntity.status(status).contentType(MediaType.APPLICATION_JSON).body(Messages.ERROR);
    }
}
