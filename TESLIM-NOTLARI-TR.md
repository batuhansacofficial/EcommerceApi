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

Önceki paket yalnız arayüzün demo modu ile sınanmıştı. Yeni güvenlik/checkout kodu üzerinde API ve frontend derlemesi, frontend lint çalıştırıldı. Test projesinin ilk derlemesindeki EF Core alt bağımlılık sürüm farkı için açık Relational 10.0.12 referansı eklendi; bu son değişikliğin restore/derlemesi otomatik onay servisinin kullanım sınırı nedeniyle henüz yeniden çalıştırılamadı.

Yerel PostgreSQL kurulumu Windows uygulama denetimi nedeniyle çalışmadı. Docker Desktop/CLI bulunamadı. PostgreSQL ve tarayıcı senaryoları, yayınlanacak commit için CI'da başarıyla çalışmadan tamamlanmış kabul edilmemelidir.

**Bu not oluşturulduğu anda canlı dağıtım yapılmış değildir.** Gerçek dağıtım URL'si, commit ve CI sonuçları doğrulandıktan sonra yayın kaydına eklenmelidir.

## Sınırlar

Ödeme alınmaz. Render ücretsiz veritabanı 30 gün sonra sona erer ve ücretsiz yedekleme sağlamaz. Rate limit tek süreç içindir; çoklu replica öncesinde ortak depoya taşınmalıdır. Gerçek yayın yedeğinin geri yükleme tatbikatı ve dış alarm kurulumu henüz doğrulanmamıştır. Ayrıntılar: [işletim kılavuzu](docs/OPERATIONS-TR.md).
