package de.normacase.http;

import java.net.InetAddress;
import org.springframework.boot.web.server.WebServerFactoryCustomizer;
import org.springframework.boot.web.servlet.server.ConfigurableServletWebServerFactory;
import org.springframework.core.Ordered;
import org.springframework.stereotype.Component;

/** Apply after Spring server properties so configuration cannot widen this pilot. */
@Component
final class LoopbackBinding implements WebServerFactoryCustomizer<ConfigurableServletWebServerFactory>, Ordered {
    @Override public int getOrder() { return Ordered.LOWEST_PRECEDENCE; }
    private static InetAddress ipv4Loopback() {
        try { return InetAddress.getByName("127.0.0.1"); }
        catch (java.net.UnknownHostException ex) { throw new IllegalStateException("Loopback is unavailable"); }
    }
    @Override public void customize(ConfigurableServletWebServerFactory factory) {
        factory.setAddress(ipv4Loopback());
        factory.setPort(5081);
    }
}
