# Derleme aşaması: tam SDK ile restore ve publish.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Önce proje dosyaları: bağımlılık değişmedikçe restore katmanı önbellekten gelir.
COPY src/Tamirhane.Domain/Tamirhane.Domain.csproj src/Tamirhane.Domain/
COPY src/Tamirhane.Application/Tamirhane.Application.csproj src/Tamirhane.Application/
COPY src/Tamirhane.Infrastructure/Tamirhane.Infrastructure.csproj src/Tamirhane.Infrastructure/
COPY src/Tamirhane.Web/Tamirhane.Web.csproj src/Tamirhane.Web/
RUN dotnet restore src/Tamirhane.Web/Tamirhane.Web.csproj

COPY src/ src/
RUN dotnet publish src/Tamirhane.Web/Tamirhane.Web.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# Çalışma aşaması: sadece ASP.NET çalışma ortamı, imajın root olmayan "app" kullanıcısıyla.
# 10.0 etiketi her zaman son yamayı taşır.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Workstation GC: 512 MB'lık ücretsiz sunucuda bellek düşük kalır.
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=0
USER $APP_UID
EXPOSE 8080

# Render dinlenecek portu $PORT ile verir.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet Tamirhane.Web.dll"]
