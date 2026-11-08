"""Analizlerden çıkan hedef değerler (kaynak: tür araştırması).

Resim referansı (3117 resim, 570 bölüm simülasyonu):
- İnsana benzer bot (%25 rastgele hamle) kazanma oranı zorluk etiketiyle düzenli düşer: Easy %94, Medium %86,
  Hard %83, Very Hard %75, Ultra Hard %60. Resim ölçümlerinden daha güçlü zorluk göstergesi (Spearman −0,34).
- Zorluk arttıkça: daha çok renk bölgesi (Easy 43 → VH 112), daha küçük bölgeler, daha dengesiz renkler (aksan
  renkleri, en çok/en az 4× → 16×), daha çok kilitli renk (1 → 3,5), daha çok silüet (%32 → %58). Başta kenardan
  açık renk sayısı her zorlukta ~4.
- İlk 25 bölüm desen ağırlıklı, sonra karakter/nesne; en büyük renk payı ~%30–34.
Tahta referansı (1701 tahta):
- İlk 10 bölüm öğretici: 5×6, 6 blok, %59 doluluk, başta 2 serbest blok, 3 katman, yer değiştirme yok.
- 11+ sıkışıklık bulmacası: başta serbest blok 0, 9–14 katman, 8–12 yer değiştirme; çok zorda 8×10, 20 blok.
- Her renge tam bir kapı; kapı genişliği 2 (%50), 1 (%27), 3 (%23); şekiller 2'li %36, tekli %26, kare %10, L3 %10.
Bizim oyunda tahta yörünge ve resimle birlikte zorluk yarattığı için referans sıkışıklığı yumuşatılarak alınır.
"""

TIERS = ['Kolay', 'Orta', 'Zor', 'Çok Zor']

# Oynanış hedefleri (bizim simülatör, insana benzer bot %25 rastgele, 20 deneme). (en az, en çok)
# 2026-10-28 oyuncu geri bildirimi: bölümler her zorlukta fazla kolaydı (bot oyuncudan zayıf oynuyor); bantlar aşağı çekildi.
# 2026-10-28 (2): 101–107 denemesinden sonra biraz daha zor.
WIN_RATE = {'Kolay': (0.72, 0.88), 'Orta': (0.56, 0.75), 'Zor': (0.40, 0.60), 'Çok Zor': (0.28, 0.46)}
# Slot: referansta usta bot bile Easy bölümlerde slotları 5'e kadar dolduruyor (medyan 5); slot doluluğu zorluk
# göstergesi değil, oyunun normal gerilimi. Zor/Çok Zor'da slotların dolması tercih edilir (gerilim, "az kalsın" anı).
# Zorlukla belirgin artan: usta botun zorunlu hamleleri (Easy 13,8 → Medium 47,8 → Hard 73,3 → Very Hard 81,8).
SLOT_PEAK = {'Kolay': (0.0, 5.0), 'Orta': (0.0, 5.0), 'Zor': (3.0, 5.0), 'Çok Zor': (4.0, 5.0)}
# Gerçekçi oyuncu tipleriyle (sim.HUMAN_PROFILES, süre sınırıyla) ağırlıklı kazanma: zorluk sırasını doğrular.
HUMAN_WIN = {'Kolay': (0.65, 0.90), 'Orta': (0.50, 0.78), 'Zor': (0.35, 0.62), 'Çok Zor': (0.22, 0.50)}
# Acemi oyuncunun süre yüzünden kaybetme oranı üst sınırı (süre baskısı var ama asıl zorluk tahtada).
NOVICE_TIME_FAIL_MAX = {'Kolay': 0.12, 'Orta': 0.18, 'Zor': 0.25, 'Çok Zor': 0.30}
SECONDS = {'Kolay': (10, 60), 'Orta': (20, 80), 'Zor': (30, 100), 'Çok Zor': (40, 130)}

# Tahta hedefleri (sürükleme modeli). Yoğunluk 0..1 arasında aralık içinde konum seçer.
BOARD = {
    # Doluluk referansa yaklaştırıldı (11–100: normal %69–73, zor %75–77; başta serbest blok 0–0,5).
    'Kolay':   dict(first_free=(1, 3), layers=(3, 6), shuffles=(0, 1), fill=(0.62, 0.72)),
    'Orta':    dict(first_free=(1, 2), layers=(4, 8), shuffles=(0, 3), fill=(0.68, 0.78)),
    'Zor':     dict(first_free=(0, 1), layers=(5, 10), shuffles=(1, 5), fill=(0.72, 0.82)),
    'Çok Zor': dict(first_free=(0, 1), layers=(6, 12), shuffles=(2, 7), fill=(0.75, 0.86)),
}

# Resim hedefleri (referans verisi; 2597 ana bölüm). Referansta resmin biçimi bölüm
# aralığına bağlı, zorluğa neredeyse bağlı değil: aynı aralıkta Çok Zor resim Kolay'a göre +1 renk, +%18–30 bölge,
# +0,5–1 kilitli renk; silüet, simetri, siyah ve gölge değişmiyor. Zorluğu tahta taşır.
# Aralık başına: bölge ortalama boyutu (piksel), küçük detay üst sınırı, en büyük renk payı, en çok/en az renk oranı,
# başta açık renk alt sınırı, kilitli renk, aksan rengi (payı <%5) alt sınırı, sağ-sol simetri alt sınırı.
PICTURE_RANGES = [
    (10, dict(region_avg=20, tiny_max=0.30, top_share=0.45, balance=2, start_min=2, locked=1, accents=0, symmetry=0.70)),
    (25, dict(region_avg=9.5, tiny_max=0.65, top_share=0.35, balance=4, start_min=4, locked=1, accents=0, symmetry=0.70)),
    (100, dict(region_avg=7.5, tiny_max=0.65, top_share=0.35, balance=7, start_min=4, locked=1, accents=0, symmetry=0.70)),
    (400, dict(region_avg=8.0, tiny_max=0.65, top_share=0.32, balance=11, start_min=4, locked=2, accents=1, symmetry=0.0)),
    (10 ** 9, dict(region_avg=8.5, tiny_max=0.65, top_share=0.32, balance=11, start_min=5, locked=2, accents=1, symmetry=0.0)),
]
# Zorluğun resme etkisi (aynı aralıktaki Kolay'a göre): renk eki, bölge çarpanı, kilitli renk eki (0,5 → yarı yarıya).
TIER_PICTURE = {'Kolay': (0, 1.00, 0.0), 'Orta': (1, 1.18, 0.5), 'Zor': (1, 1.25, 0.5), 'Çok Zor': (1, 1.30, 1.0)}
# Desenler: büyük düzgün alanlar.
PATTERN_REGION_AVG = 13
PATTERN_TINY_MAX = 0.20
