using System.Net.Http; // HttpClientFactory için zaten olmalı
using Microsoft.Extensions.DependencyInjection; // AddHttpClient için zaten olmalı
using Microsoft.AspNetCore.Builder; // WebApplication için zaten olmalı
using Microsoft.Extensions.Hosting; // IsDevelopment için zaten olmalı
using Microsoft.Extensions.Configuration; // Configuration için zaten olmalı
using System; // Console için zaten olmalı

var builder = WebApplication.CreateBuilder(args);

// --- CORS Politikası Tanımı ---
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins"; // Politikaya bir isim verelim

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          policy.WithOrigins("http://localhost:5173", // React uygulamasının çalıştığı adres (Vite varsayılanı)
                                             "http://127.0.0.1:5173") // Bazen localhost yerine bu kullanılır
                                .AllowAnyHeader() // Gelen tüm header'lara izin ver (Content-Type dahil)
                                .AllowAnyMethod(); // Tüm HTTP metodlarına izin ver (POST dahil)
                          // Eğer React uygulaman farklı bir portta çalışıyorsa, o adresi de ekle
                          // Örneğin Create React App varsayılanı için: .WithOrigins("http://localhost:3000", "http://127.0.0.1:3000")
                      });
});
// --- CORS Politikası Tanımı Sonu ---


// Add services to the container.
builder.Services.AddControllers();

// HttpClientFactory'yi yapılandır (Bu zaten vardı)
builder.Services.AddHttpClient();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // Geliştirme ortamında Python servis URL'sini appsettings.Development.json'dan al
    var pythonBaseUrl = app.Configuration["PythonService:BaseUrl"];
    Console.WriteLine($"Development Mode: Python Service URL = {pythonBaseUrl ?? "Not Found!"}");
}
else
{
     // Yayın ortamında ortam değişkeninden veya başka bir yapılandırmadan al
     var pythonBaseUrl = Environment.GetEnvironmentVariable("PYTHON_SERVICE_URL") ?? app.Configuration["PythonService:BaseUrl"];
     Console.WriteLine($"Production Mode: Python Service URL = {pythonBaseUrl ?? "Not Found!"}");
     // HTTPS yönlendirmesi ve HSTS genellikle yayın ortamında etkinleştirilir
     app.UseHttpsRedirection();
     // app.UseHsts(); // Gerekirse etkinleştir
}


app.UseRouting(); // UseRouting, UseCors'tan önce gelmeli

// --- CORS Middleware'ini Uygula ---
app.UseCors(MyAllowSpecificOrigins); // Tanımladığımız politikayı kullan
// --- CORS Middleware'ini Uygula Sonu ---


app.UseAuthorization(); // Eğer yetkilendirme kullanıyorsan

app.MapControllers(); // Bu zaten vardı

app.Run();