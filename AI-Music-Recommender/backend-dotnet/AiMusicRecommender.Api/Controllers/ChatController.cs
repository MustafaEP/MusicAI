using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web; // Spotify kütüphanesi için
using System.Collections.Generic; // Listeler için
using System; // Exception ve Console için
using System.Linq; // Linq sorguları için (örn: FirstOrDefault)

namespace AiMusicRecommender.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly string? _pythonServiceUrl; // Null olabilir olarak işaretlendi

        public ChatController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            // appsettings.Development.json'dan Python servis URL'sini oku
            _pythonServiceUrl = _configuration["PythonService:BaseUrl"];
            if (string.IsNullOrEmpty(_pythonServiceUrl))
            {
                Console.WriteLine("UYARI: PythonService:BaseUrl yapılandırması bulunamadı!");
                // Hata fırlatmak yerine null bırakabiliriz, kullanım yerinde kontrol edilir.
            }
        }

        // --- DTO Modelleri ---
        public class ChatRequest
        {
            // Gelen mesajın null veya boş olabileceğini belirtelim
            public string? Message { get; set; }
        }

        public class AiResponse
        {
            // Bu alanların Python'dan null gelme ihtimali düşük ama belirtmek iyi pratik
            public string? Mood { get; set; }
            public string? Reply { get; set; }
        }

        public class SpotifyPlaylist
        {
            public string? Name { get; set; }
            public string? Url { get; set; }
            public string? ImageUrl { get; set; }
        }

        public class ChatResponse
        {
            public string? AiReply { get; set; }
            public string? DetectedMood { get; set; }
            // Liste null olabilir veya boş olabilir
            public List<SpotifyPlaylist>? Playlists { get; set; }
        }
        // --- DTO Modelleri Sonu ---


        [HttpPost]
        public async Task<IActionResult> PostMessage([FromBody] ChatRequest request)
        {
            // Gelen mesaj null veya boş ise hata döndür
            if (string.IsNullOrWhiteSpace(request?.Message))
            {
                return BadRequest("Mesaj boş olamaz.");
            }

            // Python URL'si yapılandırmada yoksa hata döndür
            if (string.IsNullOrEmpty(_pythonServiceUrl))
            {
                 return StatusCode(500, "Python servis adresi yapılandırılmamış.");
            }

            // --- 1. Python Servisine İstek Gönder ---
            AiResponse? aiResult = null; // Null olabilir olarak tanımla
            try
            {
                var pythonRequestData = new { text = request.Message }; // request.Message null olamaz (yukarıda kontrol edildi)
                var jsonContent = JsonSerializer.Serialize(pythonRequestData);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var httpClient = _httpClientFactory.CreateClient();
                var pythonSentimentUrl = $"{_pythonServiceUrl}/analyze-sentiment";
                Console.WriteLine($"Python servisine istek gönderiliyor: {pythonSentimentUrl}");
                // Console.WriteLine($"Gönderilen veri: {jsonContent}"); // İsteğe bağlı debug

                HttpResponseMessage response = await httpClient.PostAsync(pythonSentimentUrl, httpContent);

                string responseBody = await response.Content.ReadAsStringAsync(); // Yanıtı her zaman oku

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Python servisinden gelen yanıt: {responseBody}");
                    // Deserialize etmeden önce boş olup olmadığını kontrol et
                    if (string.IsNullOrWhiteSpace(responseBody))
                    {
                         Console.WriteLine("Python servisinden boş yanıt alındı.");
                         return StatusCode(500, "AI servisinden boş yanıt alındı.");
                    }
                    aiResult = JsonSerializer.Deserialize<AiResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    // aiResult veya içindeki değerler null ise hata ver
                    if (aiResult == null || string.IsNullOrWhiteSpace(aiResult.Mood) || string.IsNullOrWhiteSpace(aiResult.Reply))
                    {
                         Console.WriteLine("Python servisinden beklenen formatta yanıt alınamadı.");
                         return StatusCode(500, "AI servisinden geçerli yanıt alınamadı.");
                    }
                }
                else
                {
                    Console.WriteLine($"Python servisinden hata yanıtı: {response.StatusCode} - {responseBody}");
                    return StatusCode((int)response.StatusCode, $"Python servisinden hata alındı: {responseBody}");
                }
            }
            catch (HttpRequestException httpEx)
            {
                 Console.WriteLine($"Python servisine bağlanırken hata: {httpEx.Message}");
                return StatusCode(503, $"Duygu analizi servisine ulaşılamıyor: {httpEx.Message}");
            }
            catch (JsonException jsonEx)
            {
                 Console.WriteLine($"Python yanıtını işlerken JSON hatası: {jsonEx.Message}");
                return StatusCode(500, "AI servisinden gelen yanıt işlenemedi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Beklenmedik hata (Python çağrısı): {ex.ToString()}"); // Tam hata detayını yazdır
                return StatusCode(500, "İstek işlenirken bir hata oluştu.");
            }

            // aiResult null olamaz (yukarıdaki kontrollerden geçti)
            // Bu noktada aiResult.Mood ve aiResult.Reply null veya boş olamaz.

            // --- 2. Spotify API'sine İstek Gönder ---
            List<SpotifyPlaylist> recommendedPlaylists = new List<SpotifyPlaylist>();
            try
            {
                var spotifyClientId = _configuration["Spotify:ClientId"];
                var spotifyClientSecret = _configuration["Spotify:ClientSecret"];

                if (string.IsNullOrEmpty(spotifyClientId) || string.IsNullOrEmpty(spotifyClientSecret))
                {
                    Console.WriteLine("UYARI: Spotify ClientId veya ClientSecret yapılandırması eksik! Öneri yapılamayacak.");
                    // Hata döndürmek yerine boş listeyle devam edebiliriz.
                }
                else
                {
                    var config = SpotifyClientConfig.CreateDefault();
                    var requestToken = new ClientCredentialsRequest(spotifyClientId, spotifyClientSecret);
                    var responseToken = await new OAuthClient(config).RequestToken(requestToken);
                    var spotifyClient = new SpotifyClient(responseToken.AccessToken);

                    var searchQuery = $"{aiResult.Mood} playlist"; // aiResult.Mood null olamaz
                    Console.WriteLine($"Spotify'da arama yapılıyor: '{searchQuery}'");

                    var searchRequest = new SearchRequest(SearchRequest.Types.Playlist, searchQuery);
                    // ----> HATA DÜZELTME: Limit'i özellik olarak ata <----
                    searchRequest.Limit = 5;

                    // Aramayı gerçekleştir
                    var searchResponse = await spotifyClient.Search.Item(searchRequest);

                    if (searchResponse.Playlists?.Items != null) // Null kontrolü eklendi
                    {
                        foreach (var item in searchResponse.Playlists.Items)
                        {
                            // item null olabilir mi? Kontrol eklemek iyi olabilir.
                            if (item == null) continue;

                            recommendedPlaylists.Add(new SpotifyPlaylist
                            {
                                Name = item.Name, // item.Name null olabilir mi?
                                Url = item.ExternalUrls?.ContainsKey("spotify") == true ? item.ExternalUrls["spotify"] : null, // Null kontrolü
                                ImageUrl = item.Images?.FirstOrDefault()?.Url // İlk resmi al, yoksa null
                            });
                            Console.WriteLine($"- Bulunan Çalma Listesi: {item.Name ?? "İsimsiz"}");
                        }
                    }
                    else
                    {
                         Console.WriteLine("Spotify'da uygun çalma listesi bulunamadı.");
                    }
                } // else bloğu (ClientId/Secret varsa) sonu
            }
            catch (APIException apiEx)
            {
                Console.WriteLine($"Spotify API hatası: {apiEx.Message}");
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"Spotify ile iletişimde beklenmedik hata: {ex.ToString()}"); // Tam hata detayı
            }

            // --- 3. Son Yanıtı Oluştur ve Döndür ---
            var finalResponse = new ChatResponse
            {
                AiReply = aiResult.Reply, // aiResult.Reply null olamaz
                DetectedMood = aiResult.Mood, // aiResult.Mood null olamaz
                Playlists = recommendedPlaylists.Any() ? recommendedPlaylists : null // Liste boşsa null gönderilebilir (isteğe bağlı)
            };

            return Ok(finalResponse);
        }
    }
}