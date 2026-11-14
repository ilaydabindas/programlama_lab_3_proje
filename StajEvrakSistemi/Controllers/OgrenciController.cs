using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using StajEvrakSistemi.Data;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Controllers;

public class OgrenciController : Controller
{
    // Yalnızca Öğrenci rolündeki kullanıcıların sayfaya erişebilmesini sağlar
    private bool OgrenciMi() => HttpContext.Session.GetString("Rol") == "Ogrenci";

    [HttpGet]
    public IActionResult Index()
    {
        if (!OgrenciMi()) return RedirectToAction("Giris", "Hesap");

        var ogrenciNo = HttpContext.Session.GetString("KullaniciAdi");
        StajBasvurusu? basvuru = null;

        // Öğrencinin daha önce yaptığı bir başvuru var mı kontrol et
        using (var baglanti = VeritabaniServisi.BaglantiGetir())
        {
            baglanti.Open();
            var komut = new SqliteCommand(@"
                SELECT Id, OgrenciNo, AdSoyad, TcKimlik, Uyruk, DogumTarihi, OkuduguBolum, 
                       TeslimYili, OncekiStajGunSayisi, SaglikYardimiStatusu, Telefon, Eposta, 
                       Adres, BaslangicTarihi, BitisTarihi, StajGunleri, MuhendisSayisi, 
                       MuhendisBolumu, GoogleFormUrl, Statu 
                FROM Basvurular WHERE OgrenciNo=@no", baglanti);
            
            komut.Parameters.AddWithValue("@no", ogrenciNo);
            using var okuyucu = komut.ExecuteReader();

            if (okuyucu.Read())
            {
                basvuru = new StajBasvurusu
                {
                    Id = okuyucu.GetInt32(0),
                    OgrenciNo = okuyucu.GetString(1),
                    AdSoyad = okuyucu.IsDBNull(2) ? "Belirtilmemiş" : okuyucu.GetString(2),
                    TcKimlik = okuyucu.IsDBNull(3) ? "" : okuyucu.GetString(3),
                    Uyruk = okuyucu.IsDBNull(4) ? "" : okuyucu.GetString(4),
                    DogumTarihi = okuyucu.IsDBNull(5) ? "" : okuyucu.GetString(5),
                    OkuduguBolum = okuyucu.IsDBNull(6) ? "" : okuyucu.GetString(6),
                    TeslimYili = okuyucu.IsDBNull(7) ? "" : okuyucu.GetString(7),
                    OncekiStajGunSayisi = okuyucu.IsDBNull(8) ? 0 : okuyucu.GetInt32(8),
                    SaglikYardimiStatusu = okuyucu.IsDBNull(9) ? "" : okuyucu.GetString(9),
                    Telefon = okuyucu.IsDBNull(10) ? "" : okuyucu.GetString(10),
                    Eposta = okuyucu.IsDBNull(11) ? "" : okuyucu.GetString(11),
                    Adres = okuyucu.IsDBNull(12) ? "" : okuyucu.GetString(12),
                    BaslangicTarihi = okuyucu.IsDBNull(13) ? "" : okuyucu.GetString(13),
                    BitisTarihi = okuyucu.IsDBNull(14) ? "" : okuyucu.GetString(14),
                    StajGunleri = okuyucu.IsDBNull(15) ? "" : okuyucu.GetString(15),
                    MuhendisSayisi = okuyucu.IsDBNull(16) ? 0 : okuyucu.GetInt32(16),
                    MuhendisBolumu = okuyucu.IsDBNull(17) ? "" : okuyucu.GetString(17),
                    GoogleFormUrl = okuyucu.IsDBNull(18) ? "" : okuyucu.GetString(18),
                    Statu = okuyucu.GetString(19)
                };
            }
        }

        return View(basvuru);
    }

    [HttpGet]
    public IActionResult EvrakYukle()
    {
        if (!OgrenciMi()) return RedirectToAction("Giris", "Hesap");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> EvrakYukle(
        string adSoyad, string tcKimlik, string uyruk, string dogumTarihi, string okuduguBolum,
        string teslimYili, int oncekiStajGunSayisi, string saglikYardimiStatusu,
        string telefon, string eposta, string adres, string baslangicTarihi, string bitisTarihi, 
        string stajGunleri, int muhendisSayisi, string muhendisBolumu, string googleFormUrl, IFormFile evrakBelgesi)
    {
        if (!OgrenciMi()) return RedirectToAction("Giris", "Hesap");

        var ogrenciNo = HttpContext.Session.GetString("KullaniciAdi");

        // Dosya seçilip seçilmediğini kontrol et
        if (evrakBelgesi == null || evrakBelgesi.Length == 0)
        {
            TempData["Hata"] = "Lütfen staj evrakınızı yükleyiniz.";
            return RedirectToAction("EvrakYukle");
        }

        // format konrolü
        var izinVerilenUzantilar = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };
        var uzanti = Path.GetExtension(evrakBelgesi.FileName).ToLowerInvariant();
        
        if (!izinVerilenUzantilar.Contains(uzanti))
        {
            TempData["Hata"] = "Geçersiz format! Staj belgesi yalnızca Word (.doc/.docx), PDF, JPEG ve PNG formatında kabul edilir.";
            return RedirectToAction("EvrakYukle");
        }

        // 1. Klasör yoksa oluştur (wwwroot/uploads)
        var klasorYolu = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if (!Directory.Exists(klasorYolu))
        {
            Directory.CreateDirectory(klasorYolu);
        }

        // 2. Benzersiz dosya adı oluştur ve dosyayı fiziksel olarak kaydet
        var benzersizDosyaAdi = $"{ogrenciNo}_{Guid.NewGuid():N}{uzanti}";
        var tamDosyaYolu = Path.Combine(klasorYolu, benzersizDosyaAdi);

        using (var stream = new FileStream(tamDosyaYolu, FileMode.Create))
        {
            await evrakBelgesi.CopyToAsync(stream);
        }

        // 3. Başvuruyu tüm detaylarıyla veritabanına kaydet veya güncelle
        using (var baglanti = VeritabaniServisi.BaglantiGetir())
        {
            baglanti.Open();
            
            // Öğrencinin daha önceden kaydı var mı kontrol et
            var kontrolKomut = new SqliteCommand("SELECT COUNT(*) FROM Basvurular WHERE OgrenciNo=@no", baglanti);
            kontrolKomut.Parameters.AddWithValue("@no", ogrenciNo);
            long kayitSayisi = Convert.ToInt64(kontrolKomut.ExecuteScalar());

            if (kayitSayisi > 0)
            {
                // Kayıt varsa durumu güncelleyip tekrar 'İnceleniyor'a al ve tüm alanları güncelle
                var guncelleKomut = new SqliteCommand(@"
                    UPDATE Basvurular SET 
                        AdSoyad=@ad, TcKimlik=@tc, Uyruk=@uyruk, DogumTarihi=@dogum, OkuduguBolum=@bolum,
                        TeslimYili=@yil, OncekiStajGunSayisi=@onceki, SaglikYardimiStatusu=@saglik,
                        Telefon=@tel, Eposta=@ep, Adres=@adres, BaslangicTarihi=@bas, BitisTarihi=@bit, 
                        StajGunleri=@gun, MuhendisSayisi=@muh, MuhendisBolumu=@muhBolum, GoogleFormUrl=@url,
                        DosyaAdi=@dosya, Statu='İnceleniyor' 
                    WHERE OgrenciNo=@no", baglanti);
                
                guncelleKomut.Parameters.AddWithValue("@no", ogrenciNo);
                guncelleKomut.Parameters.AddWithValue("@ad", string.IsNullOrWhiteSpace(adSoyad) ? "Belirtilmemiş" : adSoyad);
                guncelleKomut.Parameters.AddWithValue("@tc", tcKimlik ?? "");
                guncelleKomut.Parameters.AddWithValue("@uyruk", uyruk ?? "");
                guncelleKomut.Parameters.AddWithValue("@dogum", dogumTarihi ?? "");
                guncelleKomut.Parameters.AddWithValue("@bolum", okuduguBolum ?? "");
                guncelleKomut.Parameters.AddWithValue("@yil", teslimYili ?? "");
                guncelleKomut.Parameters.AddWithValue("@onceki", oncekiStajGunSayisi);
                guncelleKomut.Parameters.AddWithValue("@saglik", saglikYardimiStatusu ?? "");
                guncelleKomut.Parameters.AddWithValue("@tel", telefon ?? "");
                guncelleKomut.Parameters.AddWithValue("@ep", eposta ?? "");
                guncelleKomut.Parameters.AddWithValue("@adres", adres ?? "");
                guncelleKomut.Parameters.AddWithValue("@bas", baslangicTarihi ?? "");
                guncelleKomut.Parameters.AddWithValue("@bit", bitisTarihi ?? "");
                guncelleKomut.Parameters.AddWithValue("@gun", stajGunleri ?? "");
                guncelleKomut.Parameters.AddWithValue("@muh", muhendisSayisi);
                guncelleKomut.Parameters.AddWithValue("@muhBolum", muhendisBolumu ?? "");
                guncelleKomut.Parameters.AddWithValue("@url", googleFormUrl ?? "");
                guncelleKomut.Parameters.AddWithValue("@dosya", benzersizDosyaAdi);
                guncelleKomut.ExecuteNonQuery();
            }
            else
            {
                // İlk defa geliyorsa tüm alanlarla yeni ekle 
                var ekleKomut = new SqliteCommand(@"
                    INSERT INTO Basvurular 
                    (OgrenciNo, AdSoyad, TcKimlik, Uyruk, DogumTarihi, OkuduguBolum, TeslimYili, OncekiStajGunSayisi, SaglikYardimiStatusu, Telefon, Eposta, Adres, BaslangicTarihi, BitisTarihi, StajGunleri, MuhendisSayisi, MuhendisBolumu, GoogleFormUrl, DosyaAdi, Statu) 
                    VALUES (@no, @ad, @tc, @uyruk, @dogum, @bolum, @yil, @onceki, @saglik, @tel, @ep, @adres, @bas, @bit, @gun, @muh, @muhBolum, @url, @dosya, 'İnceleniyor')", baglanti);
                
                ekleKomut.Parameters.AddWithValue("@no", ogrenciNo);
                ekleKomut.Parameters.AddWithValue("@ad", string.IsNullOrWhiteSpace(adSoyad) ? "Belirtilmemiş" : adSoyad);
                ekleKomut.Parameters.AddWithValue("@tc", tcKimlik ?? "");
                ekleKomut.Parameters.AddWithValue("@uyruk", uyruk ?? "");
                ekleKomut.Parameters.AddWithValue("@dogum", dogumTarihi ?? "");
                ekleKomut.Parameters.AddWithValue("@bolum", okuduguBolum ?? "");
                ekleKomut.Parameters.AddWithValue("@yil", teslimYili ?? "");
                ekleKomut.Parameters.AddWithValue("@onceki", oncekiStajGunSayisi);
                ekleKomut.Parameters.AddWithValue("@saglik", saglikYardimiStatusu ?? "");
                ekleKomut.Parameters.AddWithValue("@tel", telefon ?? "");
                ekleKomut.Parameters.AddWithValue("@ep", eposta ?? "");
                ekleKomut.Parameters.AddWithValue("@adres", adres ?? "");
                ekleKomut.Parameters.AddWithValue("@bas", baslangicTarihi ?? "");
                ekleKomut.Parameters.AddWithValue("@bit", bitisTarihi ?? "");
                ekleKomut.Parameters.AddWithValue("@gun", stajGunleri ?? "");
                ekleKomut.Parameters.AddWithValue("@muh", muhendisSayisi);
                ekleKomut.Parameters.AddWithValue("@muhBolum", muhendisBolumu ?? "");
                ekleKomut.Parameters.AddWithValue("@url", googleFormUrl ?? "");
                ekleKomut.Parameters.AddWithValue("@dosya", benzersizDosyaAdi);
                ekleKomut.ExecuteNonQuery();
            }
        }

        TempData["Mesaj"] = "Staj bilgi formunuz ve evrakınız başarıyla sisteme kaydedildi.";
        return RedirectToAction("Index");
    }
}