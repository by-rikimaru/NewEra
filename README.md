# New Era Memory Select

New Era için C# / .NET Framework ile yazılmış bot. SQL veritabanı kullanmaz: ayarlar ve harita tabloları JSON, navigasyon ağı sıkıştırılmış ikili veridir.

> **Durum:** Test sürümü. Tüm dungeonların canlı gidiş ve bitiş testi tamamlanmadı.

## İndirme ve güncelleme

Hazır EXE dosyası [Releases](https://github.com/by-rikimaru/NewEra/releases/latest) sayfasındadır. `NewEra-MemorySelect.exe` dosyasını indirip açın.

Program yeni sürümleri kendisi kontrol eder, indirir, imzasını doğrular ve kendini günceller. Güncelleme adresi varsayılan olarak ayarlıdır; "Güncelleme/Kılavuz" sekmesinden değiştirilebilir.

## Klasörler

| Klasör | İçeriği |
|---|---|
| `src` | C# kaynak kodları: arayüz, otomasyon, navigasyon, güncelleme |
| `data` | Programa gömülen veriler: yol ağı, dungeon ve NPC haritaları, canavar adları, güncelleme genel anahtarı |
| `assets` | Simge, harita görseli, kullanım kılavuzu (PDF) |
| `tests` | Kontrol programları, test sonuçları, ekran görüntüleri |
| `tools` | Derleme, test ve yayın betikleri |
| `phbot` | phBot rota eklentisi |

## Derleme

Windows'ta .NET Framework C# derleyicisi (`csc.exe`) kurulu olmalıdır. Python gerekmez.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build.ps1
```

Çıktı: `build\NewEra-MemorySelect.exe`

## Testler

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-tests.ps1
```

Betik kontrol programını derleyip çalıştırır. Sonuç `tests\Test-Sonucu.txt` dosyasına yazılır. Testler oyuna komut göndermez.

## Yeni sürüm yayınlama

1. **Bir kez:** `.\tools\1-anahtar-uret.ps1` ile kendi imza anahtarınızı üretin. Özel anahtar kullanıcı klasörünüzde `NewEraKeys` içinde kalır. Depoya asla koymayın.
2. **Her sürümde:**
   ```powershell
   .\tools\2-yayin-hazirla.ps1 -Version 3.4.3.0 -Repo kullanici/depo
   ```
   Betik sürüm numarasını ayarlar, derler, imzalar ve `release\` klasörüne `NewEra-MemorySelect.exe` ile `manifest.json` dosyalarını koyar.
3. GitHub'da `v3.4.3.0` gibi bir etiketle yeni Release oluşturup bu iki dosyayı yükleyin.

Güncellemeler RSA imzasıyla doğrulanır. Doğrulama genel anahtarı `data\updates-public-key.xml` dosyasındadır; kendi sürümlerinizi yayınlamak için bu dosyayı kendi anahtarınızla değiştirip yeniden derleyin.

## phBot rota eklentisi

`phbot\RikimaruGrimRoutes.py` dosyasını phBot `Plugins` klasörüne koyun. Canlı rota üretimi phBot içinde oyuna giriş yapılmasını gerektirir.

## Bilinen durum

- Çevrimdışı rota taramasında 18 dungeon girişinin tamamı için rota bulunur.
- D11 ve D12 girişleri yol ağından kopuk küçük alanlardadır. Rota bu kısa boşluğu canlı yürüyüşle geçer; bu geçişin canlı testi yapılmadı.
- `data\road-mesh.bin.gz` dosyasının üretim kaynağı `tools\export-road-mesh.py` betiğidir. Yeniden üretmek için yerel oyun arşivi ve çözümlenmiş bölgeler gerekir. Hazır dosya normal derleme için yeterlidir.
- Depo kişisel hesap ayarlarını ve güncelleme imzalama özel anahtarını içermez.

## Harita

Grim Crypt sekmesindeki **Dünya haritası**, oyunun kendi minimap karolarından birleştirilir (kaynak: [JellyBitz/xSROMap](https://github.com/JellyBitz/xSROMap), MIT). Koordinat eşlemesi kesindir: harita pikseli = oyun koordinatı / 4 + ofset. Yeniden üretmek için `tools\build-world-map.ps1`. Nanzhang bölgesinin karosu bu veri setinde yoktur.

## Lisans

Kod [MIT lisansı](LICENSE) altındadır. Oyuna ait harita, görsel ve isim verileri ilgili hak sahiplerine aittir; MIT lisansı bunları kapsamaz.
