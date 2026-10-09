FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy every referenced project before restore; this includes uncommitted local work.
COPY . .
RUN dotnet publish "Startup/MUnique.OpenMU.Startup.csproj" \
    -c Release -o /app/publish -p:ci=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
RUN apk add --no-cache icu-libs tzdata
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
COPY --from=build /app/publish .
ARG APP_UID=1000
RUN mkdir -p /app/logs /app/data-protection-keys \
    && adduser -D -u ${APP_UID} appuser \
    && chown -R appuser /app/logs /app/data-protection-keys \
    && chmod 700 /app/logs /app/data-protection-keys
USER appuser
HEALTHCHECK --interval=30s --timeout=3s --start-period=15s --retries=3 \
  CMD nc -z localhost 8080 || exit 1
EXPOSE 8080 44405 44406 55901 55902 55903 55904 55905 55906 55980
ENTRYPOINT ["dotnet", "MUnique.OpenMU.Startup.dll", "-autostart"]
