package de.normacase.identity;

import java.util.List;
import org.springframework.http.CacheControl;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.GrantedAuthority;
import org.springframework.security.oauth2.server.resource.authentication.JwtAuthenticationToken;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
class MeController {

    @GetMapping("/api/v1/me")
    ResponseEntity<Me> me(JwtAuthenticationToken authentication) {
        String username = authentication.getToken().getClaimAsString("preferred_username");
        if (username == null) {
            username = authentication.getName();
        }
        String displayName = authentication.getToken().getClaimAsString("name");
        List<String> roles = authentication.getAuthorities().stream()
                .map(GrantedAuthority::getAuthority)
                .filter(a -> a.startsWith("ROLE_"))
                .map(a -> a.substring("ROLE_".length()))
                .sorted()
                .toList();
        return ResponseEntity.ok()
                .cacheControl(CacheControl.noStore())
                .body(new Me(username, displayName != null ? displayName : username, roles));
    }

    record Me(String username, String displayName, List<String> roles) {
    }
}
