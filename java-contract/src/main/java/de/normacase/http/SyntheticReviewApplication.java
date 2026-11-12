package de.normacase.http;

import java.net.InetAddress;
import java.net.UnknownHostException;
import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.web.embedded.tomcat.TomcatServletWebServerFactory;
import org.springframework.boot.web.servlet.server.ServletWebServerFactory;
import org.springframework.context.annotation.Bean;

/** A separate, local synthetic pilot; never a productive identity boundary. */
@SpringBootApplication
public class SyntheticReviewApplication {
    public static void main(String[] args) {
        var application = new SpringApplication(SyntheticReviewApplication.class);
        application.setAddCommandLineProperties(false);
        application.run(args);
    }
    @Bean ServletWebServerFactory localServer() throws UnknownHostException {
        var server = new TomcatServletWebServerFactory();
        server.setAddress(InetAddress.getByName("127.0.0.1"));
        server.setPort(5081);
        return server;
    }
}
