# 1. Standard Imports
import os
import json # JSON parse etmek için eklendi

# 2. Third-party Imports
from dotenv import load_dotenv
from flask import Flask, request, jsonify
# import nltk # VADER artık kullanılmıyor
from openai import OpenAI

# 3. Load Environment Variables
load_dotenv()

# 4. Initialize Global Objects
app = Flask(__name__)

# --- OpenAI İstemcisini Başlat ---
openai_api_key = None
client = None
try:
    openai_api_key = os.getenv("OPENAI_API_KEY")
    if not openai_api_key:
        raise ValueError("HATA: OPENAI_API_KEY ortam değişkeni bulunamadı!")
    client = OpenAI(api_key=openai_api_key)
    print(">>> OpenAI istemcisi başarıyla başlatıldı.")
except Exception as e:
    print(f"HATA: OpenAI istemcisi başlatılamadı! {e}")
# ---------------------------------

# 5. Define Flask Routes
@app.route('/')
def home():
    return "Python AI Servisi Çalışıyor! (OpenAI Entegreli)"

@app.route('/analyze-sentiment', methods=['POST'])
def analyze_sentiment_with_openai():
    # OpenAI istemcisi başlatılabildi mi kontrol et
    if not client:
         return jsonify({"error": "OpenAI servisine bağlanılamadı."}), 503

    # İstekten veriyi al
    data = request.get_json()
    if not data or 'text' not in data:
        return jsonify({"error": "İstekte 'text' alanı bulunamadı."}), 400

    user_message = data['text']

    # OpenAI API Çağrısı
    try:
        system_prompt = """Sen bir müzik ve podcast öneri asistanısın. Kullanıcının mesajını analiz ederek onun genel ruh halini (örneğin: mutlu, üzgün, enerjik, sakin, stresli, nötr vb.) tek kelimeyle belirle ve ona kısa, empatik bir yanıt ver. Yanıtını şu JSON formatında ver: {"mood": "belirlenen_ruh_hali", "reply": "kısa_empatik_yanıt"}"""
        print(f">>> OpenAI'a gönderilen mesaj: {user_message}")

        completion = client.chat.completions.create(
            model="gpt-4o-mini",
            messages=[
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_message}
            ],
            response_format={ "type": "json_object" },
            temperature=0.7
        )

        ai_response_content = completion.choices[0].message.content
        print(f">>> OpenAI'dan gelen ham yanıt (string): {ai_response_content}")

        # Gelen JSON string'ini Python dictionary'sine çevir
        try:
            ai_response_json = json.loads(ai_response_content)
        except json.JSONDecodeError:
            print(f">>> HATA: OpenAI yanıtı geçerli bir JSON değil! Yanıt: {ai_response_content}")
            # Fallback yanıtı veya daha spesifik hata yönetimi
            return jsonify({"mood": "bilinmiyor", "reply": "Yapay zekadan geçerli bir yanıt alınamadı."}), 500

        # Beklenen anahtarlar var mı kontrol et
        if not isinstance(ai_response_json, dict) or "mood" not in ai_response_json or "reply" not in ai_response_json:
             print(f">>> HATA: OpenAI yanıtı beklenen JSON formatında değil! Yanıt: {ai_response_json}")
             # Fallback yanıtı
             return jsonify({"mood": "bilinmiyor", "reply": "Mesajını aldım, ancak yanıt formatı beklenenden farklı."})

        print(f">>> Belirlenen ruh hali: {ai_response_json.get('mood')}")
        print(f">>> AI Yanıtı: {ai_response_json.get('reply')}")

        # OpenAI'dan gelen parse edilmiş JSON yanıtını doğrudan döndür
        return jsonify(ai_response_json)

    except Exception as e:
        # Hata detaylarını loglamak önemlidir
        import traceback
        print(f"HATA: OpenAI API çağrısı sırasında beklenmedik hata oluştu: {e}")
        print(traceback.format_exc()) # Hatanın tam izini yazdır
        return jsonify({"error": f"OpenAI API ile iletişimde hata: {str(e)}"}), 500

# 6. Main execution block (Script doğrudan çalıştırıldığında çalışır)
# Bu bloğun en dış seviyede (girintisiz) olması GEREKİR
if __name__ == '__main__':
    # İçindeki kodun bir seviye girintili olması GEREKİR (4 boşluk)
    app.run(debug=True, port=5001)