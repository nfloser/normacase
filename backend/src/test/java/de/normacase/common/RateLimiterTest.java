package de.normacase.common;

import static org.assertj.core.api.Assertions.assertThat;

import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneOffset;
import org.junit.jupiter.api.Test;

class RateLimiterTest {

    static class MovableClock extends Clock {
        Instant now = Instant.parse("2026-11-21T10:00:00Z");

        @Override public ZoneOffset getZone() { return ZoneOffset.UTC; }
        @Override public Clock withZone(java.time.ZoneId zone) { return this; }
        @Override public Instant instant() { return now; }
    }

    @Test
    void allowsTheBurstThenAsksToWaitAndRefillsOverTime() {
        MovableClock clock = new MovableClock();
        RateLimiter limiter = new RateLimiter(60, clock);
        for (int i = 0; i < 60; i++) {
            assertThat(limiter.acquire("host-a")).isZero();
        }
        Duration wait = limiter.acquire("host-a");
        assertThat(wait).isPositive().isLessThanOrEqualTo(Duration.ofSeconds(1));
        assertThat(limiter.acquire("host-b")).isZero();

        clock.now = clock.now.plusSeconds(1);
        assertThat(limiter.acquire("host-a")).isZero();
        assertThat(limiter.acquire("host-a")).isPositive();
    }
}
