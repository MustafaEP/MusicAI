from fastapi import FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from openai import OpenAI
import os
from dotenv import load_dotenv

# Ortam değişkenlerini yükle (.env dosyasından)
load_dotenv()

# OpenAI istemcisi oluşturuluyor
client = OpenAI()

# FastAPI uygulaması
app = FastAPI()

# CORS ayarları (frontend'le iletişim için)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Gerekirse sadece frontend URL'ini yaz
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Gelen istek modeli
class MessageRequest(BaseModel):
    message: str

# Ana sohbet endpoint'i
@app.post("/chat")
async def chat(request: MessageRequest):
    try:
        response = client.chat.completions.create(
            model="gpt-4o",
            messages=[
                {"role": "system", "content": "Sen bir empatik ve arkadaş canlısı asistan botsun."},
                {"role": "user", "content": request.message}
            ],
            max_tokens=100
        )
        return {"response": response.choices[0].message.content}

    except Exception as e:
        return {"error": str(e)}
