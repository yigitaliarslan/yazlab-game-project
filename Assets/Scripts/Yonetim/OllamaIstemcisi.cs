using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
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
        public float temperature = 0.15f;      // Halüsinasyonu ve saçmalamayı önleyen düşük sıcaklık
        public float top_p = 0.80f;            // Odaklı kelime seçimi
        public int top_k = 20;                 // Alakasız token üretimini engeller
        public float repeat_penalty = 1.12f;   // Kelime/cümle tekrarını dengeler
        public int num_predict = 150;          // Kısa, net ve hızlı JSON yanıtı
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
    /// Yerel Ollama LLM İstemcisi (IYerelDilModeliServisi implementasyonu).
    /// - Aşamalı sorgulama (kimlik/parola + kanıt/rüşvet) uygular.
    /// - Yapılandırılmış JSON şeması ile oyun durumunu (Kapı Açma veya Savaş Tetikleme) kontrol eder.
    /// - Sunucu kapalıysa, zaman aşımı olursa veya bozuk JSON gelirse hata yönetimi devreye girer.
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
        [TextArea(6, 12)]
        [SerializeField] private string sistemYonergesi =
            "Sen Kızıl Kule kapısını nöbetçi olarak bekleyen Kıdemli Muhafız Komutan Varg'sın.\n" +
            "Karşındaki oyuncuya doğrudan 1. tekil şahıs (\"ben\") olarak sert ve otoriter bir kale komutanı gibi Türkçe cevap ver.";

        private readonly List<OllamaMesaj> _konusmaGecmisi = new List<OllamaMesaj>();
        private bool _istekDevamEdiyor = false;
        private bool _sunucuCevrimici = true;
        private string _sonBaglantiDurumu = "Yerel Sunucu Hazır (localhost:11434)";

        public string AktifModelAdi => modelAdi;
        public bool IstekDevamEdiyorMu => _istekDevamEdiyor;
        public bool SunucuCevrimiciMi => _sunucuCevrimici;
        public string SonBaglantiDurumu => _sonBaglantiDurumu;

        private void Awake()
        {
            SohbetiSifirla();
        }

        public void SohbetiSifirla()
        {
            _konusmaGecmisi.Clear();
            _istekDevamEdiyor = false;
        }

        /// <summary>
        /// Yerel Ollama sunucusunun açık olup olmadığını ve seçili modelin yüklü olup olmadığını
        /// ana oyun akışını dondurmadan (asenkron) kontrol eder.
        /// </summary>
        public void SunucuDurumunuKontrolEt(Action<bool, string> onTamamlandi = null)
        {
            StartCoroutine(SunucuDurumuCoroutine(onTamamlandi));
        }

        private IEnumerator SunucuDurumuCoroutine(Action<bool, string> onTamamlandi)
        {
            string tagsUrl = "http://localhost:11434/api/tags";
            try
            {
                Uri uri = new Uri(apiUrl);
                tagsUrl = $"{uri.Scheme}://{uri.Authority}/api/tags";
            }
            catch { }

            using (UnityWebRequest istek = UnityWebRequest.Get(tagsUrl))
            {
                istek.timeout = 4;
                yield return istek.SendWebRequest();

                if (istek.result != UnityWebRequest.Result.Success)
                {
                    _sunucuCevrimici = false;
                    _sonBaglantiDurumu = "Çevrimdışı (Ollama Sunucusu Kapalı - Güvenli Yedek Mod Aktif)";
                    onTamamlandi?.Invoke(false, _sonBaglantiDurumu);
                    yield break;
                }

                string yanit = istek.downloadHandler != null ? istek.downloadHandler.text : "";
                string kisaModelAdi = (modelAdi ?? "llama3.2").Split(':')[0].ToLowerInvariant();
                if (!string.IsNullOrEmpty(yanit) && !yanit.ToLowerInvariant().Contains(kisaModelAdi))
                {
                    _sunucuCevrimici = false;
                    _sonBaglantiDurumu = $"Uyarı: Ollama Açık Ancak '{modelAdi}' Modeli Yüklü Değil (Güvenli Mod)";
                    onTamamlandi?.Invoke(false, _sonBaglantiDurumu);
                    yield break;
                }

                _sunucuCevrimici = true;
                _sonBaglantiDurumu = $"Çevrimiçi ({modelAdi} Hazır)";
                onTamamlandi?.Invoke(true, _sonBaglantiDurumu);
            }
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

            // Bağlam penceresini korumak için son 4 mesajı (2 konuşma turunu) tut
            while (_konusmaGecmisi.Count > 4)
            {
                _konusmaGecmisi.RemoveAt(0);
            }

            string dinamikSistemIstemi = OlusturDurumaOzelSistemIstemi(oyuncuMesaji, mevcutGuvenSkoru, iknaAdimSayisi);

            List<OllamaMesaj> istekMesajlari = new List<OllamaMesaj>
            {
                new OllamaMesaj("system", dinamikSistemIstemi)
            };
            istekMesajlari.AddRange(_konusmaGecmisi);
            istekMesajlari.Add(new OllamaMesaj("user", oyuncuMesaji));

            OllamaChatIstegi istekVerisi = new OllamaChatIstegi
            {
                model = modelAdi,
                messages = istekMesajlari,
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

                // Bağlantı hatası, model eksikliği veya zaman aşımı durumu
                if (istek.result != UnityWebRequest.Result.Success)
                {
                    _sunucuCevrimici = false;
                    string detayliHata = OlusturBaglantiHataMesaji(istek);
                    _sonBaglantiDurumu = "Çevrimdışı / Hata Yönetimi Aktif";
                    Debug.LogWarning($"[OllamaIstemcisi] {detayliHata}");

                    onHata?.Invoke(detayliHata);

                    MuhafizKararVerisi cevrimdisiKarar = CevrimdisiYedekKararOlustur(oyuncuMesaji, mevcutGuvenSkoru, iknaAdimSayisi);
                    DenetleVeDengeliPuanla(cevrimdisiKarar, oyuncuMesaji, mevcutGuvenSkoru, iknaAdimSayisi);
                    GecmiseTemizTurEkle(oyuncuMesaji, cevrimdisiKarar);
                    onBasarili?.Invoke(cevrimdisiKarar);
                    yield break;
                }

                _sunucuCevrimici = true;
                _sonBaglantiDurumu = $"Çevrimiçi ({modelAdi})";
                string gelenHamJson = istek.downloadHandler.text;

                // Boş, bozuk veya geçersiz JSON yanıtlarına karşı ayrıştırma
                try
                {
                    OllamaChatYaniti disYanit = JsonUtility.FromJson<OllamaChatYaniti>(gelenHamJson);
                    string modelHamIcerik = (disYanit != null && disYanit.message != null) ? disYanit.message.content : "";

                    MuhafizKararVerisi karar = EsnekJsonAyristir(modelHamIcerik, oyuncuMesaji, out bool jsonOnarildiMi);
                    if (jsonOnarildiMi)
                    {
                        string jsonUyari = "[GEÇERSİZ / BOZUK JSON ONARILDI] Yerel dil modeli standart şema dışında bir çıktı üretti. Hata yakalanarak yapılandırılmış veri güvenli biçimde onarıldı.";
                        Debug.LogWarning($"[OllamaIstemcisi] {jsonUyari} Ham içerik: {modelHamIcerik}");
                        onHata?.Invoke(jsonUyari);
                    }

                    DenetleVeDengeliPuanla(karar, oyuncuMesaji, mevcutGuvenSkoru, iknaAdimSayisi);
                    GecmiseTemizTurEkle(oyuncuMesaji, karar);

                    onBasarili?.Invoke(karar);
                }
                catch (Exception ex)
                {
                    string ayristirmaHatasi = $"[JSON AYRIŞTIRMA HATASI] Model yanıtı işlenirken beklenmedik hata oluştu ({ex.GetType().Name}). Oyun çökmeden güvenli yedek yanıtla devam ediyor.";
                    Debug.LogWarning($"[OllamaIstemcisi] {ayristirmaHatasi}");
                    onHata?.Invoke(ayristirmaHatasi);

                    MuhafizKararVerisi yedekKarar = CevrimdisiYedekKararOlustur(oyuncuMesaji, mevcutGuvenSkoru, iknaAdimSayisi);
                    DenetleVeDengeliPuanla(yedekKarar, oyuncuMesaji, mevcutGuvenSkoru, iknaAdimSayisi);
                    GecmiseTemizTurEkle(oyuncuMesaji, yedekKarar);
                    onBasarili?.Invoke(yedekKarar);
                }
            }
        }

        private string OlusturBaglantiHataMesaji(UnityWebRequest istek)
        {
            string hamHata = istek != null && !string.IsNullOrEmpty(istek.error) ? istek.error : "Bilinmeyen ağ hatası";
            long kod = istek != null ? istek.responseCode : 0;

            if (hamHata.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return $"[ZAMAN AŞIMI UYARISI] Yerel Ollama sunucusu ({apiUrl}) {zamanAsimiSaniye} saniye içinde yanıt vermedi. Oyun akışı kesintisiz devam ediyor; güvenli karar mekanizması devreye alındı.";
            }

            if (kod == 404)
            {
                return $"[MODEL BULUNAMADI - HTTP 404] Yerel Ollama sunucusu açık ancak '{modelAdi}' modeli yüklü değil (Terminalde 'ollama pull {modelAdi}' komutunu çalıştırın). Oyun çökmeden güvenli modda devam ediyor.";
            }

            return $"[SUNUCU BAĞLANTI HATASI] Yerel Ollama sunucusuna ({apiUrl}) ulaşılamadı ({hamHata}). Ollama servisinin açık olduğundan emin olun. Oyun çökmeden çevrimdışı güvenli modda devam ediyor.";
        }

        private void GecmiseTemizTurEkle(string oyuncuMesaji, MuhafizKararVerisi karar)
        {
            _konusmaGecmisi.Add(new OllamaMesaj("user", oyuncuMesaji));
            _konusmaGecmisi.Add(new OllamaMesaj("assistant", JsonUtility.ToJson(karar)));
        }

        /// <summary>
        /// Model farklı JSON alan adları ("yanit", "mesaj", "response") kullansa veya bozuk JSON üretse bile
        /// ham JSON anahtarlarını ("tepkiTuru", "guvenDegisimi") asla konuşma balonuna sızdırmadan ayrıştırır.
        /// </summary>
        private MuhafizKararVerisi EsnekJsonAyristir(string hamIcerik, string oyuncuMesaji, out bool jsonOnarildiMi)
        {
            jsonOnarildiMi = false;
            if (string.IsNullOrWhiteSpace(hamIcerik) || !hamIcerik.Contains("{") || !hamIcerik.Contains("}"))
            {
                jsonOnarildiMi = true;
            }

            string temizJson = TemizJsonAyikla(hamIcerik);
            MuhafizKararVerisi karar = null;

            try
            {
                karar = JsonUtility.FromJson<MuhafizKararVerisi>(temizJson);
            }
            catch
            {
                karar = null;
                jsonOnarildiMi = true;
            }

            if (karar == null)
            {
                karar = new MuhafizKararVerisi();
                jsonOnarildiMi = true;
            }

            // Eğer model "cevap" yerine "yanit", "mesaj", "response", "text" gibi bir anahtar kullandıysa yakala
            if (string.IsNullOrWhiteSpace(karar.cevap) && !string.IsNullOrWhiteSpace(temizJson))
            {
                jsonOnarildiMi = true;
                string[] olasiAnahtarlar = { "cevap", "yanit", "yanıt", "mesaj", "response", "message", "text", "komutan_varg" };
                foreach (string anahtar in olasiAnahtarlar)
                {
                    Match m = Regex.Match(temizJson, $"\"{anahtar}\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.IgnoreCase);
                    if (m.Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                    {
                        karar.cevap = Regex.Unescape(m.Groups[1].Value);
                        break;
                    }
                }
            }

            karar.cevap = YanitMetniniTemizle(karar.cevap);
            if (!string.IsNullOrEmpty(karar.cevap) && (karar.cevap.Contains("{") || karar.cevap.Contains("tepkiTuru")))
            {
                jsonOnarildiMi = true;
            }

            return karar;
        }

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
        /// Modelin yanıt metnine sızdırdığı köşeli parantez etiketlerini, JSON kalıntılarını ve önekleri temizler.
        /// </summary>
        private string YanitMetniniTemizle(string metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return "";

            string t = metin.Trim();
            // Köşeli parantez içindeki durum notlarını temizle: [MEVCUT OYUN DURUMU...] vb.
            t = Regex.Replace(t, @"\[[^\]]*\]", "").Trim();
            // Başlangıçtaki "Komutan Varg:" gibi mükerrer isim öneklerini temizle
            t = Regex.Replace(t, @"^(Kıdemli\s+Muhafız\s+)?(Komutan\s+Varg|Muhafız\s+Varg|Varg)\s*:\s*", "", RegexOptions.IgnoreCase).Trim();
            // 3B modelin zamir/özne kaymalarını doğal birinci tekil şahısa düzelt
            t = Regex.Replace(t, @"sana\s+sunduğum", "bana sunduğun", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"kapıyı\s+açabilirsin(iz)?", "kapının kilidini açıyorum, geçebilirsin", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"kapiyi\s+acabilirsin(iz)?", "kapının kilidini açıyorum, geçebilirsin", RegexOptions.IgnoreCase);
            // Çift tırnak sarma kalıntılarını temizle
            t = t.Trim('"', '\'', ' ');
            return t;
        }

        private string OlusturDurumaOzelSistemIstemi(string oyuncuMesaji, int mevcutGuvenSkoru, int iknaAdimSayisi)
        {
            OyuncuMesajiniAnalizEt(
                oyuncuMesaji,
                out bool tehditVar,
                out bool parolaVar,
                out bool emirKanitVar,
                out bool rusvetVar,
                out bool dusukRusvetVar,
                out bool alakasizVar);

            bool kapiAcilacak = (parolaVar || emirKanitVar || rusvetVar) && !dusukRusvetVar && !tehditVar && iknaAdimSayisi >= 1 && (mevcutGuvenSkoru + 30) >= 80;
            MuhafizTepkiTuru hedefTepki =
                tehditVar ? MuhafizTepkiTuru.SaldirganKizdiran :
                ((parolaVar || emirKanitVar || rusvetVar) && !dusukRusvetVar ? MuhafizTepkiTuru.IknaEdici :
                (alakasizVar && !dusukRusvetVar ? MuhafizTepkiTuru.AlakasizKonusma :
                (dusukRusvetVar ? MuhafizTepkiTuru.YetersizTeklif : MuhafizTepkiTuru.SupheciRed)));

            int hedefGuvenDegisimi =
                hedefTepki == MuhafizTepkiTuru.SaldirganKizdiran ? -35 :
                (hedefTepki == MuhafizTepkiTuru.IknaEdici ? 30 :
                (hedefTepki == MuhafizTepkiTuru.SupheciRed ? -5 : -10));

            bool hedefSavas = hedefTepki == MuhafizTepkiTuru.SaldirganKizdiran;

            string ornekCevap = SenaryoyaUygunCevapUret(
                hedefTepki,
                kapiAcilacak,
                iknaAdimSayisi,
                parolaVar,
                emirKanitVar,
                rusvetVar,
                dusukRusvetVar);

            string durumAciklamasi;
            if (tehditVar)
            {
                durumAciklamasi = "Oyuncu sana hakaret ve tehdit etti! Çok sert bir şekilde onu azarlayıp tehditlerine boyun eğmeyeceğini ve kapıyı açmayacağını söyle (kapiyiAc: false, savasTetikle: true).";
            }
            else if ((parolaVar || emirKanitVar || rusvetVar) && !dusukRusvetVar)
            {
                if (kapiAcilacak)
                {
                    durumAciklamasi = "Oyuncu 2. aşamayı da tamamladı! Oyuncunun sunduğu kanıtı/altını kabul ettiğini ve Kızıl Kule kapısının kilidini şimdi SENİN açtığını, geçebileceğini söyle (kapiyiAc: true).";
                }
                else
                {
                    durumAciklamasi = "Henüz İlk Aşamadasın (Aşama 0/2). Kapıyı henüz AÇMA (kapiyiAc: false). Oyuncunun sunduğu ilk kanıtın/teklifin olumlu olduğunu söyle ama kapıyı açmak için ikinci doğrulama kanıtını ('Kızıl Şahin' gece parolası veya kraliyet mührü/500 altın) da sunmasını iste.";
                }
            }
            else if (dusukRusvetVar)
            {
                durumAciklamasi = "Oyuncu sana çok düşük bir rüşvet teklif etti. Onu tersle, en az 500 altın veya gece parolası ('Kızıl Şahin') olmadan kapıyı açmayacağını söyle (kapiyiAc: false).";
            }
            else if (alakasizVar)
            {
                durumAciklamasi = "Oyuncu nöbetle alakasız bir şey söyledi. Onu sertçe uyar, burasının Kızıl Kule nöbet noktası olduğunu ve gece parolasını ('Kızıl Şahin') açıklaması gerektiğini söyle (kapiyiAc: false).";
            }
            else
            {
                durumAciklamasi = "Oyuncu kanıtsız bir ricada bulundu. Kuru sözle kapıyı açmayacağını, gece parolasını ('Kızıl Şahin') veya 500 altınlık keseyi göstermesi gerektiğini söyle (kapiyiAc: false).";
            }

            return sistemYonergesi + "\n" +
                   "ŞU ANKİ DURUM VE GÖREVİN:\n" + durumAciklamasi + "\n\n" +
                   "ÖRNEK KONUŞMA ÜSLUBU (Oyuncunun sözlerine göre kendi cümleni kur):\n" +
                   "\"" + ornekCevap + "\"\n\n" +
                   "Yanıtını SADECE aşağıdaki JSON formatında ver:\n" +
                   "{\n" +
                   "  \"cevap\": \"Komutan Varg'ın oyuncunun mesajına özel 1-2 cümlelik Türkçe yanıtı\",\n" +
                   "  \"tepkiTuru\": \"" + hedefTepki + "\",\n" +
                   "  \"guvenDegisimi\": " + hedefGuvenDegisimi + ",\n" +
                   "  \"kapiyiAc\": " + (kapiAcilacak ? "true" : "false") + ",\n" +
                   "  \"savasTetikle\": " + (hedefSavas ? "true" : "false") + "\n" +
                   "}";
        }

        /// <summary>
        /// Modelin ürettiği cevabın Türkçe, anlamlı, rolüne uygun ve mevcut oyun kararıyla (kapı açma / reddetme)
        /// tutarlı olup olmadığını denetler.
        /// </summary>
        private bool CevapGecerliVeTutarliMi(
            string cevap,
            MuhafizTepkiTuru tepki,
            bool kapiAciliyor,
            bool parolaVar,
            bool emirKanitVar,
            bool rusvetVar)
        {
            if (string.IsNullOrWhiteSpace(cevap)) return false;
            if (cevap.Length < 28 || cevap.Length > 450) return false;

            string k = cevap.ToLowerInvariant();

            // 1. JSON anahtarı, rol karmaşası ("kapıyı açabilirsin" vb.), bozuk alt-kelime veya İngilizce/meta halüsinasyon kontrolü
            string[] yasakliKalintilar =
            {
                "tepkituru", "guvendegisimi", "kapiyiac", "savastetikle", "{", "}",
                "guvenskoru", "iknaadimi", "oyun durumu", "as an ai", "i am ", "i cannot",
                "here is", "json", "prompt", "system:", "user:", "assistant:", "true", "false",
                "açabilirsin", "acabilirsin", "açabilirsiniz", "acabilirsiniz", "kapıyı açın", "kapiyi acin",
                "sen aç", "siz açın", "ne istiyorsun", "sana bir parola", "hediye olarak",
                "sendedir", "oyuncuya", "deme", "örnek üslup", "durum:", "sana 500", "size 500",
                "teklif etmedim", "aptal muhafız", "aptalca bir", "söylediği için", "karşılaşırsın",
                "bilmediğinizi", "now", "job", "thing", "need", "require", "golden", "gurulda",
                "ya veya", "varlını", "tamahecek", "açlması", "acılacaktır", "açlabilir", "gecele",
                "ölümlük", "kapisinin", "mühürünün", "aptalli", "yolumdan çekil", "beklemeyecektir",
                "beklemiyorum", "oyun sandın", "konusal", "advice", "devalafet"
            };
            foreach (string yasak in yasakliKalintilar)
            {
                if (k.Contains(yasak)) return false;
            }

            // 2. Latin/Türkçe dışı (Kiril, Çince vb.) bozuk karakter kontrolü
            foreach (char c in cevap)
            {
                if (c > 0x024F && c != '…' && c != '’' && c != '‘' && c != '“' && c != '”' && c != '—' && c != '–' && c != '₺')
                {
                    return false;
                }
            }

            // 3. Karar - Metin Çelişkisi Kontrolü:
            bool metindeGecisIzniVar = k.Contains("kapıyı açıyorum") || k.Contains("kapiyi aciyorum") ||
                                       k.Contains("kilidini açıyorum") || k.Contains("kilidini aciyorum") ||
                                       k.Contains("kilidi açıyorum") || k.Contains("kilidi aciyorum") ||
                                       k.Contains("kapısını açıyorum") || k.Contains("kapisini aciyorum") ||
                                       k.Contains("kapı açılıyor") || k.Contains("kapi aciliyor") ||
                                       k.Contains("geçebilirsin") || k.Contains("gecebilirsin") ||
                                       k.Contains("girebilirsin") || k.Contains("yolun açık") ||
                                       k.Contains("yolun acik") || k.Contains("kilidi açtım") ||
                                       k.Contains("kilidi actim") || k.Contains("içeri gir") ||
                                       k.Contains("iceri gir") || k.Contains("buyrun");

            // Kapı henüz açılmıyorsa (ör. 1. aşama), muhafız "kapıyı açıyorum / geçebilirsin" diyemez
            if (!kapiAciliyor && metindeGecisIzniVar)
            {
                return false;
            }

            // 1. Aşamada (IknaEdici ama kapı henüz açılmıyor) muhafız oyuncunun zaten sunduğu kanıtı tekrar istememeli, ikinci kanıtı istemeli
            if (!kapiAciliyor && tepki == MuhafizTepkiTuru.IknaEdici)
            {
                bool ikinciKanitIstiyor = k.Contains("parola") || k.Contains("kızıl şahin") || k.Contains("mühür") ||
                                          k.Contains("mühr") || k.Contains("500") || k.Contains("altın") ||
                                          k.Contains("emir") || k.Contains("emri");
                if (!ikinciKanitIstiyor) return false;

                if (rusvetVar && !parolaVar && !(k.Contains("parola") || k.Contains("kızıl şahin")))
                {
                    return false;
                }
                if (parolaVar && !rusvetVar && !(k.Contains("mühür") || k.Contains("mühr") || k.Contains("500") || k.Contains("altın")))
                {
                    return false;
                }
            }

            // Kapı açılıyorsa (2. aşama tamamlandıysa):
            // Muhafız soru soramaz ("?"), reddedemez ve bizzat kendisinin kapıyı/kilidi açtığını belirtmelidir
            bool metindeRedVeyaSartVar = k.Contains("?") ||
                                         k.Contains("açmam") || k.Contains("acmam") ||
                                         k.Contains("açmıyorum") || k.Contains("acmiyorum") ||
                                         k.Contains("geçemezsin") || k.Contains("gecemezsin") ||
                                         k.Contains("izin vermiyorum") || k.Contains("reddediyorum") ||
                                         k.Contains("yetersiz") || k.Contains("yetmez") ||
                                         k.Contains("nereden bileyim") || k.Contains("göster") ||
                                         k.Contains("kanıtla") || k.Contains("kesesini sun");

            bool birinciSahisKapiAcmaVar = k.Contains("kapıyı açıyorum") || k.Contains("kapiyi aciyorum") ||
                                           k.Contains("kilidini açıyorum") || k.Contains("kilidini aciyorum") ||
                                           k.Contains("kilidi açıyorum") || k.Contains("kilidi aciyorum") ||
                                           k.Contains("kapısını açıyorum") || k.Contains("kapisini aciyorum");

            if (kapiAciliyor && (!birinciSahisKapiAcmaVar || !metindeGecisIzniVar || metindeRedVeyaSartVar))
            {
                return false;
            }

            return true;
        }

        private void OyuncuMesajiniAnalizEt(
            string oyuncuMesaji,
            out bool tehditVar,
            out bool parolaVar,
            out bool emirKanitVar,
            out bool rusvetVar,
            out bool dusukRusvetVar,
            out bool alakasizVar)
        {
            string m = (oyuncuMesaji ?? "").ToLowerInvariant();

            tehditVar = m.Contains("öldür") || m.Contains("oldur") || m.Contains("vururum") ||
                        m.Contains("aptal") || m.Contains("salak") || m.Contains("ahmak") ||
                        m.Contains("gebert") || m.Contains("çekil") || m.Contains("cekil") ||
                        m.Contains("kafana") || m.Contains("hain") || m.Contains("defol") ||
                        m.Contains("kes sesini") || m.Contains("gerizekal");

            parolaVar = m.Contains("kızıl şahin") || m.Contains("kizil sahin") ||
                        m.Contains("gece kuzgunu") || m.Contains("parola") ||
                        m.Contains("şifre") || m.Contains("sifre");

            emirKanitVar = m.Contains("komutan") || m.Contains("başkomutan") || m.Contains("baskomutan") ||
                           m.Contains("mühür") || m.Contains("muhur") || m.Contains("istihbarat") ||
                           m.Contains("belge") || m.Contains("ferman") || m.Contains("kraliyet") ||
                           m.Contains("emir") || m.Contains("emri") || m.Contains("rapor") ||
                           m.Contains("gizli görev") || m.Contains("gizli gorev");

            bool yuksekMiktar = m.Contains("500") || m.Contains("600") || m.Contains("700") ||
                                m.Contains("800") || m.Contains("900") || m.Contains("1000") ||
                                m.Contains("bin ") || m.Contains("beş yüz") || m.Contains("bes yuz") ||
                                m.Contains("kese") || m.Contains("dolu") || m.Contains("yüklü") || m.Contains("yuklu");

            bool altinKelimesi = m.Contains("altın") || m.Contains("altin") || m.Contains("akçe") ||
                                 m.Contains("akce") || m.Contains("maaş") || m.Contains("maas") ||
                                 m.Contains("ganimet") || m.Contains("ikramiye") || m.Contains("hazine");

            dusukRusvetVar = altinKelimesi && !yuksekMiktar &&
                             (Regex.IsMatch(m, @"\b(5|10|20|30|40|50|100|200)\b") || m.Contains("az ") || m.Contains("birkaç"));

            rusvetVar = (altinKelimesi && !dusukRusvetVar) || m.Contains("500") || m.Contains("1000");

            bool kuleBaglamiVar = parolaVar || emirKanitVar || altinKelimesi ||
                                  m.Contains("kapı") || m.Contains("kapi") || m.Contains("geç") || m.Contains("gec") ||
                                  m.Contains("nöbet") || m.Contains("nobet") || m.Contains("asker") ||
                                  m.Contains("muhafız") || m.Contains("muhafiz") || m.Contains("kule") ||
                                  m.Contains("prenses") || m.Contains("varg") || m.Contains("kale");

            bool gundelikAlakasiz = m.Contains("hava") || m.Contains("futbol") || m.Contains("maç") ||
                                    m.Contains("yemek") || m.Contains("çorba") || m.Contains("şaka") ||
                                    m.Contains("fıkra") || m.Contains("nasılsın") || m.Contains("nasilsin") ||
                                    m.Contains("naber") || m.Contains("selamün") || m.Contains("merhaba");

            alakasizVar = (!kuleBaglamiVar || gundelikAlakasiz) && !tehditVar && !parolaVar && !emirKanitVar && !rusvetVar;
        }

        private string SenaryoyaUygunCevapUret(
            MuhafizTepkiTuru tepki,
            bool kapiAciliyor,
            int iknaAdimSayisi,
            bool parolaVar,
            bool emirKanitVar,
            bool rusvetVar,
            bool dusukRusvetVar)
        {
            switch (tepki)
            {
                case MuhafizTepkiTuru.SaldirganKizdiran:
                    return "Bana hakaret ve tehdit etmeye nasıl cüret edersin?! Nöbet yerimde kimseye pabuç bırakmam; parolayı ('Kızıl Şahin') ve Başkomutanın emrini adamakıllı söylemezsen bu kapıdan asla geçemezsin!";

                case MuhafizTepkiTuru.AlakasizKonusma:
                    return "Beni boş ve alakasız laflarla oyalama yabancı! Burası Kızıl Kule'nin en kritik nöbet noktası; gece parolasını ('Kızıl Şahin') ve buraya geliş emrini derhal açıkla!";

                case MuhafizTepkiTuru.YetersizTeklif:
                    if (dusukRusvetVar)
                    {
                        return "Beni üç beş kuruşa tamah edecek sıradan bir kapı bekçisi mi sandın?! En az 500 altınlık kese veya Başkomutanın kraliyet mührü olmadan bu kilide elimi bile sürmem!";
                    }
                    return "Sadece kuru sözle veya kanıtsız ricayla bu kapıyı açmam! Bana gece parolasını ('Kızıl Şahin'), Başkomutanın emrini veya 500 altınlık mühürlü keseyi göster!";

                case MuhafizTepkiTuru.IknaEdici:
                    if (kapiAciliyor)
                    {
                        if (rusvetVar && parolaVar)
                        {
                            return "Pekâlâ asker! Hem 'Kızıl Şahin' gece parolasını hem de 500 altınlık ağır keseyi eksiksiz sundun. Güvenimi kazandın; Kızıl Kule çıkış kapısının kilidini açıyorum, şimdi geçebilirsin!";
                        }
                        if (rusvetVar)
                        {
                            return "Pekâlâ asker! Önceki doğrulamanın üzerine bu 500 altınlık ağır keseyle de sadakatini kanıtladın. Güvenimi kazandın; Kızıl Kule çıkış kapısının kilidini açıyorum, şimdi geçebilirsin!";
                        }
                        if (parolaVar)
                        {
                            return "Pekâlâ asker! Sunduğun kanıtın üzerine 'Kızıl Şahin' gece parolasını da eksiksiz doğruladın. Kızıl Kule çıkış kapısının kilidini açıyorum, yolun açık olsun!";
                        }
                        return "Pekâlâ asker! Başkomutanın kraliyet mührünü ve gizli görev emrini eksiksiz doğruladın. Kızıl Kule çıkış kapısının kilidini açıyorum, şimdi geçebilirsin!";
                    }
                    else
                    {
                        if (parolaVar && rusvetVar)
                        {
                            return "Hem 'Kızıl Şahin' parolasını biliyorsun hem de 500 altınlık keseyi sunuyorsun demek! Neredeyse ikna oldum asker; son teyit olarak Başkomutanın kraliyet mührünü veya gizli görev emrini de söyle ki kapıyı açayım!";
                        }
                        if (parolaVar)
                        {
                            return "Hmm... 'Kızıl Şahin' gece parolasını biliyorsun demek! Ancak tek başına parola yetmez; kapıyı açmam için şimdi bana Başkomutanın kraliyet mührünü göster ya da 500 altınlık ikramiye kesesini sun!";
                        }
                        if (rusvetVar)
                        {
                            return "Beş yüz altınlık bu ağır kese kulağa hoş geliyor asker... Ama senin casus olmadığını nereden bileyim? Kapıyı açmam için bu gecenin nöbet parolasını ('Kızıl Şahin') veya Başkomutanın mühürlü emrini de söyle!";
                        }
                        return "Başkomutanın emrinden bahsediyorsun ha? Sözlerin tutarlı görünüyor ama son bir doğrulama şart: Gece parolasını ('Kızıl Şahin') veya 500 altınlık kraliyet ödeneğini hemen kanıtla!";
                    }

                default:
                    return "Şüpheli konuşuyorsun yabancı! Gece parolasını ('Kızıl Şahin') ve Başkomutanın mühürlü emrini açıkça söylemeden bu kapıdan tek adım atamazsın!";
            }
        }

        private MuhafizKararVerisi CevrimdisiYedekKararOlustur(string oyuncuMesaji, int mevcutGuven, int iknaAdimSayisi)
        {
            MuhafizKararVerisi karar = new MuhafizKararVerisi
            {
                cevap = "",
                tepkiTuru = "SupheciRed",
                guvenDegisimi = 0,
                kapiyiAc = false,
                savasTetikle = false
            };
            DenetleVeDengeliPuanla(karar, oyuncuMesaji, mevcutGuven, iknaAdimSayisi);
            return karar;
        }

        /// <summary>
        /// Dil modelinin ürettiği kararı ve güven puanı değişimini oyun kurallarıyla denetler.
        /// </summary>
        private void DenetleVeDengeliPuanla(MuhafizKararVerisi karar, string oyuncuMesaji, int mevcutGuven, int iknaAdimSayisi)
        {
            OyuncuMesajiniAnalizEt(
                oyuncuMesaji,
                out bool tehditVar,
                out bool parolaVar,
                out bool emirKanitVar,
                out bool rusvetVar,
                out bool dusukRusvetVar,
                out bool alakasizVar);

            bool modelIknaOlduMu = karar.GetTepkiEnum() == MuhafizTepkiTuru.IknaEdici && karar.guvenDegisimi > 0 && !alakasizVar && !dusukRusvetVar && !tehditVar;
            bool gucluArgumanVar = ((parolaVar || emirKanitVar || rusvetVar) || modelIknaOlduMu) && !dusukRusvetVar && (oyuncuMesaji ?? "").Trim().Length >= 8;

            // 1. Saldırgan / Tehditkar mesaj kontrolü
            if (tehditVar || (karar.GetTepkiEnum() == MuhafizTepkiTuru.SaldirganKizdiran && !parolaVar && !emirKanitVar && !rusvetVar))
            {
                karar.tepkiTuru = "SaldirganKizdiran";
                karar.guvenDegisimi = -35;
                karar.kapiyiAc = false;
                karar.savasTetikle = true;

                if (!CevapGecerliVeTutarliMi(karar.cevap, MuhafizTepkiTuru.SaldirganKizdiran, false, parolaVar, emirKanitVar, rusvetVar))
                {
                    karar.cevap = SenaryoyaUygunCevapUret(MuhafizTepkiTuru.SaldirganKizdiran, false, iknaAdimSayisi, parolaVar, emirKanitVar, rusvetVar, dusukRusvetVar);
                }
                return;
            }

            // 2. İkna edici argüman kontrolü
            if (gucluArgumanVar)
            {
                karar.tepkiTuru = "IknaEdici";
                karar.guvenDegisimi = 30;
                karar.savasTetikle = false;

                int yeniGuven = Mathf.Clamp(mevcutGuven + karar.guvenDegisimi, 0, 100);
                // Kapı 2. adımda ve güven >= 80 olduğunda açılır
                karar.kapiyiAc = (iknaAdimSayisi >= 1 && yeniGuven >= 80);

                if (!CevapGecerliVeTutarliMi(karar.cevap, MuhafizTepkiTuru.IknaEdici, karar.kapiyiAc, parolaVar, emirKanitVar, rusvetVar))
                {
                    karar.cevap = SenaryoyaUygunCevapUret(MuhafizTepkiTuru.IknaEdici, karar.kapiyiAc, iknaAdimSayisi, parolaVar, emirKanitVar, rusvetVar, dusukRusvetVar);
                }
                return;
            }

            // 3. Yetersiz veya alakasız argüman kontrolü
            karar.kapiyiAc = false;
            karar.savasTetikle = false;

            if (alakasizVar && !dusukRusvetVar)
            {
                karar.tepkiTuru = "AlakasizKonusma";
                karar.guvenDegisimi = -10;
            }
            else if (dusukRusvetVar)
            {
                karar.tepkiTuru = "YetersizTeklif";
                karar.guvenDegisimi = -10;
            }
            else
            {
                MuhafizTepkiTuru mevcutEnum = karar.GetTepkiEnum();
                karar.tepkiTuru = (mevcutEnum == MuhafizTepkiTuru.SupheciRed) ? "SupheciRed" : "YetersizTeklif";
                karar.guvenDegisimi = -5;
            }

            MuhafizTepkiTuru hedefTepki = karar.GetTepkiEnum();
            if (!CevapGecerliVeTutarliMi(karar.cevap, hedefTepki, false, parolaVar, emirKanitVar, rusvetVar))
            {
                karar.cevap = SenaryoyaUygunCevapUret(hedefTepki, false, iknaAdimSayisi, parolaVar, emirKanitVar, rusvetVar, dusukRusvetVar);
            }
        }
    }
}
