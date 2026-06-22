# Multi-stage build for the .NET 10 API. Build context = ./backend (so Directory.*.props are included).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "src/Api/Academy.Api.csproj"
RUN dotnet publish "src/Api/Academy.Api.csproj" -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# Kerberos/GSSAPI lib so Npgsql doesn't error-log when the server advertises GSS auth.
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Academy.Api.dll"]
