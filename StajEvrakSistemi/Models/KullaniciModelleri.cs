namespace StajEvrakSistemi.Models;


public abstract class TemelKayit
{
  
    public int Id { get; set; }
   
    public DateTime OlusturulmaTarihi { get; protected set; } = DateTime.Now;

    public abstract string OzetBilgiGetir();
}


public abstract class Kullanici : TemelKayit
{
    public string KullaniciAdi { get; set; } = string.Empty;
    public string AdSoyad { get; set; } = string.Empty;
    
    private string _sifreHash = string.Empty;
    public string SifreHash 
    { 
        get => _sifreHash; 
        set => _sifreHash = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Şifre boş olamaz") : value; 
    }
    
    public int HataliGirisSayisi { get; set; } = 0;
    public bool HesapKilitliMi { get; set; } = false;
    public string Rol { get; set; } = string.Empty;


    public abstract string AnaSayfaGetir();

  
    public override string OzetBilgiGetir()
    {
        return $"Kullanıcı: {AdSoyad} (Rol: {Rol})";
    }
}

public class Ogrenci : Kullanici
{
    public string OgrenciNo { get; set; } = string.Empty;
    public string Bolum { get; set; } = string.Empty;
    public Ogrenci() { Rol = "Ogrenci"; }
    public override string AnaSayfaGetir() => "/Ogrenci/Index";
}

public class Admin : Kullanici
{
    public string YetkiSeviyesi { get; set; } = string.Empty;
    public Admin() { Rol = "Admin"; }
    public override string AnaSayfaGetir() => "/Admin/Index";
}

public class StajBasvurusu : TemelKayit
{
    public string OgrenciNo { get; set; } = string.Empty;
    public string AdSoyad { get; set; } = string.Empty;
    public string TcKimlik { get; set; } = string.Empty;
    public string Telefon { get; set; } = string.Empty;
    public string Uyruk { get; set; } = string.Empty;
    public string DogumTarihi { get; set; } = string.Empty;
    public string OkuduguBolum { get; set; } = string.Empty;
    public string TeslimYili { get; set; } = string.Empty;
    public int OncekiStajGunSayisi { get; set; }
    public string SaglikYardimiStatusu { get; set; } = string.Empty;
    public string Eposta { get; set; } = string.Empty;
    public string Adres { get; set; } = string.Empty;
    public string BaslangicTarihi { get; set; } = string.Empty;
    public string BitisTarihi { get; set; } = string.Empty;
    public string StajGunleri { get; set; } = string.Empty;
    public int MuhendisSayisi { get; set; }
    public string MuhendisBolumu { get; set; } = string.Empty;
    public int StajGunSayisi { get; set; }
    public string DosyaAdi { get; set; } = string.Empty;
    public string GoogleFormUrl { get; set; } = string.Empty;
    
    public string Statu { get; set; } = "İnceleniyor"; 
    public string HataMesaji { get; set; } = string.Empty;

    public override string OzetBilgiGetir()
    {
        return $"Başvuru - Öğrenci No: {OgrenciNo} | Statü: {Statu}";
    }
}