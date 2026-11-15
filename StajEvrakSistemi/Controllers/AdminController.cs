using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using StajEvrakSistemi.Data;
using StajEvrakSistemi.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System;

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
            var basvuru = new StajBasvurusu
            {
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
            
            // Madde 4.3'teki tüm 12 sütunu içeren tablo yapısı
            var tabloKomut = new SqliteCommand(@"
                CREATE TABLE IF NOT EXISTS TestVeriSeti (
                    OgrenciNo TEXT PRIMARY KEY,
                    AdSoyad TEXT,
                    TcKimlik TEXT,
                    Uyruk TEXT,
                    DogumTarihi TEXT,
                    IkametgahAdresi TEXT,
                    Bolum TEXT,
                    TeslimYili TEXT,
                    OncekiStajGunSayisi TEXT,
                    Eposta TEXT,
                    TelefonNo TEXT,
                    SaglikYardimi TEXT
                )", baglanti);
            tabloKomut.ExecuteNonQuery();

            // EĞER TABLO ESKİDEN DAHA AZ SÜTUNLA OLUŞMUŞSA EKSİK SÜTUNLARI OTOMATİK EKLE:
            string[] tumSutunlar = {
                "Uyruk", "DogumTarihi", "IkametgahAdresi", "Bolum", 
                "TeslimYili", "OncekiStajGunSayisi", "Eposta", "TelefonNo", "SaglikYardimi"
            };

            foreach (var sutun in tumSutunlar)
            {
                try
                {
                    using var sutunKomut = new SqliteCommand($"ALTER TABLE TestVeriSeti ADD COLUMN {sutun} TEXT;", baglanti);
                    sutunKomut.ExecuteNonQuery();
                }
                catch { }
            }

           // Yeni CSV yüklenmeden önce eski CSV verilerinin tamamını temizle:
new SqliteCommand("DELETE FROM TestVeriSeti;", baglanti).ExecuteNonQuery();

// CSV'nin ilk satırını (başlıkları) atla
await stream.ReadLineAsync();

            while (!stream.EndOfStream)
            {
                var satir = await stream.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(satir)) continue;

                var sutunlar = satir.Split(','); // CSV dosyaları verileri virgülle ayırır

                // 1. Durum: Madde 4.3'teki 12 sütunlu tam CSV (kullanici_veri_seti_4_3_duzeltilmis.csv)
                if (sutunlar.Length >= 12)
                {
                    var ekleKomut = new SqliteCommand(@"
                        INSERT OR REPLACE INTO TestVeriSeti (
                            AdSoyad, TcKimlik, Uyruk, OgrenciNo, DogumTarihi, 
                            IkametgahAdresi, Bolum, TeslimYili, OncekiStajGunSayisi, 
                            Eposta, TelefonNo, SaglikYardimi
                        ) VALUES (
                            @ad, @tc, @uyruk, @no, @dogum, 
                            @adres, @bolum, @yil, @gun, 
                            @eposta, @tel, @saglik
                        )", baglanti);

                    ekleKomut.Parameters.AddWithValue("@ad", sutunlar[0].Trim());
                    ekleKomut.Parameters.AddWithValue("@tc", sutunlar[1].Trim());
                    ekleKomut.Parameters.AddWithValue("@uyruk", sutunlar[2].Trim());
                    ekleKomut.Parameters.AddWithValue("@no", sutunlar[3].Trim());
                    ekleKomut.Parameters.AddWithValue("@dogum", sutunlar[4].Trim());
                    ekleKomut.Parameters.AddWithValue("@adres", sutunlar[5].Trim());
                    ekleKomut.Parameters.AddWithValue("@bolum", sutunlar[6].Trim());
                    ekleKomut.Parameters.AddWithValue("@yil", sutunlar[7].Trim());
                    ekleKomut.Parameters.AddWithValue("@gun", sutunlar[8].Trim());
                    ekleKomut.Parameters.AddWithValue("@eposta", sutunlar[9].Trim());
                    ekleKomut.Parameters.AddWithValue("@tel", sutunlar[10].Trim());
                    ekleKomut.Parameters.AddWithValue("@saglik", sutunlar[11].Trim());

                    ekleKomut.ExecuteNonQuery();
                    satirSayisi++;
                }
                // 2. Durum: 4 veya 5 sütunlu kısa CSV dosyalarıyla da uyumlu çalışır
                else if (sutunlar.Length >= 3)
                {
                    string ogrenciNo = sutunlar[0].Trim();
                    string adSoyad = sutunlar[1].Trim();
                    string tcKimlik = sutunlar[2].Trim();
                    string uyruk = "T.C.";
                    string dogumTarihi = "";

                    if (sutunlar.Length >= 5)
                    {
                        uyruk = sutunlar[3].Trim();
                        dogumTarihi = sutunlar[4].Trim();
                    }
                    else if (sutunlar.Length == 4)
                    {
                        dogumTarihi = sutunlar[3].Trim();
                        uyruk = tcKimlik.StartsWith("99") ? "Yabancı" : "T.C.";
                    }

                    var ekleKomut = new SqliteCommand(@"
                        INSERT OR REPLACE INTO TestVeriSeti (OgrenciNo, AdSoyad, TcKimlik, Uyruk, DogumTarihi) 
                        VALUES (@no, @ad, @tc, @uyruk, @dogum)", baglanti);
                    
                    ekleKomut.Parameters.AddWithValue("@no", ogrenciNo);
                    ekleKomut.Parameters.AddWithValue("@ad", adSoyad);
                    ekleKomut.Parameters.AddWithValue("@tc", tcKimlik);
                    ekleKomut.Parameters.AddWithValue("@uyruk", uyruk);
                    ekleKomut.Parameters.AddWithValue("@dogum", dogumTarihi);
                    
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