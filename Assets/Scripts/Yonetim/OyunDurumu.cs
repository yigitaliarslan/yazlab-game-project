namespace Yazlab.Yonetim
{
    /// <summary>
    /// PDF'te istenen oyun akışı ve durum diyagramına (State Diagram)
    /// karşılık gelen oyun durumları.
    /// </summary>
    public enum OyunDurumu
    {
        Baslangic,              // Oyun başladı, oyuncu engeli arıyor
        FizikselEngelAlaninda,  // Oyuncu engelle karşılaştı, yoldaş ajan emir bekliyor
        FizikselEngelAsildi,    // Yoldaş şalteri indirdi, yol açıldı
        SosyalEngelAlaninda,    // Oyuncu NPC Muhafızın yanına geldi (Diyalog aşaması)
        PrensesKurtarma,        // 4. Bölüm (Final): Kapı açıldı, Kızıl Kule'de Prenses Betül'ü kurtarma görevi
        BolumTamamlandi         // Prenses Betül kurtarıldı, operasyon başarıyla tamamlandı
    }
}
