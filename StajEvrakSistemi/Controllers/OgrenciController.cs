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
            var komut = new SqliteCommand("SELECT Id, OgrenciNo, AdSoyad, Statu FROM Basvurular WHERE OgrenciNo=@no", baglanti);
            komut.Parameters.AddWithValue("@no", ogrenciNo);
            using var okuyucu = komut.ExecuteReader();

            if (okuyucu.Read())
            {
                basvuru = new StajBasvurusu
                {
                    Id = okuyucu.GetInt32(0),
                    OgrenciNo = okuyucu.GetString(1),
                    AdSoyad = okuyucu.IsDBNull(2) ? "Belirtilmemiş" : okuyucu.GetString(2),
                    Statu = okuyucu.GetString(3)
                };
            }
        }

        return View(basvuru);
    }

    [HttpPost]
    public IActionResult EvrakYukle(string adSoyad, IFormFile evrakBelgesi)
    {
        if (!OgrenciMi()) return RedirectToAction("Giris", "Hesap");

        var ogrenciNo = HttpContext.Session.GetString("KullaniciAdi");

        // Dosya seçilip seçilmediğini kontrol et
        if (evrakBelgesi == null || evrakBelgesi.Length == 0)
        {
            TempData["Hata"] = "Lütfen bir staj evrak belgesi (PDF) yükleyiniz.";
            return RedirectToAction("Index");
        }

        // Yalnızca PDF formatına izin ver
        var uzanti = Path.GetExtension(evrakBelgesi.FileName).ToLowerInvariant();
        if (uzanti != ".pdf")
        {
            TempData["Hata"] = "Geçersiz format! Staj belgesi yalnızca .pdf formatında kabul edilir.";
            return RedirectToAction("Index");
        }

        // Başvuruyu veritabanına 'Bekliyor' statüsüyle kaydet
        using (var baglanti = VeritabaniServisi.BaglantiGetir())
        {
            baglanti.Open();
            var komut = new SqliteCommand(
                "INSERT INTO Basvurular (OgrenciNo, AdSoyad, Statu) VALUES (@no, @ad, 'Bekliyor')", 
                baglanti);
            komut.Parameters.AddWithValue("@no", ogrenciNo);
            komut.Parameters.AddWithValue("@ad", string.IsNullOrWhiteSpace(adSoyad) ? "Belirtilmemiş" : adSoyad);
            komut.ExecuteNonQuery();
        }

        TempData["Mesaj"] = "Staj evrakınız başarıyla sisteme yüklendi ve onaya gönderildi.";
        return RedirectToAction("Index");
    }
}