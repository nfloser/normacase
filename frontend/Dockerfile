# Build-Stufe läuft auf der Plattform des Builders: das Ergebnis sind statische Dateien.
FROM --platform=$BUILDPLATFORM node:26-alpine AS build
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npx ng build

FROM nginxinc/nginx-unprivileged:1.31-alpine
# Sicherheitskorrekturen des Basis-Images einspielen (Trivy bricht sonst bei kritischen Funden ab).
USER root
RUN apk upgrade --no-cache
USER 101
ENV NORMACASE_OIDC_ISSUER=http://localhost:8081/realms/normacase \
    NORMACASE_OIDC_ORIGIN=http://localhost:8081 \
    NORMACASE_OIDC_CLIENT_ID=normacase-workbench
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /src/dist/normacase/browser /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --retries=5 CMD wget -qO- http://127.0.0.1:8080/ >/dev/null || exit 1
