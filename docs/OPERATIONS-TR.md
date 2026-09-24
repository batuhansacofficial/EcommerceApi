# MIRA yayın ve işletim

## Kapsam

Bu uygulama kayıt/giriş, gerçek PostgreSQL sepeti, stok ve sipariş geçmişi bulunan bir **canlı demo**dur. Checkout `Pending` sipariş oluşturur; ödeme, kargo ve vergi hesaplamaz. Gerçek satışa açılmadan önce ödeme sağlayıcısı, webhook doğrulaması, stok rezervasyonu/iptal ve iade akışları ayrıca gerekir.

`Dockerfile`, derlenmiş React arayüzünü ASP.NET Core `wwwroot` altına yerleştirir. `/api` ve arayüz aynı HTTPS adresindedir. Başka origin için CORS açılmaz. Konteyner root olmayan kullanıcıyla 8080 portunu dinler.

## Render kurulumu

`render.yaml`: Frankfurt, bir ücretsiz Docker web servisi ve bir ücretsiz PostgreSQL 18. Ücretsiz web servisi 15 dakika boşta kalınca uyur. Ücretsiz PostgreSQL 30 gün sonra sona erer; ücretsiz planda yedekleme yoktur. Bu plan kalıcı üretim SLA'sı sağlamaz. Ücretli plana geçiş ayrıca onaylanmalıdır.

1. GitHub `Verify storefront and API` kontrolünün yayınlanacak commit için başarılı olmasını bekleyin.
2. Render üzerinde Blueprint'i depodaki `render.yaml` üzerinden oluşturun. Veritabanı internete kapalıdır (`ipAllowList: []`).
3. Şema güncellemesini **ayrı yayın adımı** olarak çalıştırın. Ücretsiz planda pre-deploy ve shell bulunmadığı için ilk kurulumda geçici olarak yalnızca yayın makinesinin IP'sini veritabanının izin listesine ekleyin. Dış bağlantı URL'sini güvenli ortam değişkenine alın; komut geçmişine veya repoya yazmayın.

```sh
# Ortamda DATABASE_URL (external URL), Jwt__Issuer, Jwt__Audience,
# Jwt__SecretKey, Jwt__ExpirationMinutes=15 tanımlı olmalıdır.
# Dış bağlantıda Database__SslMode=VerifyFull kullanılmalıdır.
dotnet publish src/EcommerceApi.Api -c Release -o release
ASPNETCORE_ENVIRONMENT=Production PublicDemo__Enabled=true DemoSeed__Enabled=true \
  dotnet release/EcommerceApi.Api.dll --migrate --seed-demo
```

4. Gerekirse `AdminBootstrap__Email` ve en az 16 karakterli rastgele `AdminBootstrap__Password` yalnızca bu migration/seed adımına verilir. Varsayılan yönetici hesabı yoktur. Mevcut hesabın rolü seed tarafından yükseltilmez.
5. Migration başarılı olunca dış IP iznini kaldırın, bootstrap parolasını yayın ortamından silin ve web servisini dağıtın. Render uygulaması `DATABASE_URL` için iç ağı ve `Database__SslMode=Disable` kullanır. Bu ayarı dış URL'de kullanmayın.
6. `/health/ready` 200, `/` 200, kayıt → sepet → checkout → sipariş geçmişi → çıkış akışını doğrulayın. Render deploy `live` olmalı; hata günlüklerinde başlatma/DB hatası bulunmamalı.

Ücretli serviste aynı migration komutu pre-deploy adımına taşınabilir. Uygulama Production modunda normal başlarken migration veya seed çalıştırmaz. Sonraki şema değişikliklerini geri uyumlu ekleme/değiştirme/kaldırma aşamalarına bölün.

## Oturum ve API sözleşmesi

- Tarayıcı `/api/auth/session/register` ve `/api/auth/session/login` kullanır. Cookie: Production'da `__Host-mira-session`, Secure, HttpOnly, SameSite=Strict, 30 dakika mutlak süre. Cookie içindeki oturum kimliği PostgreSQL'de kontrol edilir. Logout sunucu kaydını siler; kopyalanmış cookie de geçersizleşir.
- Tarayıcı her yazmadan önce `/api/auth/session/csrf` tokenını alır ve `X-CSRF-TOKEN` başlığında gönderir. Tarayıcıya bearer token verilmez; localStorage'da token tutulmaz.
- Var olan bearer API istemcileri `/api/auth/login` ve `/api/auth/register` üzerinden JWT almaya devam eder. JWT süresi yapılandırmada 15 dakika; izin verilen aralık 1–60 dakikadır. JWT'ler tarayıcı logout işleminden bağımsızdır; toplu iptal için issuer/key rotasyonu gerekir.
- Parola kaydı en az 12 karakter. Yönetici bootstrap en az 16 karakter.
- Login/register için IP başına 20 istek/dakika, login için hesap başına 8 istek/5 dakika. Limitler tek uygulama sürecindedir. Birden fazla replica kullanmadan önce paylaşılan limit deposuna geçin. `429` yanıtında `Retry-After` vardır.
- Render'ın özel ingress ağı `ReverseProxy__KnownNetworks__0` ile tanımlanır. Başka sunucuya taşırken gerçek proxy adreslerini girin; tüm interneti güvenilir proxy olarak işaretlemeyin.
- Data Protection anahtarları DB'de saklanır; DB erişimi ve yedekler uygulama sırları gibi korunmalıdır. DB depolama/yedek şifrelemesi hosting katmanında sağlanmalıdır.
- Checkout: zorunlu UUID `Idempotency-Key` başlığı ve `{ "expectedTotal": 120 }` JSON gövdesi. Aynı checkout'un ağ hatası sonrası tekrarında aynı anahtarı kullanın. Anahtar kullanıcı bazında saklanır. Önceden kullanılan anahtarla farklı toplam `409` döndürür.
- Admin ürün güncellemesi: GET yanıtındaki `version` değerini PUT gövdesine ekleyin. Stok veya ürün değişmişse `409`; ürünü yeniden yükleyin.
- Ürün listesi: `page=1&pageSize=24&sort=featured`; en fazla 100 kayıt/sayfa, toplam `X-Total-Count` başlığındadır. Arama en fazla 100 karakter; `%` ve `_` düz metin aranır.
- Ürün silme arşivler; mevcut sipariş satırlarının ad/fiyat/SKU kopyaları korunur. Ziyaretçilere yalnız aktif ürünler gösterilir.

## Yedekleme, geri dönüş ve izleme

- Ücretsiz DB'de 30 günlük bitiş tarihinden önce ücretli plana geçin veya şifreli dış yedek alın. Uygulama logları yedek değildir.
- `pg_dump --format=custom` ile ayrı güvenli depoya yedek alın. Parolayı komut argümanına yazmayın. Test ortamında `pg_restore --no-owner` ile boş bir DB'ye yükleyin; migration geçmişini, kullanıcı/ürün/sipariş adetlerini ve health endpointini doğrulayın. Gerçek yayın verisi için restore tatbikatı henüz yapılmadıkça yapılmış kabul etmeyin.
- Uygulama geri dönüşü Render'da önceki başarılı commit'e redeploy ile yapılır. Şema geri dönüşünü otomatik `Down` migration ile yapmayın: bu migration oturum/idempotency alanlarını siler. Önce yedek ve uyumluluğu kontrol edin; tercihen ileri düzeltme uygulayın.
- `/health/live` process, `/health/ready` DB erişimi ve bekleyen migration kontrolüdür. Render health check `/health/ready` kullanır. Hata günlükleri JSON; yanıtta `X-Request-ID` vardır. Uptime alarmı ve dış hata bildirimi hesapta ayrıca yapılandırılmalıdır.

## Doğrulama komutları

```sh
dotnet restore
dotnet build -c Release --no-restore --warnaserror
# Yalnız *_test adında, atılabilir gerçek PostgreSQL veritabanı kabul edilir.
TEST_DATABASE='Host=localhost;Database=mira_test;...' \
  dotnet run --project tests/EcommerceApi.IntegrationTests -c Release --no-build
cd src/EcommerceApi.Web
pnpm install --frozen-lockfile
pnpm run lint
pnpm run build
pnpm exec playwright install chromium
E2E_BASE_URL=http://127.0.0.1:5211 pnpm exec playwright test
```

CI, PostgreSQL 18 ile 13 entegrasyon senaryosu, gerçek API ile tarayıcı akışı, frontend kontrolleri, NuGet/npm güvenlik taraması ve Docker imaj derlemesini içerir. Senaryoların dosyada bulunması başarılı çalıştıkları anlamına gelmez; ilgili commit'in CI sonucunu kontrol edin.
