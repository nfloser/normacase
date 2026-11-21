package de.normacase.system;

import org.springframework.beans.factory.ObjectProvider;
import org.springframework.boot.info.BuildProperties;
import org.springframework.http.CacheControl;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/v1/system")
class SystemController {

    private final String platformVersion;

    SystemController(ObjectProvider<BuildProperties> buildProperties) {
        BuildProperties build = buildProperties.getIfAvailable();
        this.platformVersion = build != null ? build.getVersion() : "development";
    }

    @GetMapping
    ResponseEntity<SystemInfo> info() {
        return ResponseEntity.ok()
                .cacheControl(CacheControl.noStore())
                .body(new SystemInfo("NormaCase", platformVersion, "zero-retention"));
    }

    /** Technische Auskunft; enthält nie Fall- oder Personenbezug. */
    record SystemInfo(String product, String platformVersion, String retentionMode) {
    }
}
