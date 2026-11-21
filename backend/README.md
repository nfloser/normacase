# Backend

Spring Boot, Java 25, PostgreSQL. Module und Grenzen: [Architektur](../docs/02-architektur.md).

```bash
mvn -f backend/pom.xml verify          # Tests, braucht Docker für Testcontainers
docker run -d --name normacase-db -e POSTGRES_USER=normacase -e POSTGRES_PASSWORD=normacase -p 5432:5432 postgres:17-alpine
mvn -f backend/pom.xml spring-boot:run # http://localhost:8080/api/v1/system
```

Konfiguration über `NORMACASE_DB_URL`, `NORMACASE_DB_USER`, `NORMACASE_DB_PASSWORD`. Die Modulübersicht als PlantUML erzeugt `ModularityTest` unter `target/spring-modulith-docs/`.
