# Build from the repository root (the server compiles sources from IL2-SR-Common):
#   docker build -f IL2-SRS-Server-Web/Dockerfile -t il2-srs-server .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore with the project's sources present: the SDK only adds Blazor's framework scripts
# (_framework/blazor.web.js) when it sees the .razor files at restore time.
COPY IL2-SRS-Server-Web/ IL2-SRS-Server-Web/
RUN dotnet restore IL2-SRS-Server-Web/IL2-SRS-Server-Web.csproj

COPY IL2-SR-Common/ IL2-SR-Common/
RUN dotnet publish IL2-SRS-Server-Web/IL2-SRS-Server-Web.csproj -c Release -o /app --no-restore \
    && rm -f /app/Install-WindowsService.ps1 /app/web.config \
    && mkdir -p /data

# Alpine (musl): a minimal base without the Ubuntu packages the server never uses
# (login, tar, ICU, systemd libraries, ...), which carried unfixable scanner findings.
# Pinned by digest so every build of a commit starts from the same base. When Microsoft republishes
# 10.0-alpine, update the digest here and in the base.digest label below.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:254fe017495996d3abcc487ffacdcce2b73affb380d3af87af12c8dff41fe0b0
WORKDIR /app

# The pinned base (built 2026-10-24: Alpine 3.24.3, .NET 10.0.14) ships musl 1.2.6-r3, vulnerable to
# CVE-2026-107891 (fixed in 1.2.6-r6). Alpine 3.24 doesn't have the fix yet, so take musl and musl-utils (and the
# scanelf they need) from edge. "~1.2.6" keeps them on 1.2.6, so a newer musl release in edge (and whatever it
# needs from edge) can't come along; the build fails if edge's 1.2.6 is older than the fix. (apk keeps only one
# version constraint per package, so the minimum is checked after installing.)
# Remove this step once a newer base digest includes the fix.
RUN apk add --no-cache --upgrade --repository https://dl-cdn.alpinelinux.org/alpine/edge/main \
        'musl~1.2.6' 'musl-utils~1.2.6' \
    && musl="$(apk info -v 2>/dev/null | sed -n 's/^musl-\([0-9].*\)$/\1/p')" \
    && if [ "$(apk version -t "$musl" 1.2.6-r6)" = "<" ]; then echo "musl $musl is older than 1.2.6-r6" >&2; exit 1; fi

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
      org.opencontainers.image.base.digest="sha256:254fe017495996d3abcc487ffacdcce2b73affb380d3af87af12c8dff41fe0b0"

ENV SRS_DATA_DIR=/data \
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
