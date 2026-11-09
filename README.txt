New Era Memory Select — V3.3.1 Test
Kaynak ve çalıştırılabilir sürüm paketi

Bot C# / .NET Framework ile yazıldı. SQL veritabanı kullanmıyor.
Ayarlar ve harita tabloları JSON, navigasyon ağı sıkıştırılmış ikili veridir.

Çalıştırma
outputs/NewEra-MemorySelect-V3.3.1-Test.exe dosyasını açın.

Derleme
ZIP'i bir klasöre çıkarın.
Windows PowerShell'de paket klasöründen şu komutu çalıştırın.
powershell -NoProfile -ExecutionPolicy Bypass -File .\work\build-v3.ps1
.NET Framework C# derleyicisinin Windows'ta kurulu olması gerekir.
Kaynak dosyalar outputs/v3 altında, gömülen görseller ve veriler paket içindedir.
Görseller önceden hazırlanmıştır. Derleme Python gerektirmez.

Kontroller
powershell -NoProfile -ExecutionPolicy Bypass -File .\work\build-v3-checks.ps1
.\work\V3Checks.exe
Sonuç outputs/v3/Test-Sonucu.txt dosyasına yazılır.

phBot rota eklentisi
outputs/RikimaruGrimRoutes.py dosyasını phBot Plugins klasörüne koyun.
Canlı rota üretimi phBot içinde oyuna giriş yapılmasını gerektirir.

Sürüm durumu
Bu sürüm test sürümüdür. Tüm dungeonların canlı gidiş ve bitiş testi tamamlanmadı.
Çevrimdışı rota taramasında 18 girişin tamamı için rota bulunuyor. D11 ve D12 girişleri yol ağından kopuk küçük alanlarda olduğu için rota kısa bir boşluğu canlı yürüyüşle geçer; bu geçişin canlı testi yapılmadı.
Paket kişisel hesap ayarlarını ve güncelleme imzalama özel anahtarını içermez.
Derlemede kullanılan güncelleme doğrulama genel anahtarı pakettedir.
work/export-road-mesh.py navigasyon verisinin üretim kaynağıdır.
Yeniden veri üretimi için yerel oyun arşivi ve çözümlenmiş bölgeler gerekir.
Hazır road-mesh.bin.gz normal EXE derlemesi için yeterlidir.

outputs/NewEra-MemorySelect-V3.exe yalnızca imzalı güncelleme testi için eski sürüm örneğidir. Kullanılacak sürüm V3.3.1-Test.exe dosyasıdır.


Lisans
Kod MIT lisansı altındadır (LICENSE dosyası). Oyuna ait harita, görsel ve isim verileri ilgili hak sahiplerine aittir; MIT lisansı bunları kapsamaz.

Sürümler ve güncelleme
Hazır EXE dosyaları GitHub Releases bölümünde. Kendi sürümünü yayınlamak için tools\1-anahtar-uret.ps1 ile kendi imza anahtarını üret; özel anahtarını asla depoya koyma.
