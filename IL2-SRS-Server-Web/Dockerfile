# Build from the repository root (the server compiles sources from IL2-SR-Common):
#   docker build -f IL2-SRS-Server-Web/Dockerfile -t il2-srs-server .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore with the project's sources present: the SDK only adds Blazor's framework scripts
# (_framework/blazor.web.js) when it sees the .razor files at restore time.
COPY IL2-SRS-Server-Abstractions/ IL2-SRS-Server-Abstractions/
COPY IL2-SRS-Server-Web/ IL2-SRS-Server-Web/
COPY Plugins/IL2-SRS-Plugin-Integrations/ Plugins/IL2-SRS-Plugin-Integrations/
RUN dotnet restore IL2-SRS-Server-Web/IL2-SRS-Server-Web.csproj \
    && dotnet restore Plugins/IL2-SRS-Plugin-Integrations/IL2-SRS-Plugin-Integrations.csproj

COPY IL2-SR-Common/ IL2-SR-Common/
# Publishing the server also publishes the integrations plugin into /app/plugins; admins add others under /data/plugins.
RUN dotnet publish IL2-SRS-Server-Web/IL2-SRS-Server-Web.csproj -c Release -o /app --no-restore \
    && rm -f /app/Install-WindowsService.ps1 /app/web.config \
    && mkdir -p /data

# Alpine (musl): a minimal base without the Ubuntu packages the server never uses
# (login, tar, ICU, systemd libraries, ...), which carried unfixable scanner findings.
# Pinned by digest so every build of a commit starts from the same base. When Microsoft republishes
# 10.0-alpine, update the digest here and in the base.digest label below.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:f62a272ac1b46e83f56b8ed0416572f31cd1128e2c4a5e63eb34d348e4a36095
WORKDIR /app

# The pinned base (built 2026-09-18) still ships zlib 1.3.2-r0, vulnerable to CVE-2026-85091
# (https://github.com/advisories/GHSA-g5fp-32jq-cfw2), and OpenSSL 3.5.8. Upgrade only these packages, to at
# least the fixed versions; the build fails if Alpine can't provide them. Exact versions aren't pinned because
# Alpine drops superseded builds from its repositories. Remove this step once a newer base digest includes them.
RUN apk add --no-cache --upgrade 'zlib>=1.3.2-r1' 'libssl3>=3.5.9-r0' 'libcrypto3>=3.5.9-r0'

# Set by scripts/Publish-DockerImage.ps1; plain builds are labelled "dev".
ARG VERSION=dev
ARG REVISION=unknown
ARG IMAGE_URL=https://hub.docker.com/r/asken/il2-srs-server
LABEL org.opencontainers.image.title="IL2-SRS Server" \
      org.opencontainers.image.description="IL2 SimpleRadio Standalone server with browser admin UI and REST API (IL-2 Great Battles & Korea)" \
      org.opencontainers.image.version="$VERSION" \
      org.opencontainers.image.revision="$REVISION" \
      org.opencontainers.image.source="https://github.com/riaanjutte/IL2-SimpleRadioStandalone" \
      org.opencontainers.image.documentation="https://github.com/riaanjutte/IL2-SimpleRadioStandalone/blob/master/IL2-SRS-Server-Web/README.md" \
      org.opencontainers.image.url="$IMAGE_URL" \
      org.opencontainers.image.licenses="GPL-3.0-only" \
      org.opencontainers.image.base.name="mcr.microsoft.com/dotnet/aspnet:10.0-alpine" \
      org.opencontainers.image.base.digest="sha256:f62a272ac1b46e83f56b8ed0416572f31cd1128e2c4a5e63eb34d348e4a36095"

ENV SRS_DATA_DIR=/data \
    SRS_IMAGE_VERSION=$VERSION \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0

# The empty data folder is copied in, owned by the image's non-root "app" user (UID 1654, $APP_UID).
COPY --from=build --chown=1654:1654 /data /data
VOLUME /data

COPY --from=build /app .

# Admin UI and API
EXPOSE 8080/tcp
# SRS clients: the same port over TCP (control) and UDP (voice); change with SRS_SERVER_PORT
EXPOSE 6002/tcp 6002/udp

# Healthy while both SRS listeners run; unhealthy if one fails or an admin stops the server.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD ["dotnet", "IL2-SRS-Server.dll", "--healthcheck"]

USER $APP_UID
ENTRYPOINT ["dotnet", "IL2-SRS-Server.dll"]
