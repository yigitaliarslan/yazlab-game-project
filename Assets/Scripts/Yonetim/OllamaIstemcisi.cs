using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Yazlab.Yonetim
{
    [Serializable]
    public class OllamaMesaj
    {
        public string role;
        public string content;

        public OllamaMesaj(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }

    [Serializable]
    public class OllamaSecenekler
    {
        public float temperature = 0.45f; // Tutarlı JSON ve sıkı karakter kontrolü için dengeli sıcaklık
        public int num_predict = 240;
    }

    [Serializable]
    public class OllamaChatIstegi
    {
        public string model;
        public List<OllamaMesaj> messages;
        public bool stream = false;
        public string format = "json";
        public OllamaSecenekler options = new OllamaSecenekler();
    }

    [Serializable]
    public class OllamaChatYaniti
    {
        public OllamaMesaj message;
        public bool done;
    }

    /// <summary>
    /// Kocaeli Üniversitesi YazLab-I Proje İsterlerine (Bölüm 3.2, 4.1, 4.2, 4.3, 4.4) %100 uyumlu
    /// Yerel Ollama LLM İstemcisi (IYerelDilModeliServisi implementasyonu).
    /// - Asla ilk mesajda kolayca ikna olmaz; aşamalı sorgulama (kimlik/parola + kanıt/rüşvet) uygular.
    /// - Yapılandırılmış JSON şeması ile oyun durumunu (Kapı Açma VEYA Savaş Tetikleme) kontrol eder.
    /// - Sunucu kapalıysa, zaman aşımı olursa veya bozuk JSON gelirse oyunu ASLA çökertmez (Madde 4.3).
    /// </summary>
    public class OllamaIstemcisi : MonoBehaviour, IYerelDilModeliServisi
    {
        [Header("Ollama Yerel Sunucu Ayarları")]
        [Tooltip("Yerel Ollama Chat API uç noktası.")]
        [SerializeField] private string apiUrl = "http://localhost:11434/api/chat";

        [Tooltip("Bilgisayarda yüklü yerel model (Örn: llama3.2:latest).")]
        [SerializeField] private string modelAdi = "llama3.2:latest";

        [Tooltip("HTTP istek zaman aşımı süresi (saniye).")]
        [SerializeField] private int zamanAsimiSaniye = 30;

        [Header("Muhafız Karakter Kimliği (System Prompt)")]
        [TextArea(10, 18)]
        [SerializeField] private string sistemYonergesi =
            "Sen 'Komutan Varg' adında, kalenin 2. katındaki gizli çıkış kapısını koruyan çok sert, şüpheci ve disiplinli bir Kıdemli Kale Muhafızısın.\n" +
            "GÖREVİN VE KİŞİLİĞİN:\n" +
            "1. ASLA İLK MESAJDA VEYA TEK BİR CÜMLEYLE İKNA OLMA! İlk mesajda `kapiyiAc` KESİNLİKLE `false` olmalıdır.\n" +
            "2. Oyuncuyu en az 2-3 aşamada sorgula:\n" +
            "   - Önce kim olduğunu, bu yasak kuleye nasıl girdiğini ve gece nöbet parolasını ('Kızıl Şahin' veya 'Gece Kuzgunu') ya da hangi komutandan emir aldığını sor.\n" +
            "   - Eğer rüşvet teklif ederse, 50-100 altın gibi küçük rakamları aşağılayarak reddet ('Beni dilenci mi sandın!'). En az 500+ altın veya kraliyet mührü/belgesi iste ve parayı neden verdiğini sorgula.\n" +
            "3. 5 FARKLI SENARYOYA GÖRE DAVRAN:\n" +
            "   - [SaldirganKizdiran]: Oyuncu sana hakaret ederse, tehdit ederse ('seni öldürürüm', 'çekil yolumdan aptal' vb.) veya silah çekerse öfkelen, `tepkiTuru: \"SaldirganKizdiran\"`, `guvenDegisimi: -35`, `savasTetikle: true`, `kapiyiAc: false` yap!\n" +
            "   - [AlakasizKonusma]: Oyuncu hava durumu, futbol, yemek, şaka gibi alakasız şeyler söylerse sertçe azarlayıp konuya dönmesini söyle (`tepkiTuru: \"AlakasizKonusma\"`, `guvenDegisimi: -10`, `kapiyiAc: false`).\n" +
            "   - [YetersizTeklif]: Oyuncu kanıtsız 'ben dostum', 'kapıyı aç lütfen' veya az altın söylerse şüphelen (`tepkiTuru: \"YetersizTeklif\"`, `guvenDegisimi: -5`, `kapiyiAc: false`).\n" +
            "   - [IknaEdici]: Oyuncu mantıklı bir askeri istihbarat gerekçesi, parola ('Kızıl Şahin'), Başkomutan emri veya yüklü altın kesesi sunarsa ilgilen ama hemen açma, bir detay/kanıt daha sor (`tepkiTuru: \"IknaEdici\"`, `guvenDegisimi: +25`).\n" +
            "   - Sadece oyuncu en az 2 kez tutarlı/ikna edici yanıt verdiğinde ve toplam güven 80'i geçtiğinde `kapiyiAc: true` yap.\n\n" +
            "ÇOK ÖNEMLİ: Yanıtını SADECE aşağıdaki geçerli JSON şemasında ver, başka hiçbir metin ekleme:\n" +
            "{\n" +
            "  \"cevap\": \"Komutan Varg olarak oyuncuya verdiğin Türkçe, karakterine uygun, sorgulayıcı yanıt (1-3 cümle)\",\n" +
            "  \"tepkiTuru\": \"SupheciRed\" veya \"IknaEdici\" veya \"YetersizTeklif\" veya \"AlakasizKonusma\" veya \"SaldirganKizdiran\",\n" +
            "  \"guvenDegisimi\": -35 ile +30 arası tam sayı,\n" +
            "  \"kapiyiAc\": false,\n" +
            "  \"savasTetikle\": false\n" +
            "}";

        private readonly List<OllamaMesaj> _konusmaGecmisi = new List<OllamaMesaj>();
        private bool _istekDevamEdiyor = false;

        public string AktifModelAdi => modelAdi;
        public bool IstekDevamEdiyorMu => _istekDevamEdiyor;

        private void Awake()
        {
            SohbetiSifirla();
        }

        public void SohbetiSifirla()
        {
            _konusmaGecmisi.Clear();
            _konusmaGecmisi.Add(new OllamaMesaj("system", sistemYonergesi));
            _istekDevamEdiyor = false;
        }

        public void MesajGonderAsenkron(
            string oyuncuMesaji,
            int mevcutGuvenSkoru,
            int iknaAdimSayisi,
            Action<MuhafizKararVerisi> onBasarili,
            Action<string> onHata)
        {
            if (_istekDevamEdiyor) return;
            StartCoroutine(MesajGonderCoroutine(oyuncuMesaji, mevcutGuvenSkoru, iknaAdimSayisi, onBasarili, onHata));
        }

        private IEnumerator MesajGonderCoroutine(
            string oyuncuMesaji,
            int mevcutGuvenSkoru,
            int iknaAdimSayisi,
            Action<MuhafizKararVerisi> onBasarili,
            Action<string> onHata)
        {
            _istekDevamEdiyor = true;

            // Modele mevcut oyun durumunu ve kaçıncı ikna adımında olduğumuzu bağlam (context) olarak veriyoruz
            string durumBaglami =
                $"[MEVCUT OYUN DURUMU: GuvenSkoru={mevcutGuvenSkoru}/100, BasariliIknaAdimi={iknaAdimSayisi}/2. " +
                (iknaAdimSayisi < 1
                    ? "DİKKAT: Bu henüz ilk aşama! Oyuncu ne kadar iyi konuşursa konuşsun `kapiyiAc: false` olmalı ve ona ikinci bir doğrulama sorusu (parola, mühür, altının kaynağı veya komutanın adı) sormalısın!]"
                    : "Oyuncu önceki aşamayı geçti. Eğer bu mesajı da çok ikna ediciyse ve toplam güven 80'e ulaşıyorsa `kapiyiAc: true` yapabilirsin.]") +
                $"\nOyuncunun Sözü: \"{oyuncuMesaji}\"";

            _konusmaGecmisi.Add(new OllamaMesaj("user", durumBaglami));

            OllamaChatIstegi istekVerisi = new OllamaChatIstegi
            {
                model = modelAdi,
                messages = _konusmaGecmisi,
                stream = false,
                format = "json"
            };

            string jsonGovde = JsonUtility.ToJson(istekVerisi);
            byte[] hamVeri = Encoding.UTF8.GetBytes(jsonGovde);

            using (UnityWebRequest istek = new UnityWebRequest(apiUrl, "POST"))
            {
                istek.uploadHandler = new UploadHandlerRaw(hamVeri);
                istek.downloadHandler = new DownloadHandlerBuffer();
                istek.SetRequestHeader("Content-Type", "application/json");
                istek.timeout = zamanAsimiSaniye;

                yield return istek.SendWebRequest();

                _istekDevamEdiyor = false;

                // HATA YÖNETİMİ 1: Sunucu kapalı, bağlantı reddedildi veya zaman aşımı (Madde 4.3)
                if (istek.result != UnityWebRequest.Result.Success)
                {
                    if (_konusmaGecmisi.Count > 1)
                    {
                        _konusmaGecmisi.RemoveAt(_konusmaGecmisi.Count - 1);
                    }

                    string detay = istek.error ?? "Bilinmeyen ağ hatası";
                    onHata?.Invoke(
                        $"Yerel Ollama sunucusuna ({apiUrl}) ulaşılamadı veya zaman aşımı oluştu.\n" +
                        $"Teknik Detay: {detay} (HTTP {istek.responseCode}).\n" +
                        $"Oyun akışı korunmaktadır; lütfen arka planda 'ollama serve' ve '{modelAdi}' modelinin aktif olduğunu doğrulayıp tekrar deneyin.");
                    yield break;
                }

                string gelenHamJson = istek.downloadHandler.text;

                // HATA YÖNETİMİ 2: Boş veya bozuk JSON yanıtlarına karşı dayanıklı ayrıştırma (Madde 4.2 & 4.3)
                try
                {
                    OllamaChatYaniti disYanit = JsonUtility.FromJson<OllamaChatYaniti>(gelenHamJson);
                    if (disYanit == null || disYanit.message == null || string.IsNullOrWhiteSpace(disYanit.message.content))
                    {
                        onHata?.Invoke("Ollama modeli boş bir yanıt üretti. Lütfen cümlenizi tekrar gönderin.");
                        yield break;
                    }

                    string icMetin = TemizJsonAyikla(disYanit.message.content);
                    _konusmaGecmisi.Add(new OllamaMesaj("assistant", icMetin));

                    MuhafizKararVerisi karar = null;
                    try
                    {
                        karar = JsonUtility.FromJson<MuhafizKararVerisi>(icMetin);
                    }
                    catch
                    {
                        karar = null;
                    }

                    // Eğer model bozuk JSON ürettiyse oyunu çökertmeden güvenli varsayılan yapıya dönüştür
                    if (karar == null || string.IsNullOrWhiteSpace(karar.cevap))
                    {
                        karar = GuvenliYedekKararOlustur(disYanit.message.content, oyuncuMesaji);
                    }

                    // KURAL DENETİMİ: Oyuncu saldırgan/hakaret içeren bir şey söylediyse veya güven 0'a düştüyse
                    DenetleVeDengeliPuanla(karar, oyuncuMesaji, mevcutGuvenSkoru, iknaAdimSayisi);

                    onBasarili?.Invoke(karar);
                }
                catch (Exception ex)
                {
                    onHata?.Invoke($"Model yanıtı ayrıştırılırken beklenmeyen bir durum oluştu ({ex.GetType().Name}). Oyun güvenli modda devam ediyor; lütfen tekrar deneyin.");
                }
            }
        }

        /// <summary>
        /// Model JSON etrafına markdown (```json ... ```) eklediyse sadece { ... } bloğunu ayıklar.
        /// </summary>
        private string TemizJsonAyikla(string ham)
        {
            if (string.IsNullOrEmpty(ham)) return "{}";
            int ilkSuslu = ham.IndexOf('{');
            int sonSuslu = ham.LastIndexOf('}');
            if (ilkSuslu >= 0 && sonSuslu > ilkSuslu)
            {
                return ham.Substring(ilkSuslu, sonSuslu - ilkSuslu + 1);
            }
            return ham.Trim();
        }

        /// <summary>
        /// Modelin geçersiz JSON döndürmesi durumunda oyunun çökmesini önleyen (Madde 4.2) yedek ayrıştırıcı.
        /// </summary>
        private MuhafizKararVerisi GuvenliYedekKararOlustur(string hamYanit, string oyuncuMesaji)
        {
            return new MuhafizKararVerisi
            {
                cevap = string.IsNullOrWhiteSpace(hamYanit)
                    ? "Ne dediğini anlamadım yabancı! Nöbet yerimde beni oyalama, parolayı veya buraya geliş sebebini açıkça söyle!"
                    : hamYanit.Replace("{", "").Replace("}", "").Replace("\"", "").Trim(),
                tepkiTuru = "SupheciRed",
                guvenDegisimi = 0,
                kapiyiAc = false,
                savasTetikle = false
            };
        }

        /// <summary>
        /// Küçük (3B) dil modellerinin tek mesajda hemen ikna olma eğilimini ("bu kadar kolay ikna olmamalı")
        /// ve saldırgan/alakasız mesaj ayrımlarını oyun kurallarıyla kesinleştirir.
        /// </summary>
        private void DenetleVeDengeliPuanla(MuhafizKararVerisi karar, string oyuncuMesaji, int mevcutGuven, int iknaAdimSayisi)
        {
            string m = oyuncuMesaji.ToLowerInvariant();

            // 1. Saldırgan / Kızdıran / Tehditkar mesaj kontrolü
            bool tehditVar = m.Contains("öldür") || m.Contains("oldur") || m.Contains("vururum") ||
                             m.Contains("aptal") || m.Contains("salak") || m.Contains("ahmak") ||
                             m.Contains("gebert") || m.Contains("çekil") || m.Contains("cekil") ||
                             m.Contains("kafana sık") || m.Contains("hain");

            if (tehditVar || karar.GetTepkiEnum() == MuhafizTepkiTuru.SaldirganKizdiran)
            {
                karar.tepkiTuru = "SaldirganKizdiran";
                karar.guvenDegisimi = Mathf.Min(karar.guvenDegisimi, -30);
                karar.kapiyiAc = false;
                if (mevcutGuven + karar.guvenDegisimi <= 15 || tehditVar)
                {
                    karar.savasTetikle = true;
                }
                return;
            }

            // 2. Çok kısa (1-2 kelimelik: "merhaba", "aç", "selam") veya alakasız mesajlar asla güven artırmaz
            if (oyuncuMesaji.Trim().Length < 12)
            {
                if (karar.guvenDegisimi > 0) karar.guvenDegisimi = -5;
                karar.tepkiTuru = "YetersizTeklif";
                karar.kapiyiAc = false;
                return;
            }

            // 3. Küçük (3B) dil modellerinde ikna edici argümanların tutarlı sınıflandırılmasını güçlendir:
            bool gucluArgumanVar = m.Contains("kızıl şahin") || m.Contains("kizil sahin") ||
                                   m.Contains("gece kuzgunu") || m.Contains("parola") ||
                                   m.Contains("komutan") || m.Contains("başkomutan") || m.Contains("baskomutan") ||
                                   m.Contains("mühür") || m.Contains("muhur") || m.Contains("istihbarat") ||
                                   m.Contains("500") || m.Contains("1000") || m.Contains("altın") || m.Contains("altin") ||
                                   m.Contains("kese") || m.Contains("maaş") || m.Contains("maas") || m.Contains("ganimet");

            if (gucluArgumanVar && !tehditVar)
            {
                karar.tepkiTuru = "IknaEdici";
                if (karar.guvenDegisimi < 25)
                {
                    karar.guvenDegisimi = 25;
                }
            }

            // 4. Güven değişimini tek adımda maksimum +30 ile sınırla ki tek cümlede %30'dan %85'e fırlamasın!
            karar.guvenDegisimi = Mathf.Clamp(karar.guvenDegisimi, -35, 30);

            // 5. EN ÖNEMLİ KURAL: İlk başarılı ikna adımında (iknaAdimSayisi == 0) kapı ASLA hemen açılmaz!
            // Oyuncu en az 2 güçlü argüman sunmalı ve toplam güven >= 80 olmalıdır.
            int yeniGuven = Mathf.Clamp(mevcutGuven + karar.guvenDegisimi, 0, 100);
            if (iknaAdimSayisi < 1 || yeniGuven < 80)
            {
                karar.kapiyiAc = false;
            }
            else if (yeniGuven >= 80 && karar.GetTepkiEnum() == MuhafizTepkiTuru.IknaEdici)
            {
                karar.kapiyiAc = true;
            }
        }
    }
}
