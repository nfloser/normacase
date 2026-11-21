package de.normacase.system;

import static org.assertj.core.api.Assertions.assertThat;

import de.normacase.TestcontainersConfiguration;
import java.io.InputStream;
import java.util.Map;
import java.util.Set;
import java.util.TreeSet;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.annotation.Import;
import org.springframework.web.servlet.mvc.method.annotation.RequestMappingHandlerMapping;
import org.yaml.snakeyaml.Yaml;

/** ADR 0004: Der eingecheckte Vertrag und die tatsächlichen Endpunkte stimmen überein. */
@SpringBootTest
@Import(TestcontainersConfiguration.class)
class OpenApiContractTest {

    @Autowired
    @Qualifier("requestMappingHandlerMapping")
    RequestMappingHandlerMapping mappings;

    @Test
    @SuppressWarnings("unchecked")
    void everyEndpointIsDocumentedAndEveryDocumentedPathExists() throws Exception {
        Set<String> documented = new TreeSet<>();
        try (InputStream in = getClass().getResourceAsStream("/static/api/v1/openapi.yaml")) {
            Map<String, Object> spec = new Yaml().load(in);
            ((Map<String, Map<String, Object>>) spec.get("paths")).forEach((path, operations) ->
                    operations.keySet().forEach(method -> documented.add(method.toUpperCase() + " " + path)));
        }
        Set<String> implemented = new TreeSet<>();
        mappings.getHandlerMethods().keySet().forEach(info -> info.getPatternValues().stream()
                .filter(p -> p.startsWith("/api/"))
                .forEach(p -> info.getMethodsCondition().getMethods()
                        .forEach(m -> implemented.add(m.name() + " " + p))));

        assertThat(implemented).isEqualTo(documented);
    }
}
