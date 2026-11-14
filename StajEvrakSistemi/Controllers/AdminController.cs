using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using StajEvrakSistemi.Data;
using StajEvrakSistemi.Models;
using System.IO;
using System.Threading.Tasks;

namespace StajEvrakSistemi.Controllers;

public class AdminController : Controller
{
    // Giriş yapan kullanıcının Admin rolünde olup olmadığını Session'dan kontrol eder
    private bool AdminMi() => HttpContext.Session.GetString("Rol") == "Admin";

    public IActionResult Index()
    {
        // Admin değilse yetkisiz erişimi engelle ve giriş sayfasına geri gönder
        if (!AdminMi()) return RedirectToAction("Giris", "Hesap");

        using var baglanti = VeritabaniServisi.BaglantiGetir();
        baglanti.Open();
        
        // Tüm öğrenci başvurularını ekrana basmak için veritabanından çekiyoruz
        var komut = new SqliteCommand("SELECT Id, OgrenciNo, AdSoyad, Statu FROM Basvurular", baglanti);
        using var okuyucu = komut.ExecuteReader();
        
        var basvurular = new List<StajBasvurusu>();
        while (okuyucu.Read())
        {
            basvurular.Add(new StajBasvurusu
            {
                Id = okuyucu.GetInt32(0),
                OgrenciNo = okuyucu.GetString(1),
                AdSoyad = okuyucu.IsDBNull(2) ? "Belirtilmemiş" : okuyucu.GetString(2),
                Statu = okuyucu.GetString(3)
            });
        }
        return View(basvurular);
    }

    [HttpGet]
    public IActionResult Duzenle(int id)
    {
        if (!AdminMi()) return RedirectToAction("Giris", "Hesap");
        
        using var baglanti = VeritabaniServisi.BaglantiGetir();
        baglanti.Open();
        
        var komut = new SqliteCommand("SELECT Id, OgrenciNo, Statu FROM Basvurular WHERE Id=@id", baglanti);
        komut.Parameters.AddWithValue("@id", id);
        using var okuyucu = komut.ExecuteReader();
        
        if (okuyucu.Read())
        {
            var basvuru = new StajBasvurusu {
                Id = okuyucu.GetInt32(0),
                OgrenciNo = okuyucu.GetString(1),
                Statu = okuyucu.GetString(2)
            };
            return View(basvuru);
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public IActionResult Duzenle(int id, string statu)
    {
        if (!AdminMi()) return RedirectToAction("Giris", "Hesap");
        
        // Onaylanan belgeler otomatik olarak Tamamlandı statüsüne geçer
        if (statu == "Onaylandı")
        {
            statu = "Tamamlandı";
        }
        
        using var baglanti = VeritabaniServisi.BaglantiGetir();
        baglanti.Open();
        
        // Adminin formdan seçtiği yeni statüyü veritabanında güncelle
        var komut = new SqliteCommand("UPDATE Basvurular SET Statu=@s WHERE Id=@id", baglanti);
        komut.Parameters.AddWithValue("@s", statu);
        komut.Parameters.AddWithValue("@id", id);
        komut.ExecuteNonQuery();
        
        return RedirectToAction("Index");
    }

    public IActionResult Sil(int id)
    {
        if (!AdminMi()) return RedirectToAction("Giris", "Hesap");
        
        using var baglanti = VeritabaniServisi.BaglantiGetir();
        baglanti.Open();
        
        // İşlemi tamamen biten veya iptal edilen evrak kaydını sistemden sil
        var komut = new SqliteCommand("DELETE FROM Basvurular WHERE Id=@id", baglanti);
        komut.Parameters.AddWithValue("@id", id);
        komut.ExecuteNonQuery();
        
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> VeriSetiYukle(IFormFile veriSeti)
    {
        if (!AdminMi()) return RedirectToAction("Giris", "Hesap");

        if (veriSeti == null || veriSeti.Length == 0)
        {
            TempData["Hata"] = "Lütfen bir dosya seçiniz.";
            return RedirectToAction("Index");
        }

        var uzanti = Path.GetExtension(veriSeti.FileName).ToLowerInvariant();
        if (uzanti != ".csv")
        {
            TempData["Hata"] = "Test veri seti için lütfen sisteme sadece .csv formatında dosya yükleyiniz.";
            return RedirectToAction("Index");
        }

        int satirSayisi = 0;
        
        // CSV dosyasını okuyup veritabanına yazma işlemi
        using (var stream = new StreamReader(veriSeti.OpenReadStream()))
        {
            using var baglanti = VeritabaniServisi.BaglantiGetir();
            baglanti.Open();
            
            // Eğer yoksa test verileri için arka planda otomatik tablo oluşturur
            var tabloKomut = new SqliteCommand(@"
                CREATE TABLE IF NOT EXISTS TestVeriSeti (
                    OgrenciNo TEXT PRIMARY KEY,
                    AdSoyad TEXT,
                    TcKimlik TEXT,
                    DogumTarihi TEXT
                )", baglanti);
            tabloKomut.ExecuteNonQuery();

            // CSV'nin ilk satırını (Öğrenci No, Ad Soyad, TC gibi başlıkları) atla
            await stream.ReadLineAsync();

            while (!stream.EndOfStream)
            {
                var satir = await stream.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(satir)) continue;

                var sutunlar = satir.Split(','); // CSV dosyaları verileri virgülle ayırır
                if (sutunlar.Length >= 3)
                {
                    // Satırdaki verileri veritabanına kaydet
                    var ekleKomut = new SqliteCommand(@"
                        INSERT OR REPLACE INTO TestVeriSeti (OgrenciNo, AdSoyad, TcKimlik, DogumTarihi) 
                        VALUES (@no, @ad, @tc, @dogum)", baglanti);
                    
                    ekleKomut.Parameters.AddWithValue("@no", sutunlar[0].Trim());
                    ekleKomut.Parameters.AddWithValue("@ad", sutunlar[1].Trim());
                    ekleKomut.Parameters.AddWithValue("@tc", sutunlar[2].Trim());
                    ekleKomut.Parameters.AddWithValue("@dogum", sutunlar.Length > 3 ? sutunlar[3].Trim() : "");
                    
                    ekleKomut.ExecuteNonQuery();
                    satirSayisi++;
                }
            }
        }

        TempData["Mesaj"] = $"{veriSeti.FileName} adlı veri seti başarıyla okundu ve {satirSayisi} öğrenci bilgisi sisteme aktarıldı.";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public IActionResult EvrakIndir(string ogrenciNo)
    {
        if (!AdminMi()) return RedirectToAction("Giris", "Hesap");

        string dosyaAdi = string.Empty;

        // 1. Öğrenci numarasına göre veritabanından kesin dosya adını bul
        using (var baglanti = VeritabaniServisi.BaglantiGetir())
        {
            baglanti.Open();
            var komut = new SqliteCommand("SELECT DosyaAdi FROM Basvurular WHERE OgrenciNo=@no", baglanti);
            komut.Parameters.AddWithValue("@no", ogrenciNo);
            var sonuc = komut.ExecuteScalar();
            
            if (sonuc != null && sonuc != DBNull.Value)
            {
                dosyaAdi = sonuc.ToString() ?? "";
            }
        }

        if (string.IsNullOrEmpty(dosyaAdi))
        {
            TempData["Hata"] = "Bu öğrenciye ait sistemde kayıtlı bir evrak bulunamadı.";
            return RedirectToAction("Index");
        }

        // 2. Dosyanın fiziksel olarak var olup olmadığını kontrol et
        var klasorYolu = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        var tamDosyaYolu = Path.Combine(klasorYolu, dosyaAdi);

        if (!System.IO.File.Exists(tamDosyaYolu))
        {
            TempData["Hata"] = "Evrak kaydı mevcut ancak fiziksel dosya sunucuda bulunamadı.";
            return RedirectToAction("Index");
        }

        // 3. Dosya uzantısına göre doğru içerik tipini (MIME Type) belirle
        var uzanti = Path.GetExtension(dosyaAdi).ToLowerInvariant();
        string icerikTipi = uzanti switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };

        // Dosyayı doğru formatta tarayıcıya gönder
        return PhysicalFile(tamDosyaYolu, icerikTipi);
    }
}