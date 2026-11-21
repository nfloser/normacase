package de.normacase.identity;

import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import de.normacase.TestcontainersConfiguration;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.test.web.servlet.MockMvc;

@SpringBootTest
@AutoConfigureMockMvc
@Import(TestcontainersConfiguration.class)
class AuthenticationIntegrationTest {

    @Autowired
    MockMvc mvc;

    @Test
    void apiRequiresAuthenticationAndAnswersWithProblemDetails() throws Exception {
        mvc.perform(get("/api/v1/me"))
                .andExpect(status().isUnauthorized())
                .andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_PROBLEM_JSON))
                .andExpect(jsonPath("$.status").value(401))
                .andExpect(jsonPath("$.detail").value("Anmeldung erforderlich."));
    }

    @Test
    void invalidBearerTokenIsRejected() throws Exception {
        mvc.perform(get("/api/v1/me").header("Authorization", "Bearer kein-gueltiges-token"))
                .andExpect(status().isUnauthorized());
    }

    @Test
    void meReturnsNameAndRolesFromTheToken() throws Exception {
        mvc.perform(get("/api/v1/me").with(jwt()
                        .jwt(j -> j.claim("preferred_username", "gutachterin.a").claim("name", "Gutachterin A"))
                        .authorities(new SimpleGrantedAuthority("ROLE_gutachter"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.username").value("gutachterin.a"))
                .andExpect(jsonPath("$.displayName").value("Gutachterin A"))
                .andExpect(jsonPath("$.roles[0]").value("gutachter"));
    }

    @Test
    void healthAndSystemStayPublic() throws Exception {
        mvc.perform(get("/actuator/health")).andExpect(status().isOk());
        mvc.perform(get("/api/v1/system")).andExpect(status().isOk());
    }
}
