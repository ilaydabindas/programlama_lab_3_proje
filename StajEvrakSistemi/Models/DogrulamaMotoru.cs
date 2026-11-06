namespace StajEvrakSistemi.Models;

public static class DogrulamaMotoru
{
    public static string HataKontrol(string tcKimlik, int stajGunSayisi)
    {
        if (string.IsNullOrEmpty(tcKimlik) || tcKimlik.Length != 11) 
            return "TC Kimlik No 11 haneli olmalıdır.";
            
        if (stajGunSayisi < 20) 
            return "Staj gün sayısı en az 20 iş günü olmak zorundadır.";
            
        return "Gecerli";
    }
}
