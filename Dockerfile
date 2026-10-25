FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/ src/

RUN dotnet restore src/MiddiFranchi.Web/MiddiFranchi.Web.csproj

RUN dotnet publish src/MiddiFranchi.Web/MiddiFranchi.Web.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /home/app/.local/share/MiddiFranchi /home/app/.aspnet/DataProtection-Keys \
    && chown -R app:app /home/app/.local /home/app/.aspnet

COPY --from=build /app/publish .

USER app

ENTRYPOINT ["dotnet", "MiddiFranchi.Web.dll"]
