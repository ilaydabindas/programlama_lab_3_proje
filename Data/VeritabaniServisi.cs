using Microsoft.Data.Sqlite;

namespace StajEvrakSistemi.Data;

public static class VeritabaniServisi
{
    private const string ConnectionString = "Data Source=staj_sistemi.db";

    public static SqliteConnection BaglantiGetir() => new SqliteConnection(ConnectionString);

    public static void Baslat()
    {
        using var baglanti = BaglantiGetir();
        baglanti.Open();

        var komut = baglanti.CreateCommand();
        komut.CommandText = @"
            CREATE TABLE IF NOT EXISTS Kullanicilar (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                KullaniciAdi TEXT NOT NULL UNIQUE,
                AdSoyad TEXT,
                SifreHash TEXT NOT NULL,
                Rol TEXT NOT NULL,
                HataliGirisSayisi INTEGER DEFAULT 0,
                HesapKilitliMi INTEGER DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS Basvurular (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OgrenciNo TEXT NOT NULL,
                AdSoyad TEXT,
                TcKimlik TEXT,
                Telefon TEXT,
                Eposta TEXT,
                Adres TEXT,
                BaslangicTarihi TEXT,
                BitisTarihi TEXT,
                StajGunSayisi INTEGER,
                DosyaAdi TEXT,
                GoogleFormUrl TEXT,
                Statu TEXT NOT NULL,
                HataMesaji TEXT
            );
            CREATE TABLE IF NOT EXISTS TestVeriSeti (
                OgrenciNo TEXT PRIMARY KEY,
                AdSoyad TEXT,
                TcKimlik TEXT,
                Telefon TEXT,
                Eposta TEXT,
                Adres TEXT
            );
        ";
        komut.ExecuteNonQuery();
        VarsayilanKullanicilariEkle();
    }

    public static string SifreHashle(string sifre)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(sifre);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public static void VarsayilanKullanicilariEkle()
    {
        using var baglanti = BaglantiGetir();
        baglanti.Open();
        var komut = baglanti.CreateCommand();
        string hashliSifre = SifreHashle("12345");
        komut.CommandText = $@"
            INSERT OR IGNORE INTO Kullanicilar (KullaniciAdi, AdSoyad, SifreHash, Rol) VALUES ('admin', 'Sistem Yöneticisi', '{hashliSifre}', 'Admin');
            INSERT OR IGNORE INTO Kullanicilar (KullaniciAdi, AdSoyad, SifreHash, Rol) VALUES ('220202001', 'Yağmur Sıla Kocaoğlu', '{hashliSifre}', 'Ogrenci');
        ";
        komut.ExecuteNonQuery();
    }
}