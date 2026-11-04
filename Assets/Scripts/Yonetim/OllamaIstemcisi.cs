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
        public float temperature = 0.25f;      // Halüsinasyonu ve saçmalamayı önleyen düşük sıcaklık
        public float top_p = 0.85f;            // Odaklı kelime seçimi
        public int top_k = 30;                 // Alakasız token üretimini engeller
        public float repeat_penalty = 1.18f;   // Kelime/cümle tekrarını engeller
        public int num_predict = 180;          // Kısa, net ve hızlı JSON yanıtı
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
    /// - Halüsinasyon, bozuk JSON, İngilizce/anlamsız yanıt veya durum çelişkisi ürettiğinde akıllı denetleyici ile düzeltir.
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
            "Sen 'Komutan Varg' adında, Kızıl Kule'nin 2. katındaki çıkış kapısını koruyan sert, şüpheci ve disiplinli Kıdemli Kale Muhafızısın.\n" +
            "SADECE akıcı ve doğal Türkçe konuş. Asla İngilizce kelime veya köşeli parantezli durum notu kullanma.\n" +
            "KURALLAR:\n" +
            "1. İlk aşamada (Aşama 0/2) ASLA kapıyı açma (`kapiyiAc: false`). Oyuncu gece parolasını ('Kızıl Şahin' veya 'Gece Kuzgunu') ya da Başkomutan emrini söylese bile, ikinci bir kanıt (kraliyet mührü veya 500+ altın kese) iste.\n" +
            "2. İkinci aşamada (Aşama 1/2) oyuncu kanıtı, mührü veya 500+ altını da sunarsa ikna ol ve `kapiyiAc: true` yap.\n" +
            "3. Hakaret/tehdit varsa `tepkiTuru: \"SaldirganKizdiran\"`, `guvenDegisimi: -35`, `savasTetikle: true`, `kapiyiAc: false` yap.\n" +
            "4. Hava durumu, futbol, yemek gibi alakasız laflarda `tepkiTuru: \"AlakasizKonusma\"`, `guvenDegisimi: -10`, `kapiyiAc: false` yap.\n" +
            "5. Kanıtsız ricada veya 50-100 altın gibi küçük tekliflerde `tepkiTuru: \"YetersizTeklif\"`, `guvenDegisimi: -5`, `kapiyiAc: false` yap.\n" +
            "Yanıtını SADECE şu JSON şemasında ver:\n" +
            "{\"cevap\": \"Komutan Varg'ın Türkçe yanıtı (1-2 cümle)\", \"tepkiTuru\": \"SupheciRed|IknaEdici|YetersizTeklif|AlakasizKonusma|SaldirganKizdiran\", \"guvenDegisimi\": 0, \"kapiyiAc\": false, \"savasTetikle\": false}";

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

            // 3B modelin bağlamının şişip sapıtmaması için sadece son 4 mesajı (2 konuşma turunu) tut
            while (_konusmaGecmisi.Count > 4)
            {
                _konusmaGecmisi.RemoveAt(0);
            }

            string asamaBilgisi = iknaAdimSayisi < 1
                ? $"ŞU ANKİ DURUM: Güven=%{mevcutGuvenSkoru}, İkna Aşaması=0/2 (İLK AŞAMA). Oyuncu doğru parola veya emir söylese bile kapıyı henüz açma (`kapiyiAc: false`), ikinci doğrulama olarak kraliyet mührünü veya 500+ altın kesesini sor!"
                : $"ŞU ANKİ DURUM: Güven=%{mevcutGuvenSkoru}, İkna Aşaması=1/2 (İKİNCİ AŞAMA). Oyuncu ilk aşamayı geçti; eğer bu mesajında da mühür, Başkomutan emri, parola veya 500+ altın sunuyorsa ikna ol ve kapıyı aç (`kapiyiAc: true`, `tepkiTuru: \"IknaEdici\"`, `guvenDegisimi: 30`)!";

            List<OllamaMesaj> istekMesajlari = new List<OllamaMesaj>
            {
                new OllamaMesaj("system", sistemYonergesi + "\n\n" + asamaBilgisi)
            };
            istekMesajlari.AddRange(_konusmaGecmisi);
            // Kullanıcı mesajına köşeli parantezli meta-veri EKLEME (modelin cevaba kopyalamasını önler)
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

                // HATA YÖNETİMİ 1: Sunucu kapalı, bağlantı reddedildi veya zaman aşımı (Madde 4.3)
                if (istek.result != UnityWebRequest.Result.Success)
                {
                    MuhafizKararVerisi cevrimdisiKarar = CevrimdisiYedekKararOlustur(oyuncuMesaji, mevcutGuvenSkoru, iknaAdimSayisi);
                    DenetleVeDengeliPuanla(cevrimdisiKarar, oyuncuMesaji, mevcutGuvenSkoru, iknaAdimSayisi);
                    GecmiseTemizTurEkle(oyuncuMesaji, cevrimdisiKarar);
                    onBasarili?.Invoke(cevrimdisiKarar);
                    yield break;
                }

                string gelenHamJson = istek.downloadHandler.text;

                // HATA YÖNETİMİ 2: Boş, bozuk veya halüsinasyon içeren JSON yanıtlarına karşı dayanıklı ayrıştırma
                try
                {
                    OllamaChatYaniti disYanit = JsonUtility.FromJson<OllamaChatYaniti>(gelenHamJson);
                    string modelHamIcerik = (disYanit != null && disYanit.message != null) ? disYanit.message.content : "";

                    MuhafizKararVerisi karar = EsnekJsonAyristir(modelHamIcerik, oyuncuMesaji);

                    // KURAL VE TUTARLILIK DENETİMİ: Puanlamayı, kapı açma kararını ve cevabın anlamlılığını kesinleştir
                    DenetleVeDengeliPuanla(karar, oyuncuMesaji, mevcutGuvenSkoru, iknaAdimSayisi);

                    // Konuşma geçmişine sadece doğrulanmış, temiz JSON'u kaydet (sonraki turlarda sapıtmayı %100 engeller)
                    GecmiseTemizTurEkle(oyuncuMesaji, karar);

                    onBasarili?.Invoke(karar);
                }
                catch (Exception)
                {
                    MuhafizKararVerisi yedekKarar = CevrimdisiYedekKararOlustur(oyuncuMesaji, mevcutGuvenSkoru, iknaAdimSayisi);
                    DenetleVeDengeliPuanla(yedekKarar, oyuncuMesaji, mevcutGuvenSkoru, iknaAdimSayisi);
                    GecmiseTemizTurEkle(oyuncuMesaji, yedekKarar);
                    onBasarili?.Invoke(yedekKarar);
                }
            }
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
        private MuhafizKararVerisi EsnekJsonAyristir(string hamIcerik, string oyuncuMesaji)
        {
            string temizJson = TemizJsonAyikla(hamIcerik);
            MuhafizKararVerisi karar = null;

            try
            {
                karar = JsonUtility.FromJson<MuhafizKararVerisi>(temizJson);
            }
            catch
            {
                karar = null;
            }

            if (karar == null)
            {
                karar = new MuhafizKararVerisi();
            }

            // Eğer model "cevap" yerine "yanit", "mesaj", "response", "text" gibi bir anahtar kullandıysa yakala
            if (string.IsNullOrWhiteSpace(karar.cevap) && !string.IsNullOrWhiteSpace(temizJson))
            {
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
            // Çift tırnak sarma kalıntılarını temizle
            t = t.Trim('"', '\'', ' ');
            return t;
        }

        /// <summary>
        /// Modelin ürettiği cevabın Türkçe, anlamlı, JSON sızıntısız ve mevcut oyun kararıyla (kapı açma / reddetme)
        /// %100 uyumlu olup olmadığını denetler.
        /// </summary>
        private bool CevapGecerliVeTutarliMi(string cevap, MuhafizTepkiTuru tepki, bool kapiAciliyor)
        {
            if (string.IsNullOrWhiteSpace(cevap)) return false;
            if (cevap.Length < 18 || cevap.Length > 420) return false;

            string k = cevap.ToLowerInvariant();

            // 1. JSON anahtarı, kod kalıntısı veya İngilizce/meta halüsinasyon kontrolü
            string[] yasakliKalintilar =
            {
                "tepkituru", "guvendegisimi", "kapiyiac", "savastetikle", "{", "}",
                "guvenskoru", "iknaadimi", "oyun durumu", "as an ai", "i am ", "i cannot",
                "here is", "json", "prompt", "system:", "user:", "assistant:", "true", "false"
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
            // Kapı henüz açılmıyorsa (ör. 1. aşama), muhafız "kapıyı açıyorum / geçebilirsin" diyemez!
            bool metindeGecisIzniVar = k.Contains("kapıyı açıyorum") || k.Contains("kapiyi aciyorum") ||
                                       k.Contains("geçebilirsin") || k.Contains("gecebilirsin") ||
                                       k.Contains("yolun açık") || k.Contains("yolun acik") ||
                                       k.Contains("kilidi açtım") || k.Contains("kilidi actim") ||
                                       k.Contains("içeri gir") || k.Contains("iceri gir");

            if (!kapiAciliyor && metindeGecisIzniVar)
            {
                return false;
            }

            // Kapı açılıyorsa (2. aşama tamamlandıysa), muhafız "kapıyı açmam / geçemezsin" diyemez!
            bool metindeRedVar = k.Contains("açmam") || k.Contains("acmam") ||
                                 k.Contains("açmıyorum") || k.Contains("acmiyorum") ||
                                 k.Contains("geçemezsin") || k.Contains("gecemezsin") ||
                                 k.Contains("izin vermiyorum") || k.Contains("reddediyorum") ||
                                 k.Contains("yetersiz");

            if (kapiAciliyor && (metindeRedVar || !metindeGecisIzniVar))
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
                        if (rusvetVar)
                        {
                            return "Pekâlâ! Hem parolayı doğruladın hem de 500 altınlık ağır keseyle sadakatini kanıtladın. Güvenimi kazandın asker, Kızıl Kule kapısını açıyorum; geçebilirsin!";
                        }
                        return "Pekâlâ! Hem 'Kızıl Şahin' gece parolasını hem de Başkomutanın mühürlü emrini eksiksiz doğruladın. Kızıl Kule çıkış kapısını açıyorum, yolun açık olsun asker!";
                    }
                    else
                    {
                        if (parolaVar)
                        {
                            return "Hmm... 'Kızıl Şahin' gece parolasını biliyorsun demek! Ancak tek başına parola yetmez; şimdi bana Başkomutanın kraliyet mührünü göster ya da 500 altınlık ikramiye kesesini sun!";
                        }
                        if (rusvetVar)
                        {
                            return "Bu ağır altın kesesi kulağa hoş geliyor... Ama senin casus olmadığını nereden bileyim? Kapıyı açmam için bu gecenin nöbet parolasını ('Kızıl Şahin') veya Başkomutanın mühürlü emrini de söyle!";
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
        /// Küçük (3B) dil modellerinin tek mesajda hemen ikna olma eğilimini, alakasız/saçma yanıtlara puan vermesini
        /// ve metin-karar çelişkilerini oyun kurallarıyla %100 uyumlu hale getirir.
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

            bool gucluArgumanVar = (parolaVar || emirKanitVar || rusvetVar) && !dusukRusvetVar && (oyuncuMesaji ?? "").Trim().Length >= 10;

            // 1. Saldırgan / Kızdıran / Tehditkar mesaj kontrolü
            if (tehditVar || karar.GetTepkiEnum() == MuhafizTepkiTuru.SaldirganKizdiran)
            {
                karar.tepkiTuru = "SaldirganKizdiran";
                karar.guvenDegisimi = -35;
                karar.kapiyiAc = false;
                karar.savasTetikle = true;

                if (!CevapGecerliVeTutarliMi(karar.cevap, MuhafizTepkiTuru.SaldirganKizdiran, false))
                {
                    karar.cevap = SenaryoyaUygunCevapUret(MuhafizTepkiTuru.SaldirganKizdiran, false, iknaAdimSayisi, parolaVar, emirKanitVar, rusvetVar, dusukRusvetVar);
                }
                return;
            }

            // 2. Güçlü argüman varsa (Parola / Başkomutan Emri / Mühür / 500+ Altın)
            if (gucluArgumanVar)
            {
                karar.tepkiTuru = "IknaEdici";
                karar.guvenDegisimi = 30;
                karar.savasTetikle = false;

                int yeniGuven = Mathf.Clamp(mevcutGuven + karar.guvenDegisimi, 0, 100);
                // İlk adımda (iknaAdimSayisi == 0) kapı ASLA açılmaz; 2. adımda ve güven >= 80 olduğunda açılır
                karar.kapiyiAc = (iknaAdimSayisi >= 1 && yeniGuven >= 80);

                if (!CevapGecerliVeTutarliMi(karar.cevap, MuhafizTepkiTuru.IknaEdici, karar.kapiyiAc))
                {
                    karar.cevap = SenaryoyaUygunCevapUret(MuhafizTepkiTuru.IknaEdici, karar.kapiyiAc, iknaAdimSayisi, parolaVar, emirKanitVar, rusvetVar, dusukRusvetVar);
                }
                return;
            }

            // 3. Güçlü argüman YOKSA modelin yanlışlıkla güven artırmasını veya kapıyı açmasını KESİNLİKLE engelle!
            karar.kapiyiAc = false;
            karar.savasTetikle = false;

            if (alakasizVar && !dusukRusvetVar)
            {
                karar.tepkiTuru = "AlakasizKonusma";
                karar.guvenDegisimi = -10;
            }
            else
            {
                karar.tepkiTuru = "YetersizTeklif";
                karar.guvenDegisimi = dusukRusvetVar ? -10 : -5;
            }

            MuhafizTepkiTuru hedefTepki = karar.GetTepkiEnum();
            if (!CevapGecerliVeTutarliMi(karar.cevap, hedefTepki, false))
            {
                karar.cevap = SenaryoyaUygunCevapUret(hedefTepki, false, iknaAdimSayisi, parolaVar, emirKanitVar, rusvetVar, dusukRusvetVar);
            }
        }
    }
}
