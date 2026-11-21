FROM maven:3.9-eclipse-temurin-25 AS build
WORKDIR /src
COPY pom.xml .
RUN mvn -B -ntp dependency:go-offline
COPY src src
RUN mvn -B -ntp -DskipTests package && cp target/normacase-backend-*.jar /app.jar

FROM eclipse-temurin:25-jre-alpine
RUN addgroup -S normacase && adduser -S -G normacase -u 10001 normacase
USER 10001
COPY --from=build /app.jar /opt/normacase/app.jar
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=40s --retries=5 \
  CMD wget -qO- http://127.0.0.1:8080/actuator/health/readiness || exit 1
ENTRYPOINT ["java", "-XX:MaxRAMPercentage=75", "-jar", "/opt/normacase/app.jar"]
