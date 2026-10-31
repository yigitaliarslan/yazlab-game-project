using System;

namespace Yazlab.Yonetim
{
    /// <summary>
    /// Muhafızın diyalog sırasındaki duygusal ve mantıksal tepki kategorisi.
    /// YazLab-I Proje İsterleri (Bölüm 4.4): En az 5 farklı diyalog senaryosunu sınıflandırır.
    /// </summary>
    public enum MuhafizTepkiTuru
    {
        SupheciRed = 0,       // Standart şüpheci nöbetçi reddi ("Neden seni geçireyim?")
        IknaEdici = 1,        // Mantıklı argüman, parola, belge veya rüşvetle güven kazandıran adım
        YetersizTeklif = 2,   // Çok düşük rüşvet veya zayıf bahane ("10 altınla beni mi kandıracaksın?")
        AlakasizKonusma = 3,  // Konu dışı / saçma konuşma ("Burası sohbet yeri değil, nöbetteyim!")
        SaldirganKizdiran = 4 // Hakaret, tehdit veya aşırı şüphe uyandıran düşmanca tavır -> SAVAŞ TETİKLER!
    }

    /// <summary>
    /// LLM'in ürettiği yapılandırılmış (structured JSON) çıktının ayrıştırıldığı veri modeli (DTO).
    /// YazLab-I Proje İsterleri (Bölüm 3.2 & 4.2): Kırılgan serbest metin eşleştirmesi yerine
    /// oyun durumunu değiştirecek veriler (kapı açma, savaş tetikleme, güven değişimi) bu sınıfla ayrıştırılır.
    /// </summary>
    [Serializable]
    public class MuhafizKararVerisi
    {
        public string cevap;
        public string tepkiTuru;     // "SupheciRed", "IknaEdici", "YetersizTeklif", "AlakasizKonusma", "SaldirganKizdiran"
        public int guvenDegisimi;    // -35 ile +35 arası kademeli güven puanı değişimi
        public bool kapiyiAc;        // Sadece çok aşamalı ikna tamamlandığında true olur
        public bool savasTetikle;    // Oyuncu hakaret/tehdit ettiğinde veya şüphe sınırı aşıldığında true olur

        public MuhafizTepkiTuru GetTepkiEnum()
        {
            if (string.IsNullOrEmpty(tepkiTuru)) return MuhafizTepkiTuru.SupheciRed;
            string t = tepkiTuru.Trim().ToLowerInvariant();
            if (t.Contains("saldir") || t.Contains("kizdir") || t.Contains("tehdit") || t.Contains("hostile"))
                return MuhafizTepkiTuru.SaldirganKizdiran;
            if (t.Contains("alakasiz") || t.Contains("irrelevant") || t.Contains("sacma"))
                return MuhafizTepkiTuru.AlakasizKonusma;
            if (t.Contains("yetersiz") || t.Contains("weak") || t.Contains("az"))
                return MuhafizTepkiTuru.YetersizTeklif;
            if (t.Contains("ikna") || t.Contains("persua") || t.Contains("olumlu"))
                return MuhafizTepkiTuru.IknaEdici;
            return MuhafizTepkiTuru.SupheciRed;
        }
    }

    /// <summary>
    /// OOP Prensipleri (SOLID - Dependency Inversion & Interface Segregation) kapsamında
    /// Yerel Dil Modeli (Local LLM) servislerini soyutlayan arayüz.
    /// </summary>
    public interface IYerelDilModeliServisi
    {
        string AktifModelAdi { get; }
        bool IstekDevamEdiyorMu { get; }
        void SohbetiSifirla();
        void MesajGonderAsenkron(
            string oyuncuMesaji,
            int mevcutGuvenSkoru,
            int iknaAdimSayisi,
            Action<MuhafizKararVerisi> onBasarili,
            Action<string> onHata);
    }
}
