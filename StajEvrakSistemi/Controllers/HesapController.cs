using Microsoft.AspNetCore.Mvc;
using StajEvrakSistemi.Data;
using Microsoft.Data.Sqlite;

namespace StajEvrakSistemi.Controllers;

public class HesapController : Controller
{
    [HttpGet]
    public IActionResult Giris() => View();

    [HttpPost]
    public IActionResult Giris(string kullaniciAdi, string sifre)
    {
        string hashliSifre = VeritabaniServisi.SifreHashle(sifre);
        using var baglanti = VeritabaniServisi.BaglantiGetir();
        baglanti.Open();

        // Kilit kontrolü
        var kilitKomut = new SqliteCommand("SELECT HesapKilitliMi FROM Kullanicilar WHERE KullaniciAdi=@k", baglanti);
        kilitKomut.Parameters.AddWithValue("@k", kullaniciAdi);
        var kilit = kilitKomut.ExecuteScalar();
        
        if (kilit != null && Convert.ToInt32(kilit) == 1)
        {
            ViewBag.Hata = "Hesabınız 5 hatalı giriş denemesi nedeniyle kilitlenmiştir!";
            return View();
        }

        // Doğru şifre ile giriş kontrolü
        var komut = new SqliteCommand("SELECT Rol FROM Kullanicilar WHERE KullaniciAdi=@k AND SifreHash=@s", baglanti);
        komut.Parameters.AddWithValue("@k", kullaniciAdi);
        komut.Parameters.AddWithValue("@s", hashliSifre);
        var rol = komut.ExecuteScalar()?.ToString();

        if (rol != null)
        {
            // Giriş başarılıysa hatalı giriş sayacını 0'a çek
            var sifirla = new SqliteCommand("UPDATE Kullanicilar SET HataliGirisSayisi = 0 WHERE KullaniciAdi=@k", baglanti);
            sifirla.Parameters.AddWithValue("@k", kullaniciAdi);
            sifirla.ExecuteNonQuery();

            HttpContext.Session.SetString("KullaniciAdi", kullaniciAdi);
            HttpContext.Session.SetString("Rol", rol);
            return rol == "Admin" ? RedirectToAction("Index", "Admin") : RedirectToAction("Index", "Ogrenci");
        }
        
        // Giriş hatalıysa (Şifre yanlış veya kullanıcı yok.)
        var hataArtir = new SqliteCommand(@"
            UPDATE Kullanicilar SET HataliGirisSayisi = HataliGirisSayisi + 1, 
            HesapKilitliMi = CASE WHEN HataliGirisSayisi + 1 >= 5 THEN 1 ELSE 0 END 
            WHERE KullaniciAdi=@k", baglanti);
        hataArtir.Parameters.AddWithValue("@k", kullaniciAdi);
        
        // Etkilenen satır sayısını alıyoruz (Kullanıcı veritabanında varsa 1 döner)
        int etkilenenSatir = hataArtir.ExecuteNonQuery();

        if (etkilenenSatir > 0)
        {
            // Kullanıcı var ama şifreyi yanlış girdiyse güncel hata sayısını çekelim
            var sayacKomut = new SqliteCommand("SELECT HataliGirisSayisi FROM Kullanicilar WHERE KullaniciAdi=@k", baglanti);
            sayacKomut.Parameters.AddWithValue("@k", kullaniciAdi);
            int guncelHataSayisi = Convert.ToInt32(sayacKomut.ExecuteScalar());

            int kalanHak = 5 - guncelHataSayisi;

            if (kalanHak > 0)
            {
                ViewBag.Hata = $"Şifre hatalı! Kalan deneme hakkınız: {kalanHak}";
            }
            else
            {
                ViewBag.Hata = "Art arda 5 hatalı giriş yaptınız. Hesabınız güvenlik amacıyla kilitlenmiştir!";
            }
        }
        else
        {
            // Veritabanında öyle bir kullanıcı adı hiç yoksa
            ViewBag.Hata = "Kullanıcı adı veya şifre hatalı!";
        }

        return View();
    }

    [HttpGet]
    public IActionResult KayitOl() => View();

    [HttpPost]
    public IActionResult KayitOl(string kullaniciAdi, string adSoyad, string sifre)
    {
        using var baglanti = VeritabaniServisi.BaglantiGetir();
        baglanti.Open();

        var kontrol = new SqliteCommand("SELECT COUNT(*) FROM Kullanicilar WHERE KullaniciAdi=@k", baglanti);
        kontrol.Parameters.AddWithValue("@k", kullaniciAdi);
        if (Convert.ToInt32(kontrol.ExecuteScalar()) > 0)
        {
            ViewBag.Hata = "Bu öğrenci numarası / kullanıcı adı zaten kayıtlı!";
            return View();
        }

        string hashli = VeritabaniServisi.SifreHashle(sifre);
        var ekle = new SqliteCommand("INSERT INTO Kullanicilar (KullaniciAdi, AdSoyad, SifreHash, Rol) VALUES (@k, @ad, @s, 'Ogrenci')", baglanti);
        ekle.Parameters.AddWithValue("@k", kullaniciAdi);
        ekle.Parameters.AddWithValue("@ad", adSoyad ?? "");
        ekle.Parameters.AddWithValue("@s", hashli);
        ekle.ExecuteNonQuery();

        TempData["Mesaj"] = "Kaydınız başarıyla oluşturuldu! Belirlediğiniz şifre ile giriş yapabilirsiniz.";
        return RedirectToAction("Giris");
    }

    public IActionResult Cikis()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Giris");
    }
}