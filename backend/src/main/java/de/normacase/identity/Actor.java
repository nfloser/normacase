package de.normacase.identity;

import org.springframework.security.core.Authentication;
import org.springframework.security.oauth2.server.resource.authentication.JwtAuthenticationToken;

/** Handelnde Person oder handelndes Fachsystem, nur mit technischer Kennung. */
public record Actor(String id, Kind kind) {

    public enum Kind { PERSON, SYSTEM }

    public static Actor from(Authentication authentication) {
        String id = authentication.getName();
        if (authentication instanceof JwtAuthenticationToken jwt) {
            String username = jwt.getToken().getClaimAsString("preferred_username");
            if (username != null) {
                id = username;
            }
        }
        boolean system = authentication.getAuthorities().stream()
                .anyMatch(a -> a.getAuthority().equals("ROLE_" + Roles.INTEGRATION));
        return new Actor(id, system ? Kind.SYSTEM : Kind.PERSON);
    }

    public boolean hasRole(Authentication authentication, String role) {
        return authentication.getAuthorities().stream().anyMatch(a -> a.getAuthority().equals("ROLE_" + role));
    }
}
