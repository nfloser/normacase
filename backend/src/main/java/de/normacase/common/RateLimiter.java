package de.normacase.common;

import java.time.Clock;
import java.time.Duration;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Token-Bucket je Schlüssel (z. B. je technischem Konto). Bewusst ohne Fremdbibliothek, damit Verhalten und
 * Grenzen im Test exakt prüfbar sind. Im Cluster gilt die Grenze je Instanz; der Ingress begrenzt zusätzlich.
 */
public final class RateLimiter {

    private final long capacity;
    private final double refillPerNano;
    private final Clock clock;
    private final ConcurrentHashMap<String, Bucket> buckets = new ConcurrentHashMap<>();

    public RateLimiter(long requestsPerMinute, Clock clock) {
        this.capacity = Math.max(1, requestsPerMinute);
        this.refillPerNano = capacity / (double) Duration.ofMinutes(1).toNanos();
        this.clock = clock;
    }

    /** {@code Duration.ZERO} wenn erlaubt, sonst die Wartezeit bis zum nächsten freien Platz. */
    public Duration acquire(String key) {
        long now = clock.instant().getEpochSecond() * 1_000_000_000L + clock.instant().getNano();
        Bucket bucket = buckets.computeIfAbsent(key, k -> new Bucket(capacity, now));
        synchronized (bucket) {
            bucket.tokens = Math.min(capacity, bucket.tokens + (now - bucket.updated) * refillPerNano);
            bucket.updated = now;
            if (bucket.tokens >= 1) {
                bucket.tokens -= 1;
                return Duration.ZERO;
            }
            return Duration.ofNanos((long) Math.ceil((1 - bucket.tokens) / refillPerNano));
        }
    }

    private static final class Bucket {
        double tokens;
        long updated;

        Bucket(double tokens, long updated) {
            this.tokens = tokens;
            this.updated = updated;
        }
    }
}
