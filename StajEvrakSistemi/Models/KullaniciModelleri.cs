namespace StajEvrakSistemi.Models;

public abstract class Kullanici
{
    public int Id { get; set; }
    public string KullaniciAdi { get; set; } = string.Empty;
    
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
}

public class Ogrenci : Kullanici
{
    public Ogrenci() { Rol = "Ogrenci"; }
    public override string AnaSayfaGetir() => "/Ogrenci/Index";
}