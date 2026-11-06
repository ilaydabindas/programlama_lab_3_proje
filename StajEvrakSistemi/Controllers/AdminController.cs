using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using StajEvrakSistemi.Data;
using StajEvrakSistemi.Models;

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
                // İsim boş bırakılmışsa DBNull hatası almamak için varsayılan değer atıyoruz
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
        
        // Düzenlenecek başvurunun mevcut statü bilgilerini getir
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
}