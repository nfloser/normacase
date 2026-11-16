package de.normacase.http;

import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.util.Collections;
import java.util.Locale;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

/** The synthetic adapter is a same-origin, loopback-only, read-only surface. */
@Component
public final class LocalBoundaryFilter extends OncePerRequestFilter {
    @Override protected void doFilterInternal(HttpServletRequest request, HttpServletResponse response, FilterChain chain)
            throws ServletException, IOException {
        response.setHeader("Cache-Control", "no-store");
        response.setHeader("X-Content-Type-Options", "nosniff");
        response.setHeader("Referrer-Policy", "no-referrer");
        response.setHeader("X-Frame-Options", "DENY");
        response.setHeader("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'; base-uri 'none'");
        var hosts = Collections.list(request.getHeaders("Host"));
        var origins = Collections.list(request.getHeaders("Origin"));
        boolean forwarded = Collections.list(request.getHeaderNames()).stream().map(name -> name.toLowerCase(Locale.ROOT))
            .anyMatch(name -> name.equals("forwarded") || name.startsWith("x-forwarded-"));
        if (!"127.0.0.1".equals(request.getRemoteAddr()) || hosts.size() != 1
            || !(hosts.getFirst().equalsIgnoreCase("127.0.0.1:5081") || hosts.getFirst().equalsIgnoreCase("localhost:5081"))
            || origins.size() > 1 || (!origins.isEmpty() && !origins.getFirst().equalsIgnoreCase("http://" + hosts.getFirst())) || forwarded) {
            reject(response, 403); return;
        }
        if (!"/api/review/work-queues".equals(request.getRequestURI())) { reject(response, 404); return; }
        if (!"GET".equals(request.getMethod())) { response.setHeader("Allow", "GET"); reject(response, 405); return; }
        if (request.getContentLengthLong() != -1 && request.getContentLengthLong() != 0
            || request.getHeader("Transfer-Encoding") != null) { reject(response, 400); return; }
        chain.doFilter(request, response);
    }
    private static void reject(HttpServletResponse response, int status) throws IOException {
        response.setStatus(status);
        response.setContentType("application/json;charset=UTF-8");
        response.getWriter().write(Messages.ERROR);
    }
}
