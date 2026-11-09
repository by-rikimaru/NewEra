New Era Memory Select
Kaynak kod ve derleme araçları

Bot C# / .NET Framework ile yazıldı. SQL veritabanı kullanmıyor.
Ayarlar ve harita tabloları JSON, navigasyon ağı sıkıştırılmış ikili veridir.

Klasörler
src\      C# kaynak kodları (arayüz, otomasyon, navigasyon, güncelleme).
data\     Programa gömülen veriler: yol ağı, dungeon/NPC haritaları, canavar adları, güncelleme genel anahtarı.
assets\   Simge, harita görseli ve kullanım kılavuzu (PDF).
tests\    Kontrol programları, test sonuçları ve ekran görüntüleri.
tools\    Derleme, test ve yayın betikleri.
phbot\    phBot rota eklentisi.

Çalıştırma
Hazır EXE dosyaları GitHub Releases bölümündedir. NewEra-MemorySelect.exe dosyasını indirip açın.
Program yeni sürümleri kendisi kontrol eder ve günceller.

Derleme
.NET Framework C# derleyicisinin Windows'ta kurulu olması gerekir. Python gerekmez.
Paket klasöründen Windows PowerShell'de şu komutu çalıştırın:
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build.ps1
Çıktı: build\NewEra-MemorySelect.exe

Kontroller
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-tests.ps1
Betik kontrol programını derler ve çalıştırır. Sonuç tests\Test-Sonucu.txt dosyasına yazılır.

Yeni sürüm yayınlama
1) Bir kereliğe mahsus: .\tools\1-anahtar-uret.ps1 ile kendi imza anahtarını üretin.
   Özel anahtar kullanıcı klasörünüzde NewEraKeys içinde kalır. Asla depoya koymayın.
2) Her sürümde: .\tools\2-yayin-hazirla.ps1 -Version 3.4.3.0 -Repo kullanici/depo
   Betik sürüm numarasını ayarlar, derler, imzalar ve release\ klasörüne
   NewEra-MemorySelect.exe ile manifest.json dosyalarını koyar.
3) GitHub'da vX.Y.Z.W etiketiyle yeni bir Release oluşturup bu iki dosyayı yükleyin.

phBot rota eklentisi
phbot\RikimaruGrimRoutes.py dosyasını phBot Plugins klasörüne koyun.
Canlı rota üretimi phBot içinde oyuna giriş yapılmasını gerektirir.

Sürüm durumu
Bu sürüm test sürümüdür. Tüm dungeonların canlı gidiş ve bitiş testi tamamlanmadı.
Çevrimdışı rota taramasında 18 girişin tamamı için rota bulunuyor. D11 ve D12 girişleri yol ağından kopuk küçük alanlarda olduğu için rota kısa bir boşluğu canlı yürüyüşle geçer; bu geçişin canlı testi yapılmadı.
Depo kişisel hesap ayarlarını ve güncelleme imzalama özel anahtarını içermez.
Derlemede kullanılan güncelleme doğrulama genel anahtarı data\updates-public-key.xml dosyasındadır.
tools\export-road-mesh.py navigasyon verisinin (data\road-mesh.bin.gz) üretim kaynağıdır.
Yeniden veri üretimi için yerel oyun arşivi ve çözümlenmiş bölgeler gerekir.
Hazır road-mesh.bin.gz normal EXE derlemesi için yeterlidir.

Lisans
Kod MIT lisansı altındadır (LICENSE dosyası). Oyuna ait harita, görsel ve isim verileri ilgili hak sahiplerine aittir; MIT lisansı bunları kapsamaz.
