import React, { useState, useEffect, useRef } from 'react';
import './App.css'; // Boş CSS dosyamız

function App() {
  // Mesajları tutacak state (dizi)
  const [messages, setMessages] = useState([]);
  // Kullanıcının yazdığı mesajı tutacak state
  const [input, setInput] = useState('');
  // Backend'den yanıt beklenirken yükleme durumunu tutacak state
  const [isLoading, setIsLoading] = useState(false);

  // Mesaj listesinin sonuna otomatik kaydırma için ref
  const messagesEndRef = useRef(null);

  // Mesaj listesi her güncellendiğinde en sona kaydır
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  // Mesaj gönderme fonksiyonu
  const handleSend = async () => {
    const userMessage = input.trim();
    if (!userMessage) return;

    setMessages(prev => [...prev, { sender: 'user', text: userMessage }]);
    setInput('');
    setIsLoading(true);

    try {
      // --- Backend API Çağrısı ---
      const backendUrl = 'http://localhost:5092/api/chat'; // .NET backend adresin (Port numarasını kontrol et!)

      const response = await fetch(backendUrl, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ Message: userMessage }), // Backend'in beklediği formatta gönder
      });

      if (!response.ok) {
        // HTTP hata durumu (4xx, 5xx)
        const errorData = await response.json().catch(() => ({})); // Hata mesajını JSON olarak almaya çalış
        console.error("API Hata Yanıtı:", response.status, errorData);
        throw new Error(`API hatası: ${response.status} ${response.statusText}. ${errorData?.title || errorData?.message || ''}`);
      }

      // Başarılı yanıt (2xx)
      const botResponse = await response.json(); // Yanıtı JSON olarak parse et

      console.log("API Başarılı Yanıt:", botResponse);

      // Botun yanıtını ve önerileri mesaj listesine ekle
      // Backend'den gelen alan adlarının (aiReply, detectedMood, playlists)
      // ChatResponse DTO'muz ile eşleştiğinden emin olalım.
      setMessages(prev => [
        ...prev,
        {
          sender: 'bot',
          text: botResponse.aiReply || "Bir yanıt aldım ama içerik boş.", // Null kontrolü
          mood: botResponse.detectedMood || "bilinmiyor", // Null kontrolü
          playlists: botResponse.playlists || [] // Null ise boş dizi ata
        }
      ]);
      // --------------------------

    } catch (error) {
      console.error("API çağrısı sırasında hata:", error);
      // Hata mesajını kullanıcıya göster
      setMessages(prev => [...prev, {
          sender: 'bot',
          // error.message daha açıklayıcı olabilir
          text: `Üzgünüm, bir hata oluştu: ${error.message || 'Sunucuyla iletişim kurulamadı.'}`,
          isError: true
      }]);
    } finally {
      setIsLoading(false);
    }
  };

  // Enter tuşu ile mesaj gönderme
  const handleKeyPress = (event) => {
    if (event.key === 'Enter' && !isLoading) {
      handleSend();
    }
  };

  return (
    <div className="chat-container">
      <div className="messages-list">
        {messages.map((msg, index) => (
          <div key={index} className={`message ${msg.sender}`}>
            <p>{msg.text}</p>
            {/* Bot mesajıysa ve çalma listesi varsa göster */}
            {msg.sender === 'bot' && msg.playlists && msg.playlists.length > 0 && (
              <div className="playlists">
                <h4>İşte sana özel çalma listeleri ({msg.mood}):</h4>
                <ul>
                  {msg.playlists.map((pl, plIndex) => (
                    <li key={plIndex}>
                      <img src={pl.imageUrl || 'https://via.placeholder.com/40'} alt={pl.name || 'Playlist Cover'} width="40" height="40" />
                      <a href={pl.url || '#'} target="_blank" rel="noopener noreferrer">{pl.name || 'Bilinmeyen Playlist'}</a>
                    </li>
                  ))}
                </ul>
              </div>
            )}
             {/* Hata mesajıysa farklı stil uygula (CSS'de tanımlanacak) */}
             {msg.isError && <span className="error-indicator">⚠️</span>}
          </div>
        ))}
        {/* Yükleme göstergesi */}
        {isLoading && <div className="message bot loading"><span>.</span><span>.</span><span>.</span></div>} {/* Yükleme animasyonu için span'ları ayırdım */}
        {/* Mesaj listesinin sonuna referans */}
        <div ref={messagesEndRef} />
      </div>
      <div className="input-area">
        <input
          type="text"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyPress={handleKeyPress}
          placeholder="Nasıl hissediyorsun?"
          disabled={isLoading} // Yüklenirken input'u devre dışı bırak
        />
        <button onClick={handleSend} disabled={isLoading}>
          {isLoading ? '...' : 'Gönder'}
        </button>
      </div>
    </div>
  );
}

export default App;