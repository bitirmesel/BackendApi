# ---- 1. RUN-TIME IMAGE (ASP.NET Core) ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Render Docker web service varsayılan olarak 10000 portunu dinliyor
EXPOSE 10000
ENV ASPNETCORE_URLS=http://+:10000

# ---- 2. BUILD IMAGE (.NET SDK) ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Proje dosyasını kopyala ve restore et
COPY ["DktApi.csproj", "./"]
RUN dotnet restore "DktApi.csproj"

# Tüm kodu kopyala ve build et
COPY . .
RUN dotnet build "DktApi.csproj" -c Release -o /app/build

# ---- 3. PUBLISH ----
FROM build AS publish
RUN dotnet publish "DktApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ---- 4. FINAL IMAGE ----
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "DktApi.dll"]
