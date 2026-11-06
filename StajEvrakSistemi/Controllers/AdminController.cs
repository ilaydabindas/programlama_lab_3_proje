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
}