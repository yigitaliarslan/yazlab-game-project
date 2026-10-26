using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Yazlab.Mekanikler;
using Yazlab.Yonetim;
using Yazlab.Etkilesim;

namespace Yazlab.Editor
{
    /// <summary>
    /// Taktiksel İstihbarat Operasyonu senaryosuna uygun 3 Bölümlü & Ayrı Odalı Kale Haritası:
    /// - 1. BÖLÜM (Kale Dışı & Hendek): Oyuncu ve Yardımcı Karakter kalenin dışında başlar.
    ///   Kalenin önündeki derin su hendeğini (Moat) geçmek için Eşzamanlı 2 Anahtarlı Co-op mekanizmasıyla
    ///   Askı Kapı (Asma Köprü) indirilir.
    /// - 2. BÖLÜM (İç Avlu, Ayrı Kablo/Şalter Odası ve Taret Odası): Eğilme (C/Ctrl) siperleri,
    ///   duvarlarla ayrılmış Elektrik & Kablo Kontrol Odası (KapiBulmacaPaneli) ve Kilitli Taret Odası.
    /// - 3. BÖLÜM (Gerçek Çok Katlı İç Kule / Keep & LLM NPC Muhafız Odası): Taş merdivenler,
    ///   köşe burçlu gerçek kule mimarisi, 2. kat muhafız odası ve çıkış kapısı.
    /// </summary>
    public class HaritaOlusturucu : EditorWindow
    {
        [MenuItem("Tools/YazLab/1. Haritayı Sıfırdan Oluştur (Yuvarlak Kale Seviyesi)")]
        public static void HaritayiKur()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Uyari: Oyun Calisiyor!",
                    "Oyun su anda Play modunda calisiyor! Lutfen once ustteki Play butonuna basarak oyunu durdurun, ardindan haritayi olusturun.",
                    "Tamam");
                return;
            }

            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("3 Bölümlü Taktiksel Kale Haritası",
                "Taktiksel İstihbarat Operasyonu 3 Bölümlü Kale & Şato Haritası inşa edilecek:\n\n" +
                "• 1. BÖLÜM (Kale Dışı & Hendek): Derin su hendeği, Askı Kapı (Asma Köprü) ve Yardımcı Karakterle Eşzamanlı 2 Anahtar Kurgusu ([G] + [E])\n" +
                "• 2. BÖLÜM (Ayrı Odalar & Taret): Eğilme ([C]/[Ctrl]) siperleri, bağımsız Kablo & Şalter Odası ve Otomatik Savunma Tareti Odası\n" +
                "• 3. BÖLÜM (Gerçek Kale Kulesi & NPC): Köşe sütunlu, mazgallı 2 katlı İç Kule, taş merdivenler ve LLM NPC Muhafız Odası\n" +
                "• Prosedürel Örme Taş, Ahşap Kalas, Paslı Metal ve Su Kaplamaları\n\n" +
                "Devam edilsin mi?",
                "Evet, Oluştur", "İptal"))
            {
                return;
            }

            HaritayiKurInternal();

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("3 Bölümlü Kale Haritası Hazır!",
                    "3 Bölümlü Taktiksel Kale Haritası ve Prosedürel Kaplamalar başarıyla inşa edildi!\n\n" +
                    "✓ 1. BÖLÜM: Kale dışı başlangıç alanı, Hendek (Moat), Eşzamanlı 2 Anahtar Sütunu ([G] + [E]) ve Askı Kapı (Asma Köprü) kuruldu.\n" +
                    "✓ 2. BÖLÜM: Ayrı Kablo & Şalter Odası, Eğilme ([C]/[Ctrl]) siperleri ve Taret Savunma Odası birbirinden duvarlarla ayrıldı.\n" +
                    "✓ 3. BÖLÜM: Çok katlı gerçek İç Kule (Keep), 8 basamaklı taş merdiven ve 2. kat NPC Muhafız Odası inşa edildi.\n" +
                    "✓ Örme taş, ahşap kalas, paslı metal ve su kaplamaları tüm yüzeylere uygulandı.\n\n" +
                    "Şimdi Play (▶) tuşuna basarak 1. Bölümden (Kale Dışından) operasyona başlayabilirsiniz!", "Harika!");
            }
        }

        [MenuItem("Tools/YazLab/2. Karakterleri ve Animasyonları Sahneye Ekle (Otomatik)")]
        public static void KarakterleriSahneyeKurMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Uyari", "Lutfen once Play modunu durdurun.", "Tamam");
                return;
            }

            KarakterleriSahneyeKur();
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Başarılı",
                    "Oyuncu, Yoldaş ve NPC Muhafız karakterleri animasyonlarıyla birlikte sahneye eklendi ve 3. şahıs kamera bağlandı!",
                    "Tamam");
            }
        }

        [MenuItem("Tools/YazLab/3. Her İki Sahneyi de Tam Otomatik İnşa Et ve NavMesh Bake Et")]
        public static void HerIkiSahneyiOtomatikInsaEt()
        {
            if (EditorApplication.isPlaying) return;

            string[] sahneler = new string[] {
                "Assets/Scenes/SampleScene.unity",
                "Assets/Scenes/OyunSeviyesi.unity"
            };

            foreach (string sahneYolu in sahneler)
            {
                Debug.Log($"[YazLab] Sahne açılıyor ve harita oluşturuluyor: {sahneYolu}");
                var scene = EditorSceneManager.OpenScene(sahneYolu, OpenSceneMode.Single);
                HaritayiKurInternal();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[YazLab] Sahne başarıyla güncellendi ve kaydedildi: {sahneYolu}");
            }
        }

        public static void HaritayiKurInternal()
        {
            // Eski harita kökü varsa temizle
            GameObject eskiHarita = GameObject.Find("--- HARITA_SEVIYESI ---");
            if (eskiHarita != null)
            {
                DestroyImmediate(eskiHarita);
            }

            GameObject d1 = GameObject.Find("DusmanAsker_1");
            if (d1 != null) DestroyImmediate(d1);
            GameObject d2 = GameObject.Find("DusmanAsker_2");
            if (d2 != null) DestroyImmediate(d2);

            foreach (var d in Object.FindObjectsByType<DusmanKontrol>(FindObjectsInactive.Include))
            {
                if (d != null && d.gameObject != null && !(d is TaretKontrol))
                {
                    DestroyImmediate(d.gameObject);
                }
            }
            GameObject eskiKonsol = GameObject.Find("ElektrikSalterKonsolu");
            if (eskiKonsol != null) DestroyImmediate(eskiKonsol);

            GameObject haritaKok = new GameObject("--- HARITA_SEVIYESI ---");
            Undo.RegisterCreatedObjectUndo(haritaKok, "Harita Olusturuldu");

            // ==========================================
            // MALZEMELER & PROSEDÜREL KAPLAMALAR
            // ==========================================
            Material matToprak = YeniMateryal("Mat_ToprakZemin", new Color(0.36f, 0.31f, 0.24f));
            Material matTasDuvar = YeniMateryal("Mat_TasDuvar", new Color(0.52f, 0.49f, 0.45f));
            Material matKoyuTas = YeniMateryal("Mat_KoyuTas", new Color(0.34f, 0.32f, 0.30f));
            Material matKumTorbasi = YeniMateryal("Mat_KumTorbasi", new Color(0.62f, 0.56f, 0.42f));
            Material matPasliMetal = YeniMateryal("Mat_PasliMetal", new Color(0.42f, 0.27f, 0.19f));
            Material matAhsap = YeniMateryal("Mat_Ahsap", new Color(0.48f, 0.34f, 0.20f));
            Material matAskeriVaril = YeniMateryal("Mat_AskeriVaril", new Color(0.25f, 0.32f, 0.24f));
            Material matNpcKapi = YeniMateryal("Mat_NpcKapi", new Color(0.20f, 0.38f, 0.58f));
            Material matKirmiziTehlike = YeniMateryal("Mat_KirmiziTehlike", new Color(0.85f, 0.15f, 0.10f));
            Material matYesilAktif = YeniMateryal("Mat_YesilAktif", new Color(0.15f, 0.85f, 0.25f));
            Material matHendekSuyu = YeniMateryal("Mat_HendekSuyu", new Color(0.12f, 0.36f, 0.48f));
            Material matSariKablo = YeniMateryal("Mat_SariKablo", new Color(0.95f, 0.78f, 0.15f));

            // ==========================================
            // 1. ZEMİN & BÖLÜM 1: KALE DIŞI, HENDEK VE ASKI KAPI (ASMA KÖPRÜ)
            // ==========================================
            Vector3 kaleMerkezi = new Vector3(0f, 0f, 20f);
            float kaleYaricapi = 28f;

            GameObject zeminGrup = new GameObject("1_Bolum1_KaleDisi_Hendek_Ve_AsmaKopru");
            zeminGrup.transform.SetParent(haritaKok.transform);

            // Ana Zemin (Kale dışını da kapsayacak şekilde Z = -36'dan Z = +58'e uzanır)
            GameObject anaZemin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            anaZemin.name = "AnaZemin_Toprak";
            anaZemin.transform.SetParent(zeminGrup.transform);
            anaZemin.transform.position = new Vector3(0f, -0.5f, 12f);
            anaZemin.transform.localScale = new Vector3(78f, 1f, 96f);
            anaZemin.GetComponent<MeshRenderer>().sharedMaterial = matToprak;

            NavMeshSurface navSurface = anaZemin.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.All;

            // Kale Dışı Başlangıç Alanı Çevre Duvarları (Oyuncu dışarıda doğduğunda harita dışına çıkmasın)
            KutuOlustur("KaleDisi_SolKayalikDuvar", new Vector3(-16f, 2.5f, -19f), new Vector3(2.0f, 5.0f, 22f), matKoyuTas, zeminGrup.transform);
            KutuOlustur("KaleDisi_SagKayalikDuvar", new Vector3(16f, 2.5f, -19f), new Vector3(2.0f, 5.0f, 22f), matKoyuTas, zeminGrup.transform);
            KutuOlustur("KaleDisi_ArkaSinirDuvari", new Vector3(0f, 2.5f, -29f), new Vector3(34f, 5.0f, 2.0f), matKoyuTas, zeminGrup.transform);

            // Kale Dışı Taktiksel Başlangıç Kampı Detayları (Sandıklar, Variller, Meşaleler)
            SandikOlustur("DisKamp_Sandik1", new Vector3(-6.5f, 0.6f, -23f), new Vector3(1.4f, 1.2f, 1.4f), matAhsap, zeminGrup.transform);
            VarilOlustur("DisKamp_Varil1", new Vector3(6.5f, 0.75f, -23f), matAskeriVaril, zeminGrup.transform);
            MesaleOlustur("Mesale_DisKamp_Sol", new Vector3(-6.0f, 1.2f, -20f), zeminGrup.transform, matPasliMetal);
            MesaleOlustur("Mesale_DisKamp_Sag", new Vector3(6.0f, 1.2f, -20f), zeminGrup.transform, matPasliMetal);

            // --- KALE HENDEĞİ (MOAT - Z = -14.5 ile Z = -8.5 arası) ---
            GameObject hendekGrup = new GameObject("Kale_Hendegi_Moat");
            hendekGrup.transform.SetParent(zeminGrup.transform);

            // Hendek Taş Kenar Борdürleri
            KutuOlustur("Hendek_OnTasKenar", new Vector3(-9.5f, 0.18f, -14.8f), new Vector3(13f, 0.36f, 0.6f), matKoyuTas, hendekGrup.transform);
            KutuOlustur("Hendek_OnTasKenar_Sag", new Vector3(9.5f, 0.18f, -14.8f), new Vector3(13f, 0.36f, 0.6f), matKoyuTas, hendekGrup.transform);

            // Hendek Su Yüzeyi (Dalgalanan su)
            GameObject hendekSu = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hendekSu.name = "Hendek_Suyu";
            hendekSu.transform.SetParent(hendekGrup.transform);
            hendekSu.transform.position = new Vector3(0f, -0.02f, -11.6f);
            hendekSu.transform.localScale = new Vector3(30f, 0.12f, 6.2f);
            hendekSu.GetComponent<MeshRenderer>().sharedMaterial = matHendekSuyu;
            hendekSu.AddComponent<HendekSuyuDalgalanma>();

            // Köprü kapalıyken hendeğe düşülmesini / yürünmesini engelleyen görünmez bariyer
            // (Askı kapı indiğinde AsmaKopruKontrol tarafından devre dışı bırakılır!)
            GameObject hendekEngelObj = new GameObject("Hendek_Gecis_Engeli");
            hendekEngelObj.transform.SetParent(hendekGrup.transform);
            hendekEngelObj.transform.position = new Vector3(0f, 2.0f, -11.6f);
            BoxCollider hendekOrtaEngel = hendekEngelObj.AddComponent<BoxCollider>();
            hendekOrtaEngel.size = new Vector3(7.0f, 4.5f, 5.8f);

            // Köprünün sol ve sağındaki açık hendek suyuna hiçbir zaman düşülmemesi için kalıcı yan su bariyerleri
            GameObject solSuEngel = new GameObject("Hendek_SolSuSiniri");
            solSuEngel.transform.SetParent(hendekGrup.transform);
            solSuEngel.transform.position = new Vector3(-9.8f, 2.0f, -11.6f);
            solSuEngel.AddComponent<BoxCollider>().size = new Vector3(12.5f, 4.5f, 6.2f);

            GameObject sagSuEngel = new GameObject("Hendek_SagSuSiniri");
            sagSuEngel.transform.SetParent(hendekGrup.transform);
            sagSuEngel.transform.position = new Vector3(9.8f, 2.0f, -11.6f);
            sagSuEngel.AddComponent<BoxCollider>().size = new Vector3(12.5f, 4.5f, 6.2f);

            // --- ASKI KAPI / ASMA KÖPRÜ (DRAWBRIDGE - Z = -8.6) ---
            GameObject asmaKopruKok = new GameObject("Asma_Kopru_Sistemi");
            asmaKopruKok.transform.SetParent(zeminGrup.transform);
            asmaKopruKok.transform.position = new Vector3(0f, 0.08f, -8.6f);

            // Köprü Menteşesi (Pivot: Z = -8.6'da, açıkken -Z yönüne doğru hendeğin üstünü kapatır)
            GameObject kopruMentese = new GameObject("Kopru_Mentese_Pivot");
            kopruMentese.transform.SetParent(asmaKopruKok.transform);
            kopruMentese.transform.localPosition = Vector3.zero;

            // Ahşap Köprü Gövdesi (Uzunluk: 6.4m, Genişlik: 5.2m)
            GameObject kopruGovde = GameObject.CreatePrimitive(PrimitiveType.Cube);
            kopruGovde.name = "AsmaKopru_AhsapGovde";
            kopruGovde.transform.SetParent(kopruMentese.transform);
            kopruGovde.transform.localPosition = new Vector3(0f, 0f, -3.2f);
            kopruGovde.transform.localScale = new Vector3(5.2f, 0.28f, 6.4f);
            kopruGovde.GetComponent<MeshRenderer>().sharedMaterial = matAhsap;

            // Köprü Yan Demir Kuşakları ve Korkulukları
            KutuOlustur("Kopru_SolDemirKusak", kopruMentese.transform.position + new Vector3(-2.45f, 0.25f, -3.2f), new Vector3(0.22f, 0.35f, 6.4f), matPasliMetal, kopruMentese.transform);
            KutuOlustur("Kopru_SagDemirKusak", kopruMentese.transform.position + new Vector3(2.45f, 0.25f, -3.2f), new Vector3(0.22f, 0.35f, 6.4f), matPasliMetal, kopruMentese.transform);

            // Zincir Makaraları (Giriş kulesi kirişinde)
            GameObject solMakara = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            solMakara.name = "Sol_Zincir_Makarasi";
            solMakara.transform.SetParent(asmaKopruKok.transform);
            solMakara.transform.localPosition = new Vector3(-2.6f, 5.2f, -0.4f);
            solMakara.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            solMakara.transform.localScale = new Vector3(0.8f, 0.3f, 0.8f);
            solMakara.GetComponent<MeshRenderer>().sharedMaterial = matPasliMetal;

            GameObject sagMakara = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sagMakara.name = "Sag_Zincir_Makarasi";
            sagMakara.transform.SetParent(asmaKopruKok.transform);
            sagMakara.transform.localPosition = new Vector3(2.6f, 5.2f, -0.4f);
            sagMakara.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            sagMakara.transform.localScale = new Vector3(0.8f, 0.3f, 0.8f);
            sagMakara.GetComponent<MeshRenderer>().sharedMaterial = matPasliMetal;

            AsmaKopruKontrol asmaKopruKontrol = asmaKopruKok.AddComponent<AsmaKopruKontrol>();
            asmaKopruKontrol.kopruMentese = kopruMentese.transform;
            asmaKopruKontrol.solMakara = solMakara.transform;
            asmaKopruKontrol.sagMakara = sagMakara.transform;
            asmaKopruKontrol.hendekGecisEngeli = hendekOrtaEngel;
            asmaKopruKontrol.kapaliAciX = -78f;
            asmaKopruKontrol.acikAciX = 0f;

            // --- EŞZAMANLI 2 ANAHTARLI CO-OP KÖPRÜ KİLİDİ (SOL: OYUNCU [E], SAĞ: YARDIMCI KARAKTER [G]) ---
            GameObject ciftAnahtarObj = new GameObject("Cift_Anahtarli_Kopru_Kilidi");
            ciftAnahtarObj.transform.SetParent(zeminGrup.transform);

            // Sol Anahtar Sütunu (X = -5.2, Z = -16.2)
            GameObject solSutun = KutuOlustur("Sol_Anahtar_Sutunu", new Vector3(-5.2f, 0.9f, -16.2f), new Vector3(0.9f, 1.8f, 0.9f), matKoyuTas, ciftAnahtarObj.transform);
            GameObject solKol = KutuOlustur("Sol_Anahtar_Kolu", new Vector3(-5.2f, 1.3f, -16.75f), new Vector3(0.18f, 0.65f, 0.18f), matPasliMetal, solSutun.transform);
            GameObject solIsik = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            solIsik.name = "Sol_Anahtar_Isigi";
            solIsik.transform.SetParent(solSutun.transform);
            solIsik.transform.position = new Vector3(-5.2f, 2.0f, -16.2f);
            solIsik.transform.localScale = new Vector3(0.38f, 0.38f, 0.38f);
            MeshRenderer solIsikRend = solIsik.GetComponent<MeshRenderer>();
            solIsikRend.sharedMaterial = new Material(matKirmiziTehlike);

            // Sağ Anahtar Sütunu (X = +5.2, Z = -16.2)
            GameObject sagSutun = KutuOlustur("Sag_Anahtar_Sutunu", new Vector3(5.2f, 0.9f, -16.2f), new Vector3(0.9f, 1.8f, 0.9f), matKoyuTas, ciftAnahtarObj.transform);
            GameObject sagKol = KutuOlustur("Sag_Anahtar_Kolu", new Vector3(5.2f, 1.3f, -16.75f), new Vector3(0.18f, 0.65f, 0.18f), matPasliMetal, sagSutun.transform);
            GameObject sagIsik = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sagIsik.name = "Sag_Anahtar_Isigi";
            sagIsik.transform.SetParent(sagSutun.transform);
            sagIsik.transform.position = new Vector3(5.2f, 2.0f, -16.2f);
            sagIsik.transform.localScale = new Vector3(0.38f, 0.38f, 0.38f);
            MeshRenderer sagIsikRend = sagIsik.GetComponent<MeshRenderer>();
            sagIsikRend.sharedMaterial = new Material(matKirmiziTehlike);

            CiftAnahtarliKopruSistemi ciftAnahtarSistemi = ciftAnahtarObj.AddComponent<CiftAnahtarliKopruSistemi>();
            ciftAnahtarSistemi.bagliKopru = asmaKopruKontrol;
            ciftAnahtarSistemi.solAnahtarSutunu = solSutun.transform;
            ciftAnahtarSistemi.sagAnahtarSutunu = sagSutun.transform;
            ciftAnahtarSistemi.solAnahtarKolu = solKol.transform;
            ciftAnahtarSistemi.sagAnahtarKolu = sagKol.transform;
            ciftAnahtarSistemi.solIsikRenderer = solIsikRend;
            ciftAnahtarSistemi.sagIsikRenderer = sagIsikRend;

            // ==========================================
            // 2. YUVARLAK KALE DIŞ SURLARI & GÜNEY GİRİŞ KULESİ (GATEHOUSE)
            // ==========================================
            GameObject surGrup = new GameObject("2_Yuvarlak_Kale_Surlari");
            surGrup.transform.SetParent(haritaKok.transform);

            int segmentSayisi = 24;
            float aciAdimi = 360f / segmentSayisi;

            for (int i = 0; i < segmentSayisi; i++)
            {
                // Güney Giriş Kapısı (i == 12, Z = -8) Asma Köprü geçidi için açıktır; iki yanına ikiz giriş burcu dikilir!
                if (i == 12) continue;

                float aciRad = i * aciAdimi * Mathf.Deg2Rad;
                float x = kaleMerkezi.x + Mathf.Sin(aciRad) * kaleYaricapi;
                float z = kaleMerkezi.z + Mathf.Cos(aciRad) * kaleYaricapi;

                float surYuksekligi = 8.0f;
                Vector3 surScale = new Vector3(7.6f, surYuksekligi, 2.4f);
                Vector3 surPos = new Vector3(x, surYuksekligi / 2f, z);

                GameObject surParcasi = GameObject.CreatePrimitive(PrimitiveType.Cube);
                surParcasi.name = $"SurSegment_{i}";
                surParcasi.transform.SetParent(surGrup.transform);
                surParcasi.transform.position = surPos;
                surParcasi.transform.localScale = surScale;
                surParcasi.transform.rotation = Quaternion.Euler(0f, i * aciAdimi + 90f, 0f);
                surParcasi.GetComponent<MeshRenderer>().sharedMaterial = matTasDuvar;

                // Sur üstü savunma mazgalları (Crenellations)
                for (int m = -2; m <= 2; m += 2)
                {
                    GameObject mazgal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    mazgal.name = $"Mazgal_{i}_{m}";
                    mazgal.transform.SetParent(surParcasi.transform);
                    mazgal.transform.localPosition = new Vector3(m * 0.22f, 0.58f, 0f);
                    mazgal.transform.localScale = new Vector3(0.18f, 0.18f, 0.95f);
                    mazgal.GetComponent<MeshRenderer>().sharedMaterial = matKoyuTas;
                }
            }

            // Güney Ana Giriş İkiz Kuleleri (Gatehouse Towers) ve Üst Kemeri
            BurcOlustur("GuneyGirisKulesi_Sol", new Vector3(-4.6f, 0f, -8.2f), 2.4f, 10.5f, matKoyuTas, surGrup.transform);
            BurcOlustur("GuneyGirisKulesi_Sag", new Vector3(4.6f, 0f, -8.2f), 2.4f, 10.5f, matKoyuTas, surGrup.transform);
            KutuOlustur("GuneyGiris_UstKemer", new Vector3(0f, 7.2f, -8.2f), new Vector3(9.2f, 3.2f, 3.0f), matTasDuvar, surGrup.transform);
            BayrakDiregiOlustur("GirisKulesi_SolSancak", new Vector3(-4.6f, 10.5f, -8.2f), surGrup.transform, matAhsap, matKirmiziTehlike);
            BayrakDiregiOlustur("GirisKulesi_SagSancak", new Vector3(4.6f, 10.5f, -8.2f), surGrup.transform, matAhsap, matKirmiziTehlike);

            // Dış Köşe Gözetleme Burçları & Dinamik Ekipmanlar
            BurcOlustur("KuzeyBurcu", new Vector3(0f, 0f, 48f), 5.0f, 10f, matKoyuTas, surGrup.transform);
            BurcOlustur("DoguBurcu", new Vector3(27f, 0f, 20f), 4.5f, 9f, matKoyuTas, surGrup.transform);
            BurcOlustur("BatiBurcu", new Vector3(-27f, 0f, 20f), 4.5f, 9f, matKoyuTas, surGrup.transform);

            ProjektorKulesiOlustur("KuzeyProjektoru", new Vector3(0f, 10.2f, 48f), 180f, surGrup.transform, matPasliMetal);
            ProjektorKulesiOlustur("DoguProjektoru", new Vector3(27f, 9.2f, 20f), 270f, surGrup.transform, matPasliMetal);
            MesaleOlustur("Mesale_BatiBurcu", new Vector3(-27f, 9.2f, 20f), surGrup.transform, matPasliMetal);
            MesaleOlustur("Mesale_Giris_Sol", new Vector3(-3.2f, 4.2f, -9.8f), surGrup.transform, matPasliMetal);
            MesaleOlustur("Mesale_Giris_Sag", new Vector3(3.2f, 4.2f, -9.8f), surGrup.transform, matPasliMetal);

            // ==========================================
            // 3. BÖLÜM 2 (ODA 1 & ODA 2): İÇ AVLU, BAĞIMSIZ KABLO ODASI VE TARET ODASI
            // ==========================================
            GameObject bolge2 = new GameObject("3_Bolum2_Avlu_KabloOdasi_Ve_TaretOdasi");
            bolge2.transform.SetParent(haritaKok.transform);

            // --- ODA 1: SİPERLİ GİRİŞ AVLUSU (Eğilme [C]/[Ctrl] ile tam gizlilik sağlayan 1.25m siperler) ---
            KutuOlustur("Siper_Sol_1", new Vector3(-4.5f, 0.62f, -2.0f), new Vector3(4.2f, 1.25f, 0.9f), matKoyuTas, bolge2.transform);
            KutuOlustur("Siper_Sag_1", new Vector3(4.8f, 0.62f, 0.5f), new Vector3(4.5f, 1.25f, 0.9f), matKoyuTas, bolge2.transform);
            KutuOlustur("Siper_Orta_2", new Vector3(-0.5f, 0.62f, 5.5f), new Vector3(5.2f, 1.25f, 0.9f), matKoyuTas, bolge2.transform);
            KutuOlustur("Siper_Ileri_Sol", new Vector3(-5.0f, 0.62f, 9.5f), new Vector3(4.0f, 1.25f, 0.9f), matKoyuTas, bolge2.transform);
            KutuOlustur("Siper_Ileri_Sag", new Vector3(5.0f, 0.62f, 9.5f), new Vector3(4.0f, 1.25f, 0.9f), matKoyuTas, bolge2.transform);

            KumTorbasiSirasi("KumTorbasi_Sol", new Vector3(-6.0f, 0.4f, -1.2f), 4, matKumTorbasi, bolge2.transform);
            KumTorbasiSirasi("KumTorbasi_Sag", new Vector3(3.5f, 0.4f, 1.3f), 4, matKumTorbasi, bolge2.transform);
            KumTorbasiSirasi("KumTorbasi_Orta", new Vector3(-2.2f, 0.4f, 6.3f), 5, matKumTorbasi, bolge2.transform);

            // --- BAĞIMSIZ KABLO & ŞALTER ODASI (Sol Avluda Duvarlarla ve Çatıyla Ayrılmış Oda: X = -17 ile -8, Z = -1 ile +9) ---
            GameObject kabloOdasi = new GameObject("Kablo_Ve_Salter_Odasi");
            kabloOdasi.transform.SetParent(bolge2.transform);

            KutuOlustur("KabloOdasi_ArkaDuvar", new Vector3(-16.5f, 2.2f, 4.0f), new Vector3(0.8f, 4.4f, 10.0f), matTasDuvar, kabloOdasi.transform);
            KutuOlustur("KabloOdasi_GuneyDuvar", new Vector3(-12.5f, 2.2f, -0.8f), new Vector3(8.5f, 4.4f, 0.8f), matTasDuvar, kabloOdasi.transform);
            KutuOlustur("KabloOdasi_KuzeyDuvar", new Vector3(-12.5f, 2.2f, 8.8f), new Vector3(8.5f, 4.4f, 0.8f), matTasDuvar, kabloOdasi.transform);
            // Doğu ön cephe duvarları (Ortada 3.2m genişliğinde oda giriş kapısı boşluğu bırakır)
            KutuOlustur("KabloOdasi_OnDuvar_Alt", new Vector3(-8.5f, 2.2f, 0.8f), new Vector3(0.8f, 4.4f, 3.2f), matTasDuvar, kabloOdasi.transform);
            KutuOlustur("KabloOdasi_OnDuvar_Ust", new Vector3(-8.5f, 2.2f, 7.2f), new Vector3(0.8f, 4.4f, 3.2f), matTasDuvar, kabloOdasi.transform);
            KutuOlustur("KabloOdasi_KapiKirisi", new Vector3(-8.5f, 3.8f, 4.0f), new Vector3(0.9f, 1.2f, 3.4f), matKoyuTas, kabloOdasi.transform);
            KutuOlustur("KabloOdasi_Tavan", new Vector3(-12.5f, 4.5f, 4.0f), new Vector3(8.8f, 0.4f, 10.2f), matKoyuTas, kabloOdasi.transform);

            // Oda içi aydınlatma
            MesaleOlustur("KabloOdasi_IcLamba", new Vector3(-15.5f, 2.6f, 4.0f), kabloOdasi.transform, matPasliMetal);

            // Kablo Odasından Kilitli Taret Kapısına Uzanan Görsel Güç Kablosu Hatları
            KutuOlustur("GucKablosu_1", new Vector3(-12.0f, 0.08f, 4.0f), new Vector3(6.5f, 0.12f, 0.18f), matSariKablo, kabloOdasi.transform);
            KutuOlustur("GucKablosu_2", new Vector3(-8.8f, 0.08f, 9.0f), new Vector3(0.18f, 0.12f, 10.0f), matSariKablo, kabloOdasi.transform);
            KutuOlustur("GucKablosu_3", new Vector3(-4.4f, 0.08f, 13.8f), new Vector3(9.0f, 0.12f, 0.18f), matSariKablo, kabloOdasi.transform);

            // --- ODA 2 (TARET ODASI) İLE ODA 1 ARASINDAKİ BÖLME DUVARI VE KİLİTLİ KAPI (Z = 14) ---
            KutuOlustur("DepoDuvar_Sol", new Vector3(-14.5f, 3.0f, 14f), new Vector3(23f, 6.0f, 1.5f), matTasDuvar, bolge2.transform);
            KutuOlustur("DepoDuvar_Sag", new Vector3(11.5f, 3.0f, 14f), new Vector3(17f, 6.0f, 1.5f), matTasDuvar, bolge2.transform);
            KutuOlustur("DepoKapi_UstKemer", new Vector3(0f, 5.0f, 14f), new Vector3(6.4f, 2.0f, 1.7f), matKoyuTas, bolge2.transform);

            // Kilitli Taret Odası Kapısı
            GameObject engelKapiObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            engelKapiObj.name = "FizikselEngelKapisi";
            engelKapiObj.transform.SetParent(bolge2.transform);
            engelKapiObj.transform.position = new Vector3(0f, 2.0f, 14f);
            engelKapiObj.transform.localScale = new Vector3(6.0f, 4.0f, 0.5f);
            engelKapiObj.GetComponent<MeshRenderer>().sharedMaterial = matPasliMetal;

            NavMeshObstacle kapiObstacle = engelKapiObj.AddComponent<NavMeshObstacle>();
            kapiObstacle.carving = true;
            kapiObstacle.size = new Vector3(1f, 1f, 1f);

            KapiKontrol engelKapiKontrol = engelKapiObj.AddComponent<KapiKontrol>();

            // --- ELEKTRİK & KABLO BULMACA KONSOLU (Kablo Odasının İçinde: X = -14.2, Z = 4.0) ---
            GameObject konsolObj = new GameObject("ElektrikSalterKonsolu");
            konsolObj.transform.SetParent(kabloOdasi.transform);
            konsolObj.transform.position = new Vector3(-14.2f, 0f, 4.0f);
            konsolObj.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            KapiBulmacaPaneli bulmacaPaneli = konsolObj.AddComponent<KapiBulmacaPaneli>();
            bulmacaPaneli.bagliKapi = engelKapiKontrol;

            // --- OTOMATİK SAVUNMA TARETİ (TARET ODASINDA, Z = 17.5) ---
            GameObject taretKulesi = new GameObject("Otomatik_Taret_Kulesi");
            taretKulesi.transform.SetParent(bolge2.transform);
            taretKulesi.transform.position = new Vector3(0f, 0f, 17.5f);

            KutuOlustur("Taret_Kaide", new Vector3(0f, 1.5f, 17.5f), new Vector3(3.2f, 3f, 3.2f), matKoyuTas, taretKulesi.transform);

            GameObject kuleBasi = new GameObject("Taret_KuleBasi");
            kuleBasi.transform.SetParent(taretKulesi.transform);
            kuleBasi.transform.position = new Vector3(0f, 3.2f, 17.5f);

            GameObject taretGovde = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            taretGovde.name = "Taret_Govdesi";
            taretGovde.transform.SetParent(kuleBasi.transform);
            taretGovde.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            taretGovde.transform.localScale = new Vector3(2.0f, 0.5f, 2.0f);
            taretGovde.GetComponent<MeshRenderer>().sharedMaterial = matPasliMetal;

            GameObject namlu1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            namlu1.name = "Taret_Namlu1";
            namlu1.transform.SetParent(kuleBasi.transform);
            namlu1.transform.localPosition = new Vector3(-0.45f, 0.2f, 1.3f);
            namlu1.transform.localScale = new Vector3(0.24f, 0.24f, 2.2f);
            namlu1.GetComponent<MeshRenderer>().sharedMaterial = matPasliMetal;

            GameObject namlu2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            namlu2.name = "Taret_Namlu2";
            namlu2.transform.SetParent(kuleBasi.transform);
            namlu2.transform.localPosition = new Vector3(0.45f, 0.2f, 1.3f);
            namlu2.transform.localScale = new Vector3(0.24f, 0.24f, 2.2f);
            namlu2.GetComponent<MeshRenderer>().sharedMaterial = matPasliMetal;

            GameObject ucu1 = new GameObject("NamluUcu");
            ucu1.transform.SetParent(namlu1.transform);
            ucu1.transform.localPosition = new Vector3(0f, 0f, 1.15f);

            GameObject ucu2 = new GameObject("NamluUcu");
            ucu2.transform.SetParent(namlu2.transform);
            ucu2.transform.localPosition = new Vector3(0f, 0f, 1.15f);

            GameObject taretIsik = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            taretIsik.name = "Taret_TehditIsigi";
            taretIsik.transform.SetParent(kuleBasi.transform);
            taretIsik.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            taretIsik.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            MeshRenderer taretIsikRend = taretIsik.GetComponent<MeshRenderer>();
            taretIsikRend.sharedMaterial = matKirmiziTehlike;

            Light pointLight = taretIsik.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = Color.red;
            pointLight.range = 10f;
            pointLight.intensity = 2.5f;

            BoxCollider taretCol = taretKulesi.AddComponent<BoxCollider>();
            taretCol.center = new Vector3(0f, 2.2f, 0f);
            taretCol.size = new Vector3(3.6f, 4.4f, 3.6f);

            TaretKontrol tk = taretKulesi.AddComponent<TaretKontrol>();
            tk.kuleBasi = kuleBasi.transform;
            tk.namluSol = namlu1.transform;
            tk.namluSag = namlu2.transform;
            tk.namluUcuSol = ucu1.transform;
            tk.namluUcuSag = ucu2.transform;
            tk.sensorIsikRenderer = taretIsikRend;
            tk.sensorPointLight = pointLight;
            tk.maksimumCan = 200f;
            tk.mevcutCan = 200f;
            tk.atesHasari = 9f;
            tk.atesAraligi = 0.42f;
            tk.recoilMiktari = 0.35f;

            // --- DOĞU SURLARI BOYUNCA GİZLİ YAN KORİDOR VE YEDEK JENERATÖR ODASI ---
            GameObject yanYol = new GameObject("Yoldas_Gizli_Yan_Yolu");
            yanYol.transform.SetParent(bolge2.transform);

            KutuOlustur("YanYol_SiperDuvar1", new Vector3(17.5f, 1.2f, 9f), new Vector3(1.2f, 2.4f, 8f), matTasDuvar, yanYol.transform);
            KutuOlustur("YanYol_SiperDuvar2", new Vector3(18f, 1.2f, 17f), new Vector3(1.2f, 2.4f, 6f), matTasDuvar, yanYol.transform);

            GameObject jeneratorObj = new GameObject("Jenerator_Ve_GucKaynagi");
            jeneratorObj.transform.SetParent(bolge2.transform);
            jeneratorObj.transform.position = new Vector3(19.0f, 0f, 20.5f);

            KutuOlustur("Jenerator_Kabin", new Vector3(19.0f, 1.0f, 20.5f), new Vector3(2.2f, 2.0f, 1.6f), matPasliMetal, jeneratorObj.transform);

            GameObject salterKol = GameObject.CreatePrimitive(PrimitiveType.Cube);
            salterKol.name = "Salter_Kol";
            salterKol.transform.SetParent(jeneratorObj.transform);
            salterKol.transform.position = new Vector3(17.8f, 1.3f, 20.5f);
            salterKol.transform.localScale = new Vector3(0.2f, 0.6f, 0.2f);
            salterKol.GetComponent<MeshRenderer>().sharedMaterial = matAhsap;

            GameObject durumIsigi = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            durumIsigi.name = "Jenerator_DurumIsigi";
            durumIsigi.transform.SetParent(jeneratorObj.transform);
            durumIsigi.transform.position = new Vector3(17.8f, 1.8f, 20.5f);
            durumIsigi.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            MeshRenderer durumIsikRend = durumIsigi.GetComponent<MeshRenderer>();
            durumIsikRend.sharedMaterial = matKirmiziTehlike;

            SphereCollider salterCol = jeneratorObj.AddComponent<SphereCollider>();
            salterCol.isTrigger = true;
            salterCol.radius = 2.2f;

            SalterMekanizmasi salterMek = jeneratorObj.AddComponent<SalterMekanizmasi>();
            SerializedObject soSalter = new SerializedObject(salterMek);
            soSalter.FindProperty("bagliKapi").objectReferenceValue = engelKapiKontrol;
            soSalter.FindProperty("salterKolu").objectReferenceValue = salterKol.transform;
            soSalter.FindProperty("durumIsigi").objectReferenceValue = durumIsikRend;
            soSalter.FindProperty("aktifRenk").colorValue = matYesilAktif.color;
            soSalter.FindProperty("pasifRenk").colorValue = matKirmiziTehlike.color;
            soSalter.FindProperty("tetiklemeYontemi").enumValueIndex = 1;
            soSalter.ApplyModifiedProperties();

            // ==========================================
            // 4. BÖLÜM 3 (ODA 3): GERÇEK ÇOK KATLI İÇ KALE KULESİ (KEEP), MERDİVENLER VE NPC ODASI
            // ==========================================
            GameObject bolge3 = new GameObject("4_Bolum3_IcKaleKulesi_Ve_NpcOdasi");
            bolge3.transform.SetParent(haritaKok.transform);

            // Taret Odası ile İç Kuleyi Ayıran Kemerli Ön Cephe Duvarı (Z = 22.0)
            KutuOlustur("IcKule_OnDuvar_Sol", new Vector3(-6.2f, 4.5f, 22.0f), new Vector3(6.5f, 9.0f, 1.4f), matTasDuvar, bolge3.transform);
            KutuOlustur("IcKule_OnDuvar_Sag", new Vector3(6.2f, 4.5f, 22.0f), new Vector3(6.5f, 9.0f, 1.4f), matTasDuvar, bolge3.transform);
            KutuOlustur("IcKule_OnGiris_UstKemer", new Vector3(0f, 7.2f, 22.0f), new Vector3(6.2f, 3.6f, 1.6f), matKoyuTas, bolge3.transform);

            // Gerçek Kule Mimarisi: 4 Köşe Silindirik Kule Burcu (Keep Corner Turrets)
            BurcOlustur("IcKule_Burc_SolOn", new Vector3(-9.2f, 0f, 22.5f), 2.0f, 11.5f, matKoyuTas, bolge3.transform);
            BurcOlustur("IcKule_Burc_SagOn", new Vector3(9.2f, 0f, 22.5f), 2.0f, 11.5f, matKoyuTas, bolge3.transform);
            BurcOlustur("IcKule_Burc_SolArka", new Vector3(-9.2f, 0f, 44.0f), 2.2f, 12.5f, matKoyuTas, bolge3.transform);
            BurcOlustur("IcKule_Burc_SagArka", new Vector3(9.2f, 0f, 44.0f), 2.2f, 12.5f, matKoyuTas, bolge3.transform);

            // Kule Tepesi Radar ve Dalgalanan Sancak
            RadarKulesiOlustur("MerkezKule_Radari", new Vector3(0f, 10.5f, 44f), bolge3.transform, matPasliMetal);
            BayrakDiregiOlustur("MerkezKule_Sancagi", new Vector3(4.5f, 10.2f, 44f), bolge3.transform, matAhsap, matKirmiziTehlike);

            // İç Kule Yan ve Arka Duvarları
            KutuOlustur("KuleDuvar_Arka", new Vector3(0f, 5.0f, 44f), new Vector3(18f, 10f, 2f), matTasDuvar, bolge3.transform);
            KutuOlustur("KuleDuvar_Sol", new Vector3(-8.8f, 4.8f, 33.2f), new Vector3(1.8f, 9.6f, 22f), matTasDuvar, bolge3.transform);
            KutuOlustur("KuleDuvar_Sag", new Vector3(8.8f, 4.8f, 33.2f), new Vector3(1.8f, 9.6f, 22f), matTasDuvar, bolge3.transform);

            // Kule Üst Çatı Kemerleri ve Mazgalları (Gerçek bina/şato hissi)
            KutuOlustur("Kule_UstCatiKirisi_1", new Vector3(0f, 9.4f, 28f), new Vector3(18.5f, 0.8f, 1.5f), matKoyuTas, bolge3.transform);
            KutuOlustur("Kule_UstCatiKirisi_2", new Vector3(0f, 9.4f, 34f), new Vector3(18.5f, 0.8f, 1.5f), matKoyuTas, bolge3.transform);
            KutuOlustur("Kule_UstCatiKirisi_3", new Vector3(0f, 9.4f, 40f), new Vector3(18.5f, 0.8f, 1.5f), matKoyuTas, bolge3.transform);

            // 2. Kat Gözetleme & Muhafız Odası Platformu (Yükseklik = 2.2m)
            GameObject ustPlatform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ustPlatform.name = "Kule_IkinciKat_Platformu";
            ustPlatform.transform.SetParent(bolge3.transform);
            ustPlatform.transform.position = new Vector3(0f, 2.2f, 36.5f);
            ustPlatform.transform.localScale = new Vector3(16.5f, 0.4f, 15f);
            ustPlatform.GetComponent<MeshRenderer>().sharedMaterial = matKoyuTas;

            // 2. Kat Oda Bölme Duvarları ve Kemerli Kapısı (Merdiven sahanlığı ile NPC odasını ayırır)
            KutuOlustur("NpcOdasi_BolmeDuvar_Sol", new Vector3(-5.5f, 4.8f, 30.5f), new Vector3(6.0f, 5.0f, 0.9f), matTasDuvar, bolge3.transform);
            KutuOlustur("NpcOdasi_BolmeDuvar_Sag", new Vector3(5.5f, 4.8f, 30.5f), new Vector3(6.0f, 5.0f, 0.9f), matTasDuvar, bolge3.transform);
            KutuOlustur("NpcOdasi_KapiKirisi", new Vector3(0f, 6.6f, 30.5f), new Vector3(5.2f, 1.4f, 1.1f), matKoyuTas, bolge3.transform);

            // Kule İçi Meşaleler
            MesaleOlustur("Kule_IcMesale_Sol", new Vector3(-7.4f, 4.8f, 35f), bolge3.transform, matPasliMetal);
            MesaleOlustur("Kule_IcMesale_Sag", new Vector3(7.4f, 4.8f, 35f), bolge3.transform, matPasliMetal);

            // Kademeli Taş Merdivenler (Z = 22.5'ten Z = 29.3'e, Yükseklik = 0'dan 2.2m'ye)
            GameObject merdivenGrup = new GameObject("Tas_Merdivenler");
            merdivenGrup.transform.SetParent(bolge3.transform);

            int basamakSayisi = 8;
            float basamakYukseklik = 2.2f / basamakSayisi;
            float basamakDerinlik = 0.85f;
            float basamakGenislik = 5.0f;
            float baslangicZ = 22.5f;

            for (int b = 0; b < basamakSayisi; b++)
            {
                float basamakY = (b + 1) * basamakYukseklik;
                float bz = baslangicZ + b * basamakDerinlik;
                GameObject basamak = GameObject.CreatePrimitive(PrimitiveType.Cube);
                basamak.name = $"Basamak_{b + 1}";
                basamak.transform.SetParent(merdivenGrup.transform);
                basamak.transform.position = new Vector3(0f, basamakY / 2f, bz);
                basamak.transform.localScale = new Vector3(basamakGenislik, basamakY, basamakDerinlik);
                basamak.GetComponent<MeshRenderer>().sharedMaterial = matKoyuTas;
            }

            KutuOlustur("Korkuluk_Sol", new Vector3(-2.6f, 1.6f, 25.5f), new Vector3(0.3f, 1.2f, 7.0f), matAhsap, merdivenGrup.transform);
            KutuOlustur("Korkuluk_Sag", new Vector3(2.6f, 1.6f, 25.5f), new Vector3(0.3f, 1.2f, 7.0f), matAhsap, merdivenGrup.transform);

            // Muhafız Nöbetçi Masası & Varil
            KutuOlustur("NobetciMasasi", new Vector3(2.2f, 2.8f, 38.5f), new Vector3(2.4f, 0.8f, 1.0f), matAhsap, bolge3.transform);
            VarilOlustur("NobetciVarili", new Vector3(4.0f, 3.1f, 38.5f), matAskeriVaril, bolge3.transform);

            // Büyük Çıkış Kapısı (Ollama LLM NPC'nin koruduğu son kapı)
            GameObject npcKapiObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            npcKapiObj.name = "NpcCikisKapisi";
            npcKapiObj.transform.SetParent(bolge3.transform);
            npcKapiObj.transform.position = new Vector3(0f, 4.4f, 43f);
            npcKapiObj.transform.localScale = new Vector3(6.5f, 4.4f, 0.6f);
            npcKapiObj.GetComponent<MeshRenderer>().sharedMaterial = matNpcKapi;

            NavMeshObstacle npcKapiObstacle = npcKapiObj.AddComponent<NavMeshObstacle>();
            npcKapiObstacle.carving = true;
            npcKapiObstacle.size = new Vector3(1f, 1f, 1f);

            KapiKontrol npcKapiKontrol = npcKapiObj.AddComponent<KapiKontrol>();

            // NPC Etkileşim Alanı
            GameObject npcTriggerObj = new GameObject("NPC_EtkilesimAlani");
            npcTriggerObj.transform.SetParent(bolge3.transform);
            npcTriggerObj.transform.position = new Vector3(0f, 2.5f, 36.5f);
            SphereCollider npcTriggerCol = npcTriggerObj.AddComponent<SphereCollider>();
            npcTriggerCol.isTrigger = true;
            npcTriggerCol.radius = 4.5f;
            AlanTetikleyici npcTetikleyici = npcTriggerObj.AddComponent<AlanTetikleyici>();

            // Bölüm Sonu Zafer Tetikleyicisi
            GameObject cikisAlani = new GameObject("BolumSonu_Tetikleyici");
            cikisAlani.transform.SetParent(bolge3.transform);
            cikisAlani.transform.position = new Vector3(0f, 3.5f, 46.5f);
            BoxCollider cikisCol = cikisAlani.AddComponent<BoxCollider>();
            cikisCol.isTrigger = true;
            cikisCol.size = new Vector3(7f, 4f, 4f);
            AlanTetikleyici cikisTetikleyici = cikisAlani.AddComponent<AlanTetikleyici>();

            // ==========================================
            // 5. BÖLÜM YÖNETİCİSİ (BOLUM YONETICISI)
            // ==========================================
            GameObject yoneticiObj = GameObject.Find("BolumYoneticisi");
            if (yoneticiObj == null) yoneticiObj = new GameObject("BolumYoneticisi");
            yoneticiObj.transform.SetParent(haritaKok.transform);

            BolumYoneticisi by = yoneticiObj.GetComponent<BolumYoneticisi>();
            if (by == null) by = yoneticiObj.AddComponent<BolumYoneticisi>();

            SerializedObject soBy = new SerializedObject(by);
            soBy.FindProperty("fizikselEngelKapisi").objectReferenceValue = engelKapiKontrol;
            soBy.FindProperty("salterMekanizmasi").objectReferenceValue = salterMek;
            soBy.FindProperty("npsCikisKapisi").objectReferenceValue = npcKapiKontrol;
            soBy.ApplyModifiedProperties();

            if (npcTetikleyici != null)
            {
                if (npcTetikleyici.onAlanaGirildi == null) npcTetikleyici.onAlanaGirildi = new UnityEngine.Events.UnityEvent();
                UnityEditor.Events.UnityEventTools.AddPersistentListener(npcTetikleyici.onAlanaGirildi, by.NpcEtkilesimBaslat);
                EditorUtility.SetDirty(npcTetikleyici);
            }

            if (cikisTetikleyici != null)
            {
                if (cikisTetikleyici.onAlanaGirildi == null) cikisTetikleyici.onAlanaGirildi = new UnityEngine.Events.UnityEvent();
                UnityEditor.Events.UnityEventTools.AddPersistentListener(cikisTetikleyici.onAlanaGirildi, by.OyunuTamamla);
                EditorUtility.SetDirty(cikisTetikleyici);
            }

            // ==========================================
            // 6. PROSEDÜREL DOKULARI HEMEN UYGULA
            // ==========================================
            ProsedurelDokuUretici.SahnedekiMateryalleriKapla();

            // ==========================================
            // 7. KARAKTERLER & KAMERA KURULUMU (1. BÖLÜM: KALE DIŞINDAN BAŞLAR!)
            // ==========================================
            KarakterleriSahneyeKur();

            // ==========================================
            // 8. NAVMESH SURFACE BAKE
            // ==========================================
            navSurface.BuildNavMesh();

            if (!EditorApplication.isPlaying)
            {
                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(activeScene);
                    EditorSceneManager.SaveScene(activeScene);
                }
            }
        }

        public static void KarakterleriSahneyeKur()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ToonSoldiers_WW2_demo/ToonSoldier_WW2_demo_Prefab.prefab");
            RuntimeAnimatorController animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/ToonSoldiers_WW2_demo/KarakterAnimator.controller");
            Material companionMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ToonSoldiers_WW2_demo/model/Materials/US_soldier_companion.mat");

            if (prefab == null || animCtrl == null)
            {
                Debug.LogError("[HaritaOlusturucu] Asker prefabı veya Animator Controller bulunamadı!");
                return;
            }

            // 1. OYUNCU (Kalenin Dışında, Hendeğin Önünde Başlar: Z = -21.5)
            Vector3 oyuncuBaslangic = new Vector3(-1.5f, 0.05f, -21.5f);
            KarakterHareket oyuncuHareket = Object.FindAnyObjectByType<KarakterHareket>();
            GameObject oyuncuObj;
            if (oyuncuHareket == null)
            {
                oyuncuObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                oyuncuObj.name = "Oyuncu";
                oyuncuObj.tag = "Player";
                oyuncuObj.transform.position = oyuncuBaslangic;
                oyuncuObj.transform.rotation = Quaternion.identity;

                CharacterController cc = oyuncuObj.GetComponent<CharacterController>();
                if (cc == null) cc = oyuncuObj.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.35f;
                cc.center = new Vector3(0f, 0.9f, 0f);

                oyuncuHareket = oyuncuObj.AddComponent<KarakterHareket>();
                oyuncuHareket.hiz = 5.5f;
                oyuncuHareket.egilmeHizi = 2.6f;
                oyuncuHareket.donusHizi = 12f;
                oyuncuHareket.ziplamaGucu = 6.5f;

                Animator anim = oyuncuObj.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.runtimeAnimatorController = animCtrl;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }

                Undo.RegisterCreatedObjectUndo(oyuncuObj, "Oyuncu Olusturuldu");
            }
            else
            {
                oyuncuObj = oyuncuHareket.gameObject;
                oyuncuObj.tag = "Player";
                oyuncuObj.transform.position = oyuncuBaslangic;
                oyuncuObj.transform.rotation = Quaternion.identity;
                Animator anim = oyuncuObj.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.runtimeAnimatorController = animCtrl;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
            }

            if (oyuncuObj.GetComponent<KarakterAtes>() == null)
            {
                KarakterAtes ka = oyuncuObj.AddComponent<KarakterAtes>();
                ka.seriAtes = true;
                ka.atesAraligi = 0.2f;
                ka.menzil = 100f;
                ka.vurusKuvveti = 250f;
            }
            if (oyuncuObj.GetComponent<OyuncuCani>() == null)
            {
                OyuncuCani oc = oyuncuObj.AddComponent<OyuncuCani>();
                oc.maksimumCan = 100f;
                oc.mevcutCan = 100f;
            }

            // 2. YOLDAŞ KARAKTER (Kalenin Dışında, Oyuncunun Yanında Başlar: Z = -21.5)
            Vector3 yoldasBaslangic = new Vector3(2.0f, 0.05f, -21.5f);
            YardimciKarakter yoldas = Object.FindAnyObjectByType<YardimciKarakter>();
            if (yoldas == null)
            {
                GameObject yoldasObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                yoldasObj.name = "YardimciKarakter";
                yoldasObj.tag = "Companion";
                yoldasObj.transform.position = yoldasBaslangic;
                yoldasObj.transform.rotation = Quaternion.identity;

                KarakterHareket kh = yoldasObj.GetComponent<KarakterHareket>();
                if (kh != null) Object.DestroyImmediate(kh);

                CharacterController cc = yoldasObj.GetComponent<CharacterController>();
                if (cc != null) Object.DestroyImmediate(cc);

                CapsuleCollider col = yoldasObj.GetComponent<CapsuleCollider>();
                if (col == null) col = yoldasObj.AddComponent<CapsuleCollider>();
                col.height = 1.8f;
                col.radius = 0.35f;
                col.center = new Vector3(0f, 0.9f, 0f);

                yoldas = yoldasObj.AddComponent<YardimciKarakter>();
                yoldas.hedef = oyuncuObj.transform;
                yoldas.donusModu = YardimciKarakter.DonusModu.OyuncuyaBak;
                yoldas.donusHizi = 8.0f;

                Animator anim = yoldasObj.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.runtimeAnimatorController = animCtrl;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }

                if (companionMat != null)
                {
                    SkinnedMeshRenderer smr = yoldasObj.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (smr != null) smr.sharedMaterial = companionMat;
                }

                Undo.RegisterCreatedObjectUndo(yoldasObj, "Yoldas Olusturuldu");
            }
            else
            {
                yoldas.transform.position = yoldasBaslangic;
                yoldas.transform.rotation = Quaternion.identity;
                yoldas.hedef = oyuncuObj.transform;
                Animator anim = yoldas.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.runtimeAnimatorController = animCtrl;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                if (companionMat != null)
                {
                    SkinnedMeshRenderer smr = yoldas.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (smr != null) smr.sharedMaterial = companionMat;
                }
            }

            // 3. RÜŞVETÇİ NPC MUHAFIZ (Kule 2. Kat Platformunda, Z = 38, Y = 2.45)
            Vector3 npcKonum = new Vector3(0f, 2.45f, 38.0f);
            GameObject npcMuhafiz = GameObject.Find("NPC_Muhafiz");
            if (npcMuhafiz == null)
            {
                npcMuhafiz = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                npcMuhafiz.name = "NPC_Muhafiz";
                npcMuhafiz.transform.position = npcKonum;
                npcMuhafiz.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                KarakterHareket kh = npcMuhafiz.GetComponent<KarakterHareket>();
                if (kh != null) Object.DestroyImmediate(kh);

                CharacterController cc = npcMuhafiz.GetComponent<CharacterController>();
                if (cc != null) Object.DestroyImmediate(cc);

                CapsuleCollider col = npcMuhafiz.GetComponent<CapsuleCollider>();
                if (col == null) col = npcMuhafiz.AddComponent<CapsuleCollider>();
                col.height = 1.8f;
                col.radius = 0.35f;
                col.center = new Vector3(0f, 0.9f, 0f);

                Animator anim = npcMuhafiz.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.runtimeAnimatorController = animCtrl;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }

                Undo.RegisterCreatedObjectUndo(npcMuhafiz, "NPC Muhafiz Olusturuldu");
            }
            else
            {
                npcMuhafiz.transform.position = npcKonum;
                npcMuhafiz.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                Animator anim = npcMuhafiz.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.runtimeAnimatorController = animCtrl;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
            }

            // 4. ANA KAMERA (3. ŞAHIS & SAĞ TIK 1. ŞAHIS KAMERA TAKİBİ)
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                OyuncuHareket oh = mainCam.GetComponent<OyuncuHareket>();
                if (oh != null) Object.DestroyImmediate(oh);

                KameraTakip kt = mainCam.GetComponent<KameraTakip>();
                if (kt == null) kt = mainCam.gameObject.AddComponent<KameraTakip>();

                kt.hedef = oyuncuObj.transform;
                kt.mesafe = 4.5f;
                kt.bakisYuksekligi = 1.4f;
                kt.fareHassasiyeti = 2.0f;
                kt.takipHizi = 12.0f;
                kt.donusHizi = 12.0f;
                kt.minPitch = -20f;
                kt.maxPitch = 60f;
                kt.sagTiklaBirinciSahis = true;

                oyuncuHareket.kamera = mainCam.transform;

                EditorUtility.SetDirty(kt);
            }
        }

        // ==========================================
        // YARDIMCI MİMARİ FONKSİYONLAR
        // ==========================================
        private static GameObject KutuOlustur(string isim, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            GameObject kutu = GameObject.CreatePrimitive(PrimitiveType.Cube);
            kutu.name = isim;
            kutu.transform.SetParent(parent);
            kutu.transform.position = pos;
            kutu.transform.localScale = scale;
            kutu.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return kutu;
        }

        private static GameObject BurcOlustur(string isim, Vector3 pos, float yaricap, float yukseklik, Material mat, Transform parent)
        {
            GameObject burc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            burc.name = isim;
            burc.transform.SetParent(parent);
            burc.transform.position = new Vector3(pos.x, yukseklik / 2f, pos.z);
            burc.transform.localScale = new Vector3(yaricap * 2f, yukseklik / 2f, yaricap * 2f);
            burc.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return burc;
        }

        private static void KumTorbasiSirasi(string isimKoku, Vector3 baslangic, int adet, Material mat, Transform parent)
        {
            for (int i = 0; i < adet; i++)
            {
                GameObject torba = GameObject.CreatePrimitive(PrimitiveType.Cube);
                torba.name = $"{isimKoku}_{i + 1}";
                torba.transform.SetParent(parent);
                torba.transform.position = baslangic + new Vector3(i * 0.9f, 0f, 0f);
                torba.transform.localScale = new Vector3(0.85f, 0.45f, 0.5f);
                torba.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        private static GameObject SandikOlustur(string isim, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            return KutuOlustur(isim, pos, scale, mat, parent);
        }

        private static GameObject VarilOlustur(string isim, Vector3 pos, Material mat, Transform parent)
        {
            GameObject varil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            varil.name = isim;
            varil.transform.SetParent(parent);
            varil.transform.position = pos;
            varil.transform.localScale = new Vector3(0.8f, 0.75f, 0.8f);
            varil.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return varil;
        }

        private static Material YeniMateryal(string isim, Color renk)
        {
            string klasor = "Assets/Materials";
            if (!AssetDatabase.IsValidFolder(klasor))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }

            string yol = $"{klasor}/{isim}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(yol);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");

                mat = new Material(shader);
                mat.color = renk;
                AssetDatabase.CreateAsset(mat, yol);
            }
            else
            {
                mat.color = renk;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        private static void ProjektorKulesiOlustur(string isim, Vector3 pos, float rotY, Transform parent, Material matMetal)
        {
            GameObject kule = new GameObject(isim);
            kule.transform.SetParent(parent);
            kule.transform.position = pos;
            kule.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            GameObject fenerGovde = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fenerGovde.name = "ProjektorGovde";
            fenerGovde.transform.SetParent(kule.transform);
            fenerGovde.transform.localPosition = Vector3.zero;
            fenerGovde.transform.localScale = new Vector3(0.9f, 0.4f, 0.9f);
            fenerGovde.GetComponent<MeshRenderer>().sharedMaterial = matMetal;

            GameObject lamba = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamba.name = "Lens";
            lamba.transform.SetParent(fenerGovde.transform);
            lamba.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            lamba.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);

            Light spot = lamba.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(1f, 0.95f, 0.8f);
            spot.range = 28f;
            spot.spotAngle = 45f;
            spot.intensity = 3.5f;

            kule.AddComponent<GuvenlikProjektoru>();
        }

        private static void MesaleOlustur(string isim, Vector3 pos, Transform parent, Material matMetal)
        {
            GameObject mesale = new GameObject(isim);
            mesale.transform.SetParent(parent);
            mesale.transform.position = pos;

            GameObject cubuk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cubuk.name = "MesaleDemiri";
            cubuk.transform.SetParent(mesale.transform);
            cubuk.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            cubuk.transform.localScale = new Vector3(0.18f, 0.45f, 0.18f);
            cubuk.GetComponent<MeshRenderer>().sharedMaterial = matMetal;

            GameObject canak = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canak.name = "AtesCanagi";
            canak.transform.SetParent(mesale.transform);
            canak.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            canak.transform.localScale = new Vector3(0.4f, 0.25f, 0.4f);

            Light atesIsigi = canak.AddComponent<Light>();
            atesIsigi.type = LightType.Point;
            atesIsigi.color = new Color(1f, 0.55f, 0.15f);
            atesIsigi.range = 9f;
            atesIsigi.intensity = 2.0f;

            mesale.AddComponent<IsikTitremesi>();
        }

        private static void RadarKulesiOlustur(string isim, Vector3 pos, Transform parent, Material matMetal)
        {
            GameObject radar = new GameObject(isim);
            radar.transform.SetParent(parent);
            radar.transform.position = pos;

            GameObject direk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            direk.name = "RadarDiregi";
            direk.transform.SetParent(radar.transform);
            direk.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            direk.transform.localScale = new Vector3(0.25f, 0.8f, 0.25f);
            direk.GetComponent<MeshRenderer>().sharedMaterial = matMetal;

            GameObject canak = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            canak.name = "RadarCanagi";
            canak.transform.SetParent(radar.transform);
            canak.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            canak.transform.localScale = new Vector3(1.8f, 0.15f, 0.6f);
            canak.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            canak.GetComponent<MeshRenderer>().sharedMaterial = matMetal;

            radar.AddComponent<RadarDonus>();
        }

        private static void BayrakDiregiOlustur(string isim, Vector3 pos, Transform parent, Material matAhsap, Material matKumas)
        {
            GameObject bayrakObj = new GameObject(isim);
            bayrakObj.transform.SetParent(parent);
            bayrakObj.transform.position = pos;

            GameObject direk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            direk.name = "Direk";
            direk.transform.SetParent(bayrakObj.transform);
            direk.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            direk.transform.localScale = new Vector3(0.12f, 1.8f, 0.12f);
            direk.GetComponent<MeshRenderer>().sharedMaterial = matAhsap;

            GameObject flama = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flama.name = "Flama";
            flama.transform.SetParent(bayrakObj.transform);
            flama.transform.localPosition = new Vector3(0.85f, 3.0f, 0f);
            flama.transform.localScale = new Vector3(1.5f, 0.9f, 0.05f);
            flama.GetComponent<MeshRenderer>().sharedMaterial = matKumas;

            flama.AddComponent<BayrakDalgalanma>();
        }
    }
}
