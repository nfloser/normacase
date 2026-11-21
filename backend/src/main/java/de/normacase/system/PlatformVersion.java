package de.normacase.system;

import org.springframework.beans.factory.ObjectProvider;
import org.springframework.boot.info.BuildProperties;
import org.springframework.stereotype.Component;

/** Plattformversion; getrennt von der Version des Regelwerks. */
@Component
public class PlatformVersion {

    private final String value;

    PlatformVersion(ObjectProvider<BuildProperties> build) {
        BuildProperties properties = build.getIfAvailable();
        this.value = properties != null ? properties.getVersion() : "development";
    }

    public String value() {
        return value;
    }
}
