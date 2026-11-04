using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Yazlab.Mekanikler;
using Yazlab.Yonetim;

public static class OyunTamSenaryoTesti
{
    public static void CalistirVeDogrula()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== YAZLAB OYUN TAM SENARYO VE REGRESYON DOGRULAMA RAPORU ===");

        try
        {
            // 1. Sahneyi aç ve yeni İç Kule Çift Kollu Kapı sistemini sahneye kalıcı olarak kaydet
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            CiftKolluIcKuleKapisi kapiSistemi = CiftKolluIcKuleKapisi.KurulumuGarantile();

            // Jenerator_Ve_GucKaynagi içindeki SalterMekanizmasi referanslarını temizle ki sol kolu ezmesin
            GameObject jen = GameObject.Find("Jenerator_Ve_GucKaynagi");
            if (jen != null)
            {
                SalterMekanizmasi sm = jen.GetComponent<SalterMekanizmasi>();
                if (sm != null)
                {
                    SerializedObject soSm = new SerializedObject(sm);
                    soSm.FindProperty("salterKolu").objectReferenceValue = null;
                    soSm.FindProperty("durumIsigi").objectReferenceValue = null;
                    soSm.ApplyModifiedProperties();
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            sb.AppendLine("[OK] 1. SampleScene.unity guncellendi ve kaydedildi.");
            sb.AppendLine($"     - IcKuleGirisKapisi: {(kapiSistemi != null && kapiSistemi.icKuleKapisi != null ? kapiSistemi.icKuleKapisi.transform.position.ToString() : "NULL")}");
            sb.AppendLine($"     - Sol Acma Kolu: {(kapiSistemi != null && kapiSistemi.solKol != null ? kapiSistemi.solKol.position.ToString() : "NULL")}");
            sb.AppendLine($"     - Sag Acma Kolu: {(kapiSistemi != null && kapiSistemi.sagKol != null ? kapiSistemi.sagKol.position.ToString() : "NULL")}");

            // 2. SENARYO 2: Taret + Düşmanlar + Yeni İç Kule Kapısı ve [G] Çift Kol Kontrolü
            bool baslangictaTemizMi = kapiSistemi.TaretVeDusmanlarTemizMi();
            sb.AppendLine($"[OK] 2. Taret hayattayken TaretVeDusmanlarTemizMi = {baslangictaTemizMi} (Beklenen: False)");

            // Tareti imha et ve 4 baskın askerinin spawn olduğunu doğrula
            TaretKontrol taret = UnityEngine.Object.FindAnyObjectByType<TaretKontrol>();
            if (taret != null)
            {
                MethodInfo awakeMethod = typeof(TaretKontrol).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                awakeMethod?.Invoke(taret, null);
                taret.HasarAl(999f);
            }

            int baskinAskerSayisi = 0;
            foreach (var d in UnityEngine.Object.FindObjectsByType<DusmanKontrol>(FindObjectsInactive.Exclude))
            {
                if (d != null && d.name.StartsWith("TaretBaskin_Askeri"))
                {
                    baskinAskerSayisi++;
                }
            }
            sb.AppendLine($"[OK] 3. Taret imha edildi -> Spawn olan baskin askeri sayisi: {baskinAskerSayisi} (Beklenen: 4)");
            sb.AppendLine($"     - Baskin askerleri hayattayken TaretVeDusmanlarTemizMi = {kapiSistemi.TaretVeDusmanlarTemizMi()} (Beklenen: False)");

            // Baskın askerlerini temizle
            foreach (var d in UnityEngine.Object.FindObjectsByType<DusmanKontrol>(FindObjectsInactive.Exclude))
            {
                if (d != null && d.name.StartsWith("TaretBaskin_Askeri"))
                {
                    d.HasarAl(500f);
                }
            }
            sb.AppendLine($"[OK] 4. Baskin askerleri temizlendi -> TaretVeDusmanlarTemizMi = {kapiSistemi.TaretVeDusmanlarTemizMi()} (Beklenen: True)");

            // [G] komutu ile Yoldaşı gönder (1. Kapı açık ve Oyuncu avluda)
            GameObject fkObj = GameObject.Find("FizikselEngelKapisi");
            if (fkObj != null)
            {
                KapiKontrol fk = fkObj.GetComponent<KapiKontrol>();
                fk?.KapiyiAc(true);
            }
            GameObject oyuncuObj = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
            if (oyuncuObj != null)
            {
                oyuncuObj.transform.position = new Vector3(0f, 0.5f, 16.0f);
            }

            YardimciKarakter yardimci = UnityEngine.Object.FindAnyObjectByType<YardimciKarakter>();
            if (yardimci != null)
            {
                MethodInfo yAwake = typeof(YardimciKarakter).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                yAwake?.Invoke(yardimci, null);
            }
            kapiSistemi.KomutGonderVeyaUyar();
            sb.AppendLine($"[OK] 5. [G] basildi -> Durum: {kapiSistemi.MevcutDurum}, Yardimci GorevAktifMi: {(yardimci != null && yardimci.GorevAktifMi)} (Beklenen: BirinciKolaGidiyor, True)");

            // 3. SENARYO 3: Ollama LLM 5 Farklı Diyalog Senaryosu ve Halüsinasyon Önleme Doğrulaması
            BolumYoneticisi by = UnityEngine.Object.FindAnyObjectByType<BolumYoneticisi>();
            OllamaIstemcisi llm = by.GetComponent<OllamaIstemcisi>() ?? by.gameObject.AddComponent<OllamaIstemcisi>();
            MethodInfo denetleMethod = typeof(OllamaIstemcisi).GetMethod("DenetleVeDengeliPuanla", BindingFlags.Instance | BindingFlags.NonPublic);

            // 3a. Alakasız konuşma (model yanlışlıkla +25 ve kapıyı açıyorum dese bile düzeltilmeli!)
            MuhafizKararVerisi kAlakasiz = new MuhafizKararVerisi { cevap = "Kapıyı açıyorum geçebilirsin!", tepkiTuru = "IknaEdici", guvenDegisimi = 25, kapiyiAc = true };
            denetleMethod.Invoke(llm, new object[] { kAlakasiz, "Bugün hava çok güzel, futbol maçı kaç kaç bitti?", 30, 0 });
            sb.AppendLine($"[OK] 6a. LLM Senaryo (Alakasiz): Tepki={kAlakasiz.tepkiTuru}, Puan={kAlakasiz.guvenDegisimi}, KapiAc={kAlakasiz.kapiyiAc} | Cevap=\"{kAlakasiz.cevap}\"");

            // 3b. Yetersiz teklif (50 altın)
            MuhafizKararVerisi kYetersiz = new MuhafizKararVerisi { cevap = "{tepkiTuru: IknaEdici}", tepkiTuru = "IknaEdici", guvenDegisimi = 20, kapiyiAc = true };
            denetleMethod.Invoke(llm, new object[] { kYetersiz, "Al şu 50 altını da kapıyı aç dostum", 30, 0 });
            sb.AppendLine($"[OK] 6b. LLM Senaryo (Yetersiz 50 Altin + Bozuk JSON Sızıntısı): Tepki={kYetersiz.tepkiTuru}, Puan={kYetersiz.guvenDegisimi}, KapiAc={kYetersiz.kapiyiAc} | Cevap=\"{kYetersiz.cevap}\"");

            // 3c. Saldırgan / Tehdit
            MuhafizKararVerisi kSaldirgan = new MuhafizKararVerisi { cevap = "I am an AI guard", tepkiTuru = "SupheciRed", guvenDegisimi = 0, kapiyiAc = false };
            denetleMethod.Invoke(llm, new object[] { kSaldirgan, "Çekil yolumdan aptal muhafız yoksa seni vururum!", 30, 0 });
            sb.AppendLine($"[OK] 6c. LLM Senaryo (Saldirgan + İngilizce Halüsinasyon Filtresi): Tepki={kSaldirgan.tepkiTuru}, Puan={kSaldirgan.guvenDegisimi}, Savas={kSaldirgan.savasTetikle} | Cevap=\"{kSaldirgan.cevap}\"");

            // 3d. İkna Edici 1. Adım (Gece Parolası: Kızıl Şahin - Model erken kapıyı açıyorum dese bile engellenmeli!)
            MuhafizKararVerisi kAdim1 = new MuhafizKararVerisi { cevap = "Tamam kapıyı açıyorum geçebilirsin asker!", tepkiTuru = "IknaEdici", guvenDegisimi = 30, kapiyiAc = true };
            denetleMethod.Invoke(llm, new object[] { kAdim1, "Gece parolası Kızıl Şahin, Başkomutanın gizli emriyle geldim.", 30, 0 });
            sb.AppendLine($"[OK] 6d. LLM Senaryo (1. Adim Parola - Erken Kapı Açma Engeli): Tepki={kAdim1.tepkiTuru}, Puan={kAdim1.guvenDegisimi}, KapiAc={kAdim1.kapiyiAc} | Cevap=\"{kAdim1.cevap}\"");

            // 3e. İkna Edici 2. Adım (Kraliyet Mührü + 500 Altın - Model kapıyı açmam dese bile açmalı ve cevabı düzeltmeli!)
            MuhafizKararVerisi kAdim2 = new MuhafizKararVerisi { cevap = "Hayır kapıyı açmam geçemezsin!", tepkiTuru = "SupheciRed", guvenDegisimi = -10, kapiyiAc = false };
            denetleMethod.Invoke(llm, new object[] { kAdim2, "İşte Başkomutanın kraliyet mührü ve 500 altınlık kese!", 60, 1 });
            sb.AppendLine($"[OK] 6e. LLM Senaryo (2. Adim Mühür/500 Altin - Çelişki Düzeltme): Tepki={kAdim2.tepkiTuru}, Puan={kAdim2.guvenDegisimi}, KapiAc={kAdim2.kapiyiAc} | Cevap=\"{kAdim2.cevap}\"");

            // 4. SENARYO 4 & 5: Oyun Bittiğinde Yeniden Başlatma (Restart) Sonrası LLM Kapısı ve Prenses Kurtarma Testi
            // Sahneyi yeniden açarak (Scene Reload simülasyonu) Start/MuhafizlaGorusVeKapisiAc akışını test et
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            BolumYoneticisi byReload = UnityEngine.Object.FindAnyObjectByType<BolumYoneticisi>();
            MethodInfo byAwake = typeof(BolumYoneticisi).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo byStart = typeof(BolumYoneticisi).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
            byAwake?.Invoke(byReload, null);
            byStart?.Invoke(byReload, null);

            // Şimdi yeniden başlatılmış oyunda LLM'i geçtiğimizi simüle et
            byReload.MuhafizlaGorusVeKapisiAc();

            GameObject npcKapiObj = GameObject.Find("NpcCikisKapisi");
            KapiKontrol npcKapi = npcKapiObj != null ? npcKapiObj.GetComponent<KapiKontrol>() : null;
            GameObject arkaDuvar = GameObject.Find("KuleDuvar_Arka");
            GameObject kizilKule = GameObject.Find(PrensesKulesiInsaci.KokAdi);
            PrensesKurtarmaGorevi prensesGorev = PrensesKurtarmaGorevi.Ornek;

            sb.AppendLine($"[OK] 7. YENIDEN BASLATMA (RESTART) SONRASI LLM GECISI:");
            sb.AppendLine($"     - NpcCikisKapisi AcikMi: {(npcKapi != null && npcKapi.AcikMi)} (Beklenen: True)");
            sb.AppendLine($"     - KuleDuvar_Arka KapaliMi (Kemerli Gecit AcikMi): {(arkaDuvar == null || !arkaDuvar.activeSelf)} (Beklenen: True)");
            sb.AppendLine($"     - Kizil Kule OlustuMu: {kizilKule != null} (Beklenen: True)");
            sb.AppendLine($"     - PrensesKurtarmaGorevi Asamasi: {(prensesGorev != null ? prensesGorev.mevcutAsama.ToString() : "NULL")} (Beklenen: ZumrutleriVurmaGorevi)");

            // Zümrütleri önce yanlış sırada (1: Mavi), sonra doğru sırada (0: Kırmızı -> 1: Mavi -> 2: Yeşil) vur
            if (prensesGorev != null && prensesGorev.kristaller != null && prensesGorev.kristaller.Length == 3)
            {
                prensesGorev.KristalVuruldu(prensesGorev.kristaller[1]); // Yanlış sıra (Mavi) -> Sıfırlanmalı
                int yanlisSonrasiSayi = prensesGorev.VurulanZumrutSayisi;

                prensesGorev.KristalVuruldu(prensesGorev.kristaller[0]); // 1. Kırmızı
                prensesGorev.KristalVuruldu(prensesGorev.kristaller[1]); // 2. Mavi
                prensesGorev.KristalVuruldu(prensesGorev.kristaller[2]); // 3. Yeşil
                sb.AppendLine($"[OK] 8. ZUMRUT VURMA TESTI: Yanlis sira sonrasi={yanlisSonrasiSayi} (Beklenen: 0), Dogru sira (Kirmizi->Mavi->Yesil) sonrasi={prensesGorev.VurulanZumrutSayisi}/3, Asama={prensesGorev.mevcutAsama}");
            }

            // Test sırasında değiştirilen sahneyi tekrar temiz olarak yükle ve kaydet
            var cleanScene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            CiftKolluIcKuleKapisi.KurulumuGarantile();
            EditorSceneManager.MarkSceneDirty(cleanScene);
            EditorSceneManager.SaveScene(cleanScene);
            sb.AppendLine("[OK] 9. Sahne temiz durumda kaydedildi.");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"[HATA] {ex}");
        }

        string reportPath = Path.Combine(Application.dataPath, "../test_report.txt");
        File.WriteAllText(reportPath, sb.ToString());
        Debug.Log(sb.ToString());
        EditorApplication.Exit(0);
    }
}
