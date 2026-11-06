using StajEvrakSistemi.Data;

var builder = WebApplication.CreateBuilder(args);

// Controller ve View desteği
builder.Services.AddControllersWithViews();

// Oturum (Session) yönetimi
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

var app = builder.Build();

// Veritabanı tablolarını ve varsayılan kullanıcıları başlatan satır:
VeritabaniServisi.Baslat();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Session yetkilendirmeden önce çalışmalıdır
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Hesap}/{action=Giris}/{id?}");

app.Run();