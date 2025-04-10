def analyze_sentiment(message: str) -> str:
    message = message.lower()

    if any(word in message for word in ["harika", "mutlu", "iyi", "süper", "enerjik"]):
        return "mutlu"
    elif any(word in message for word in ["üzgün", "mutsuz", "canım sıkkın", "yalnız"]):
        return "üzgün"
    elif any(word in message for word in ["stres", "bunaldım", "sıkıldım", "gergin"]):
        return "stresli"
    elif any(word in message for word in ["yorgun", "bitkin", "uykusuz"]):
        return "yorgun"
    else:
        return "nötr"
