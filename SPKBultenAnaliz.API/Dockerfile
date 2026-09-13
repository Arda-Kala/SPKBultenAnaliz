# =============================================================================
# SPK Bülten Analiz Platformu — Deployment Dockerfile
# =============================================================================
# Kullanım (repo kökünde, yani SPKBultenAnaliz.sln'in bulunduğu klasörde):
#   docker build -t spk-bulten-analiz -f SPKBultenAnaliz.API/Dockerfile .
#   docker run -d -p 8080:8080 \
#     -e ConnectionStrings__DefaultConnection="Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True" \
#     -e Jwt__Key="<openssl rand -base64 48 ile üretilen değer>" \
#     -e Gemini__ApiKey="<Gemini API anahtarın>" \
#     -e Cors__AllowedOrigins__0="https://dashboard-adresin.com" \
#     -e ASPNETCORE_ENVIRONMENT="Production" \
#     --name spk-bulten-analiz spk-bulten-analiz
#
# İlk açılışta oluşturulan admin şifresini görmek için:
#   docker logs spk-bulten-analiz
# =============================================================================

# --- 1) Derleme (build) aşaması ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Önce sadece .csproj dosyalarını kopyala → katman (layer) cache'i sayesinde
# kaynak kod değiştiğinde 'dotnet restore' tekrar tekrar çalışmaz.
COPY SPKBultenAnaliz.sln .
COPY SPKBultenAnaliz.API/SPKBultenAnaliz.API.csproj SPKBultenAnaliz.API/
COPY SPKBultenAnaliz.Business/SPKBultenAnaliz.Business.csproj SPKBultenAnaliz.Business/
COPY SPKBultenAnaliz.Core/SPKBultenAnaliz.Core.csproj SPKBultenAnaliz.Core/
COPY SPKBultenAnaliz.DataAccess/SPKBultenAnaliz.DataAccess.csproj SPKBultenAnaliz.DataAccess/
COPY SPKBultenAnaliz.Tests/SPKBultenAnaliz.Tests.csproj SPKBultenAnaliz.Tests/
RUN dotnet restore SPKBultenAnaliz.sln

# Şimdi geri kalan tüm kaynak kodu kopyala ve yayına hazırla (publish).
COPY . .
RUN dotnet publish SPKBultenAnaliz.API/SPKBultenAnaliz.API.csproj \
    -c Release -o /app/publish --no-restore

# --- 2) Çalıştırma (runtime) aşaması — küçük, sadece runtime içeren imaj ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Kestrel'in tüm ağ arayüzlerinde 8080 portunu dinlemesi için.
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SPKBultenAnaliz.API.dll"]
