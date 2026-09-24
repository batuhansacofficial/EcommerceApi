# Uygulama ve yayın durumu

## Kodda uygulananlar

- .NET 10; Microsoft paketleri 10.0.12, Npgsql EF provider 10.0.3.
- MIRA React/Bootstrap arayüzü ve aynı adresten API erişimi.
- Secure/HttpOnly/SameSite cookie, CSRF, sunucuda oturum iptali; bearer API istemcileri için kısa ömürlü JWT.
- Checkout idempotency, kullanıcı bazında sepet kilidi, atomik stok düşümü, işlem geri alma ve beklenen toplam kontrolü.
- Admin ürün sürüm kontrolü, ürün arşivleme, aktif katalog, sayfalama ve düz metin arama.
- Kontrollü seed/admin bootstrap, açık production migration komutu, health endpointleri ve güvenlik başlıkları.
- 13 gerçek PostgreSQL entegrasyon senaryosu, iki gerçek API tarayıcı testi ve GitHub Actions doğrulama hattı.
- Tek konteyner dağıtımı, Render Blueprint ve işletim kılavuzu.

## Doğrulama durumu

.NET çözümü 0 uyarı ve 0 hata ile derlendi; frontend lint ve production build geçti. Docker Desktop üzerindeki PostgreSQL 18 ile 13 entegrasyon senaryosu geçti. Microsoft Edge üzerinde gerçek API ile kayıt, sepet, sipariş, oturum yenileme, geçmiş, çıkış ve mobil katalog testleri (2/2) geçti. GitHub Actions ve Render yayın doğrulaması devam ediyor.

**Bu not oluşturulduğu anda canlı dağıtım yapılmış değildir.** Gerçek dağıtım URL'si, commit ve CI sonuçları doğrulandıktan sonra yayın kaydına eklenmelidir.

## Sınırlar

Ödeme alınmaz. Render ücretsiz veritabanı 30 gün sonra sona erer ve ücretsiz yedekleme sağlamaz. Rate limit tek süreç içindir; çoklu replica öncesinde ortak depoya taşınmalıdır. Gerçek yayın yedeğinin geri yükleme tatbikatı ve dış alarm kurulumu henüz doğrulanmamıştır. Ayrıntılar: [işletim kılavuzu](docs/OPERATIONS-TR.md).
