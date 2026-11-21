package de.normacase.common;

import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.net.URI;
import java.time.Clock;
import java.time.Duration;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Configuration;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.http.ProblemDetail;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.web.servlet.HandlerInterceptor;
import org.springframework.web.servlet.config.annotation.InterceptorRegistry;
import org.springframework.web.servlet.config.annotation.WebMvcConfigurer;
import tools.jackson.databind.json.JsonMapper;

/** Begrenzt schreibende, teure Aufrufe je Konto; Überschreitung ergibt 429 mit Retry-After. */
@Configuration(proxyBeanMethods = false)
class RateLimitConfiguration implements WebMvcConfigurer {

    private final RateLimiter limiter;
    private final JsonMapper json;

    RateLimitConfiguration(@Value("${normacase.rate-limit.per-minute:120}") long perMinute, Clock clock, JsonMapper json) {
        this.limiter = new RateLimiter(perMinute, clock);
        this.json = json;
    }

    @Override
    public void addInterceptors(InterceptorRegistry registry) {
        registry.addInterceptor(new HandlerInterceptor() {
            @Override
            public boolean preHandle(HttpServletRequest request, HttpServletResponse response, Object handler) throws Exception {
                if (!"POST".equals(request.getMethod())) {
                    return true;
                }
                Authentication auth = SecurityContextHolder.getContext().getAuthentication();
                String key = auth == null ? "anonymous" : auth.getName();
                Duration wait = limiter.acquire(key);
                if (wait.isZero()) {
                    return true;
                }
                ProblemDetail problem = ProblemDetail.forStatusAndDetail(HttpStatus.TOO_MANY_REQUESTS,
                        "Zu viele Anfragen. Bitte später erneut versuchen.");
                problem.setType(URI.create(Problems.BASE + "rate-limited"));
                problem.setTitle("Anfragegrenze erreicht");
                response.setStatus(HttpStatus.TOO_MANY_REQUESTS.value());
                response.setHeader("Retry-After", String.valueOf(Math.max(1, (wait.toMillis() + 999) / 1000)));
                response.setContentType(MediaType.APPLICATION_PROBLEM_JSON_VALUE);
                json.writeValue(response.getOutputStream(), problem);
                return false;
            }
        }).addPathPatterns("/api/v1/intake", "/api/v1/assessments", "/api/v1/evidence/*/verification");
    }
}
