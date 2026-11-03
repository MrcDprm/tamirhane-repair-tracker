# Yayın: Oracle Cloud + Caddy

Uygulama Oracle Cloud Always Free ARM sunucusunda (Ubuntu, Frankfurt) systemd servisi olarak çalışır; önünde
Caddy (HTTPS) vardır. Veritabanı Neon (Frankfurt), e-posta Resend (İrlanda).

## İlk kurulum (sunucuda bir kez)

```sh
sudo mkdir -p /opt/tamirhane /etc/tamirhane
sudo install -m 600 /dev/null /etc/tamirhane/tamirhane.env   # sonra sudoedit ile doldurulur
sudo cp tamirhane.service /etc/systemd/system/
sudo systemctl daemon-reload && sudo systemctl enable tamirhane
# Caddyfile'daki site bloğunu /etc/caddy/Caddyfile'a ekleyip: sudo systemctl reload caddy
```

`/etc/tamirhane/tamirhane.env` (değerler `.env.example`'daki adlarla):

```
DATABASE_URL=postgresql://...            # Neon, direct bağlantı
RESEND_API_KEY=re_...
EMAIL_FROM=Tamirhane <tamirhane@miracdeprem.com>
PUBLIC_ORIGIN=https://tamirhane.miracdeprem.com
AllowedHosts=tamirhane.miracdeprem.com
VAPID_PUBLIC_KEY=...
VAPID_PRIVATE_KEY=...
VAPID_SUBJECT=mailto:mrcdprmm@hotmail.com
```

## Yeni sürüm

Bilgisayarda (repo kökünde) Linux ARM için tek klasörlük, .NET kurulumu gerektirmeyen bir yayın üretilir. ReadyToRun:
kod önceden derlenir, küçük işlemcide ilk açılışlar hızlanır.

```sh
dotnet publish src/Tamirhane.Web -c Release -r linux-arm64 --self-contained -p:PublishReadyToRun=true -o dist/linux-arm64
tar -czf dist/tamirhane.tar.gz -C dist/linux-arm64 .
scp dist/tamirhane.tar.gz ubuntu@SUNUCU:/tmp/
ssh ubuntu@SUNUCU 'sudo rm -rf /opt/tamirhane/* && sudo tar -xzf /tmp/tamirhane.tar.gz -C /opt/tamirhane && sudo systemctl restart tamirhane'
```

Migration'lar uygulama açılırken uygulanır. Kontrol: `curl -s https://tamirhane.miracdeprem.com/health` → `Healthy`.
