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
RUN mkdir -p /app/logs /app/data-protection-keys \
    && chown -R $APP_UID:$APP_UID /app/logs /app/data-protection-keys
USER $APP_UID
EXPOSE 8080 44405 44406 55901 55902 55903 55904 55905 55906 55980
ENTRYPOINT ["dotnet", "MUnique.OpenMU.Startup.dll", "-autostart"]
