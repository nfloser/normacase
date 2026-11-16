package de.normacase.http;

import static org.junit.jupiter.api.Assertions.*;
import org.junit.jupiter.api.Test;
import org.springframework.mock.web.MockFilterChain;
import org.springframework.mock.web.MockHttpServletRequest;
import org.springframework.mock.web.MockHttpServletResponse;

class LocalBoundaryFilterTest {
    private final LocalBoundaryFilter filter = new LocalBoundaryFilter();

    private MockHttpServletRequest valid() {
        var request = new MockHttpServletRequest("GET", "/api/review/work-queues");
        request.setRemoteAddr("127.0.0.1");
        request.addHeader("Host", "127.0.0.1:5081");
        return request;
    }
    private int apply(MockHttpServletRequest request) throws Exception {
        var response = new MockHttpServletResponse();
        filter.doFilter(request, response, new MockFilterChain());
        assertEquals("no-store", response.getHeader("Cache-Control"));
        assertEquals("nosniff", response.getHeader("X-Content-Type-Options"));
        assertNull(response.getHeader("Access-Control-Allow-Origin"));
        return response.getStatus();
    }
    @Test void permitsOnlyTheLocalReadPath() throws Exception {
        assertEquals(200, apply(valid()));
        var nonlocal = valid(); nonlocal.setRemoteAddr("192.0.2.1"); assertEquals(403, apply(nonlocal));
        var forgedHost = valid(); forgedHost.removeHeader("Host"); forgedHost.addHeader("Host", "evil.example:5081"); assertEquals(403, apply(forgedHost));
        var wrongPort = valid(); wrongPort.removeHeader("Host"); wrongPort.addHeader("Host", "localhost:5082"); assertEquals(403, apply(wrongPort));
        var externalOrigin = valid(); externalOrigin.addHeader("Origin", "https://evil.example"); assertEquals(403, apply(externalOrigin));
        var forwarded = valid(); forwarded.addHeader("X-Forwarded-For", "127.0.0.1"); assertEquals(403, apply(forwarded));
        var multipleHosts = valid(); multipleHosts.addHeader("Host", "127.0.0.1:5081"); assertEquals(403, apply(multipleHosts));
        var wrongPath = valid(); wrongPath.setRequestURI("/actuator/health"); assertEquals(404, apply(wrongPath));
        var write = valid(); write.setMethod("POST"); assertEquals(405, apply(write));
        var body = valid(); body.setContent("content".getBytes()); assertEquals(400, apply(body));
    }
    @Test void permitsTheSameOriginButNotADifferentLocalOrigin() throws Exception {
        var same = valid(); same.addHeader("Origin", "http://127.0.0.1:5081"); assertEquals(200, apply(same));
        var different = valid(); different.addHeader("Origin", "http://localhost:5081"); assertEquals(403, apply(different));
        var duplicates = valid(); duplicates.addHeader("Origin", "http://127.0.0.1:5081"); duplicates.addHeader("Origin", "http://127.0.0.1:5081"); assertEquals(403, apply(duplicates));
    }
}
