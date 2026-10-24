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
    /// Taktiksel İstihbarat Operasyonu senaryosuna uygun yuvarlak kale / şato
    /// yıkıntısı haritasını (surlar, siperler, merdivenler, taret, jeneratör,
    /// NPC kulesi ve çıkış kapısı) otomatik oluşturan ve NavMesh bake eden editör aracı.
    /// </summary>
    public class HaritaOlusturucu : EditorWindow
    {
        [MenuItem("Tools/YazLab/1. Haritayı Sıfırdan Oluştur (Yuvarlak Kale Seviyesi)")]
        public static void HaritayiKur()
        {
            if (!EditorUtility.DisplayDialog("Taktiksel İstihbarat Haritası", 
                "Taktiksel İstihbarat Operasyonu senaryosuna uygun Yuvarlak Kale & Yıkıntı Haritası inşa edilecek:\n\n" +
                "• Yuvarlak Dış Surlar ve Yıkık Kuleler\n" +
                "• Siper Hendekleri, Kum Torbaları ve Sandıklar\n" +
                "• Fiziksel Engel: Kilitli Depo + Taret Kulesi + Yoldaşın Gizli Yan Yolu + Güç Jeneratörü\n" +
                "• Sosyal Engel: Yarı Yıkık Kule, Merdivenler, Rüşvetçi NPC Muhafız Odası ve Çıkış Kapısı\n" +
                "• Otomatik Karakter & 3. Şahıs Kamera Kurulumu\n" +
                "• Otomatik NavMesh Taraması (Bake)\n\n" +
                "Devam edilsin mi?", 
                "Evet, Oluştur", "İptal"))
            {
                return;
            }

            // Eski harita kökü varsa temizle
            GameObject eskiHarita = GameObject.Find("--- HARITA_SEVIYESI ---");
            if (eskiHarita != null)
            {
                DestroyImmediate(eskiHarita);
            }
            GameObject haritaKok = new GameObject("--- HARITA_SEVIYESI ---");
            Undo.RegisterCreatedObjectUndo(haritaKok, "Harita Olusturuldu");

            // ==========================================
            // MALZEMELER (GERÇEKÇİ KALE & SAVAŞ PALETİ)
            // ==========================================
            Material matToprak = YeniMateryal("Mat_ToprakZemin", new Color(0.36f, 0.31f, 0.24f));
            Material matTasDuvar = YeniMateryal("Mat_TasDuvar", new Color(0.48f, 0.45f, 0.42f));
            Material matKoyuTas = YeniMateryal("Mat_KoyuTas", new Color(0.30f, 0.28f, 0.26f));
            Material matKumTorbasi = YeniMateryal("Mat_KumTorbasi", new Color(0.62f, 0.56f, 0.42f));
            Material matPasliMetal = YeniMateryal("Mat_PasliMetal", new Color(0.42f, 0.26f, 0.18f));
            Material matAhsap = YeniMateryal("Mat_Ahsap", new Color(0.46f, 0.33f, 0.20f));
            Material matAskeriVaril = YeniMateryal("Mat_AskeriVaril", new Color(0.25f, 0.32f, 0.24f));
            Material matNpcKapi = YeniMateryal("Mat_NpcKapi", new Color(0.20f, 0.38f, 0.58f));
            Material matKirmiziTehlike = YeniMateryal("Mat_KirmiziTehlike", new Color(0.85f, 0.15f, 0.10f));
            Material matYesilAktif = YeniMateryal("Mat_YesilAktif", new Color(0.15f, 0.85f, 0.25f));

            // ==========================================
            // 1. ZEMİN & DÜNYA MERKEZİ
            // ==========================================
            Vector3 kaleMerkezi = new Vector3(0f, 0f, 20f);
            float kaleYaricapi = 28f;

            GameObject zeminGrup = new GameObject("1_Zemin_Ve_Cevre");
            zeminGrup.transform.SetParent(haritaKok.transform);

            // Ana Zemin (Genişlik: 70m, Uzunluk: 75m)
            GameObject anaZemin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            anaZemin.name = "AnaZemin_Toprak";
            anaZemin.transform.SetParent(zeminGrup.transform);
            anaZemin.transform.position = new Vector3(0f, -0.5f, 20f);
            anaZemin.transform.localScale = new Vector3(70f, 1f, 75f);
            anaZemin.GetComponent<MeshRenderer>().sharedMaterial = matToprak;

            NavMeshSurface navSurface = anaZemin.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.All;

            // ==========================================
            // 2. YUVARLAK KALE SURLARI (24 SEGMENT)
            // ==========================================
            GameObject surGrup = new GameObject("2_Yuvarlak_Kale_Surlari");
            surGrup.transform.SetParent(haritaKok.transform);

            int segmentSayisi = 24;
            float aciAdimi = 360f / segmentSayisi;

            for (int i = 0; i < segmentSayisi; i++)
            {
                float aciRad = i * aciAdimi * Mathf.Deg2Rad;
                float x = kaleMerkezi.x + Mathf.Sin(aciRad) * kaleYaricapi;
                float z = kaleMerkezi.z + Mathf.Cos(aciRad) * kaleYaricapi;

                // Güney giriş kapısı segmentini (açı ~ 180°) boş bırakıyoruz
                if (i == 12) continue;

                // Bazı sur segmentlerini yıkık/alçak yapıyoruz (kale harabesi hissi)
                bool yikikSegment = (i == 4 || i == 18);
                float surYuksekligi = yikikSegment ? 2.5f : 6.0f;
                Vector3 surScale = new Vector3(7.6f, surYuksekligi, 2.0f);
                Vector3 surPos = new Vector3(x, surYuksekligi / 2f, z);

                GameObject surParcasi = GameObject.CreatePrimitive(PrimitiveType.Cube);
                surParcasi.name = $"SurSegment_{i}" + (yikikSegment ? "_Yikik" : "");
                surParcasi.transform.SetParent(surGrup.transform);
                surParcasi.transform.position = surPos;
                surParcasi.transform.rotation = Quaternion.Euler(0f, i * aciAdimi + 90f, 0f);
                surParcasi.GetComponent<MeshRenderer>().sharedMaterial = matTasDuvar;

                // Yıkık segmentlerin dibine moloz yığınları ekle
                if (yikikSegment)
                {
                    MolozYiginiOlustur($"Moloz_{i}", surPos - Vector3.up * 1f, matKoyuTas, surGrup.transform);
                }
            }

            // Dış Köşe Gözetleme Burçları (Bastions)
            BurcOlustur("KuzeyBurcu", new Vector3(0f, 0f, 48f), 4.5f, 8f, matTasDuvar, surGrup.transform);
            BurcOlustur("DoguBurcu", new Vector3(27f, 0f, 20f), 4.0f, 7f, matTasDuvar, surGrup.transform);
            BurcOlustur("BatiBurcu", new Vector3(-27f, 0f, 20f), 4.0f, 7f, matTasDuvar, surGrup.transform);

            // ==========================================
            // 3. BÖLGE 1: GÜNEY GİRİŞ & SİPERLER (Z = -10 ile 10)
            // ==========================================
            GameObject bolge1 = new GameObject("3_Bolge1_Giris_Ve_Siperler");
            bolge1.transform.SetParent(haritaKok.transform);

            // Büyük Taş Giriş Kemeri Sütunları
            KutuOlustur("GirisKapisi_SolSutun", new Vector3(-4.0f, 3.5f, -8f), new Vector3(2.0f, 7.0f, 2.5f), matTasDuvar, bolge1.transform);
            KutuOlustur("GirisKapisi_SagSutun", new Vector3(4.0f, 3.5f, -8f), new Vector3(2.0f, 7.0f, 2.5f), matTasDuvar, bolge1.transform);
            KutuOlustur("GirisKapisi_Kiris", new Vector3(0f, 6.5f, -8f), new Vector3(10.0f, 1.5f, 2.5f), matTasDuvar, bolge1.transform);

            // Taktiksel Siper Duvarları (Zikzak hatlar)
            KutuOlustur("Siper_Sol_1", new Vector3(-5.5f, 0.75f, -2f), new Vector3(4.0f, 1.5f, 0.8f), matTasDuvar, bolge1.transform);
            KutuOlustur("Siper_Sag_1", new Vector3(5.0f, 0.75f, 0f), new Vector3(4.5f, 1.5f, 0.8f), matTasDuvar, bolge1.transform);
            KutuOlustur("Siper_Orta_2", new Vector3(-1.0f, 0.75f, 5f), new Vector3(5.0f, 1.5f, 0.8f), matTasDuvar, bolge1.transform);

            // Kum Torbası Barikatları
            KumTorbasiSirasi("KumTorbasi_Sol", new Vector3(-5.5f, 0.4f, -1.2f), 4, matKumTorbasi, bolge1.transform);
            KumTorbasiSirasi("KumTorbasi_Sag", new Vector3(5.0f, 0.4f, 0.8f), 4, matKumTorbasi, bolge1.transform);
            KumTorbasiSirasi("KumTorbasi_Orta", new Vector3(-1.0f, 0.4f, 5.8f), 5, matKumTorbasi, bolge1.transform);

            // Ahşap Sandıklar ve Askeri Variller
            SandikOlustur("Sandik_1", new Vector3(-7.5f, 0.6f, -2.5f), new Vector3(1.3f, 1.2f, 1.3f), matAhsap, bolge1.transform);
            SandikOlustur("Sandik_2", new Vector3(-7.5f, 0.5f, -1.2f), new Vector3(1.1f, 1.0f, 1.1f), matAhsap, bolge1.transform);
            VarilOlustur("Varil_1", new Vector3(7.2f, 0.75f, 0.5f), matAskeriVaril, bolge1.transform);
            VarilOlustur("Varil_2", new Vector3(7.8f, 0.75f, 1.2f), matAskeriVaril, bolge1.transform);

            // ==========================================
            // 4. BÖLGE 2: FİZİKSEL ENGEL - KİLİTLİ DEPO, TARET & GÜÇ JENERATÖRÜ (Z = 12 ile 22)
            // ==========================================
            GameObject bolge2 = new GameObject("4_Bolge2_Depo_Taret_Ve_Jenerator");
            bolge2.transform.SetParent(haritaKok.transform);

            // İç Avlu Bölme Duvarı
            KutuOlustur("DepoDuvar_Sol", new Vector3(-11.5f, 2.5f, 14f), new Vector3(17f, 5f, 1.5f), matTasDuvar, bolge2.transform);
            KutuOlustur("DepoDuvar_Sag", new Vector3(10.5f, 2.5f, 14f), new Vector3(13f, 5f, 1.5f), matTasDuvar, bolge2.transform);

            // Kilitli Depo Kapısı (Otomatik yukarı kayan paslı çelik kapı)
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

            // --- OTOMATİK TARET KULESİ (SENARYO GEREKSİNİMİ) ---
            GameObject taretKulesi = new GameObject("Otomatik_Taret_Kulesi");
            taretKulesi.transform.SetParent(bolge2.transform);
            taretKulesi.transform.position = new Vector3(0f, 0f, 16.5f);

            // Taret Kaidesi (Yükseltilmiş taş platform)
            KutuOlustur("Taret_Kaide", new Vector3(0f, 1.5f, 16.5f), new Vector3(3f, 3f, 3f), matKoyuTas, taretKulesi.transform);

            // Taret Gövdesi (Metal kubbe)
            GameObject taretGovde = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            taretGovde.name = "Taret_Govdesi";
            taretGovde.transform.SetParent(taretKulesi.transform);
            taretGovde.transform.position = new Vector3(0f, 3.4f, 16.5f);
            taretGovde.transform.localScale = new Vector3(1.8f, 0.5f, 1.8f);
            taretGovde.GetComponent<MeshRenderer>().sharedMaterial = matPasliMetal;

            // Çift Namlu
            KutuOlustur("Taret_Namlu1", new Vector3(-0.35f, 3.4f, 14.8f), new Vector3(0.2f, 0.2f, 2.2f), matPasliMetal, taretKulesi.transform);
            KutuOlustur("Taret_Namlu2", new Vector3(0.35f, 3.4f, 14.8f), new Vector3(0.2f, 0.2f, 2.2f), matPasliMetal, taretKulesi.transform);

            // Taret Kırmızı Tehdit Sensör Işığı
            GameObject taretIsik = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            taretIsik.name = "Taret_TehditIsigi";
            taretIsik.transform.SetParent(taretKulesi.transform);
            taretIsik.transform.position = new Vector3(0f, 3.9f, 16.5f);
            taretIsik.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            MeshRenderer taretIsikRend = taretIsik.GetComponent<MeshRenderer>();
            taretIsikRend.sharedMaterial = matKirmiziTehlike;

            // --- YOLDAŞIN GİZLİ YAN YOLU (DOĞU SURLARI BOYUNCA) ---
            // Doğu tarafında (X = 18 - 22) siperlerin ve yıkık duvarların arkasından jeneratöre giden gizli patika
            GameObject yanYol = new GameObject("Yoldas_Gizli_Yan_Yolu");
            yanYol.transform.SetParent(bolge2.transform);

            KutuOlustur("YanYol_SiperDuvar1", new Vector3(18.5f, 1.2f, 9f), new Vector3(1.2f, 2.4f, 8f), matTasDuvar, yanYol.transform);
            KutuOlustur("YanYol_SiperDuvar2", new Vector3(19f, 1.2f, 17f), new Vector3(1.2f, 2.4f, 6f), matTasDuvar, yanYol.transform);
            KumTorbasiSirasi("YanYol_KumTorbasi", new Vector3(16.5f, 0.4f, 13f), 4, matKumTorbasi, yanYol.transform);

            // --- GÜÇ KAYNAĞI / JENERATÖR & ŞALTER (DOĞU AVLU, Z = 20, X = 19) ---
            GameObject jeneratorObj = new GameObject("Jenerator_Ve_GucKaynagi");
            jeneratorObj.transform.SetParent(bolge2.transform);
            jeneratorObj.transform.position = new Vector3(19.0f, 0f, 20.5f);

            // Jeneratör Gövdesi
            KutuOlustur("Jenerator_Kabin", new Vector3(19.0f, 1.0f, 20.5f), new Vector3(2.2f, 2.0f, 1.6f), matPasliMetal, jeneratorObj.transform);

            // Şalter Kolu
            GameObject salterKol = GameObject.CreatePrimitive(PrimitiveType.Cube);
            salterKol.name = "Salter_Kol";
            salterKol.transform.SetParent(jeneratorObj.transform);
            salterKol.transform.position = new Vector3(17.8f, 1.3f, 20.5f);
            salterKol.transform.localScale = new Vector3(0.2f, 0.6f, 0.2f);
            salterKol.GetComponent<MeshRenderer>().sharedMaterial = matAhsap;

            // Durum Gösterge Işığı (Kırmızıdan yeşile döner)
            GameObject durumIsigi = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            durumIsigi.name = "Jenerator_DurumIsigi";
            durumIsigi.transform.SetParent(jeneratorObj.transform);
            durumIsigi.transform.position = new Vector3(17.8f, 1.8f, 20.5f);
            durumIsigi.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            MeshRenderer durumIsikRend = durumIsigi.GetComponent<MeshRenderer>();
            durumIsikRend.sharedMaterial = matKirmiziTehlike;

            // Şalter Mekanizması Scripti ve Tetikleyicisi
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
            soSalter.FindProperty("tetiklemeYontemi").enumValueIndex = 1; // AlanaGirmece
            soSalter.ApplyModifiedProperties();

            // ==========================================
            // 5. BÖLGE 3: YARI YIKIK MERKEZ KULE, MERDİVENLER & RÜŞVETÇİ NPC MUHAFIZ
            // ==========================================
            GameObject bolge3 = new GameObject("5_Bolge3_YikikKule_Merdivenler_Ve_Npc");
            bolge3.transform.SetParent(haritaKok.transform);

            Vector3 kuleMerkez = new Vector3(0f, 0f, 34f);

            // Yarı Yıkık Kule Dış Duvarları (Arka tarafı yüksek, önü yıkık)
            KutuOlustur("KuleDuvar_Arka", new Vector3(0f, 4.5f, 44f), new Vector3(18f, 9f, 2f), matTasDuvar, bolge3.transform);
            KutuOlustur("KuleDuvar_Sol", new Vector3(-8.5f, 4.0f, 35f), new Vector3(2f, 8f, 16f), matTasDuvar, bolge3.transform);
            KutuOlustur("KuleDuvar_Sag", new Vector3(8.5f, 4.0f, 35f), new Vector3(2f, 8f, 16f), matTasDuvar, bolge3.transform);

            // 2. Kat Gözetleme & Muhafız Platformu (Yükseklik = 2.4m)
            GameObject ustPlatform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ustPlatform.name = "Kule_IkinciKat_Platformu";
            ustPlatform.transform.SetParent(bolge3.transform);
            ustPlatform.transform.position = new Vector3(0f, 2.2f, 36f);
            ustPlatform.transform.localScale = new Vector3(15f, 0.4f, 14f);
            ustPlatform.GetComponent<MeshRenderer>().sharedMaterial = matKoyuTas;

            // Kademeli Taş Merdivenler (Z = 22'den Z = 29'a, Yükseklik = 0'dan 2.2m'ye)
            GameObject merdivenGrup = new GameObject("Tas_Merdivenler");
            merdivenGrup.transform.SetParent(bolge3.transform);

            int basamakSayisi = 8;
            float basamakYukseklik = 2.2f / basamakSayisi; // ~0.275m (NavMesh için kusursuz)
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

            // Merdiven Korkulukları
            KutuOlustur("Korkuluk_Sol", new Vector3(-2.6f, 1.6f, 25.5f), new Vector3(0.3f, 1.2f, 7.0f), matAhsap, merdivenGrup.transform);
            KutuOlustur("Korkuluk_Sag", new Vector3(2.6f, 1.6f, 25.5f), new Vector3(0.3f, 1.2f, 7.0f), matAhsap, merdivenGrup.transform);

            // Muhafız Barikatı / Nöbetçi Masası
            KutuOlustur("NobetciMasasi", new Vector3(2.0f, 2.8f, 38.5f), new Vector3(2.4f, 0.8f, 1.0f), matAhsap, bolge3.transform);
            VarilOlustur("NobetciVarili", new Vector3(3.8f, 3.1f, 38.5f), matAskeriVaril, bolge3.transform);

            // Büyük Çıkış Kapısı (Ollama LLM NPC'nin koruduğu son kapı / asansör geçidi)
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

            // NPC Etkileşim Alanı (Muhafız masasının önü)
            GameObject npcTriggerObj = new GameObject("NPC_EtkilesimAlani");
            npcTriggerObj.transform.SetParent(bolge3.transform);
            npcTriggerObj.transform.position = new Vector3(0f, 2.5f, 36.5f);
            SphereCollider npcTriggerCol = npcTriggerObj.AddComponent<SphereCollider>();
            npcTriggerCol.isTrigger = true;
            npcTriggerCol.radius = 4.5f;
            AlanTetikleyici npcTetikleyici = npcTriggerObj.AddComponent<AlanTetikleyici>();

            // Bölüm Sonu Zafer Tetikleyicisi (Çıkış kapısının hemen arkası, Z = 46.5)
            GameObject cikisAlani = new GameObject("BolumSonu_Tetikleyici");
            cikisAlani.transform.SetParent(bolge3.transform);
            cikisAlani.transform.position = new Vector3(0f, 3.5f, 46.5f);
            BoxCollider cikisCol = cikisAlani.AddComponent<BoxCollider>();
            cikisCol.isTrigger = true;
            cikisCol.size = new Vector3(7f, 4f, 4f);
            AlanTetikleyici cikisTetikleyici = cikisAlani.AddComponent<AlanTetikleyici>();

            // ==========================================
            // 6. BÖLÜM YÖNETİCİSİ (BOLUM YONETICISI)
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

            // NPC alanına girildiğinde yöneticide durumu güncelle
            if (npcTetikleyici != null)
            {
                if (npcTetikleyici.onAlanaGirildi == null) npcTetikleyici.onAlanaGirildi = new UnityEngine.Events.UnityEvent();
                UnityEditor.Events.UnityEventTools.AddPersistentListener(npcTetikleyici.onAlanaGirildi, by.NpcEtkilesimBaslat);
                EditorUtility.SetDirty(npcTetikleyici);
            }

            // Çıkış tetikleyicisini oyunu tamamlamaya bağla
            if (cikisTetikleyici != null)
            {
                if (cikisTetikleyici.onAlanaGirildi == null) cikisTetikleyici.onAlanaGirildi = new UnityEngine.Events.UnityEvent();
                UnityEditor.Events.UnityEventTools.AddPersistentListener(cikisTetikleyici.onAlanaGirildi, by.OyunuTamamla);
                EditorUtility.SetDirty(cikisTetikleyici);
            }

            // ==========================================
            // 7. KARAKTERLER & 3. ŞAHIS KAMERA KURULUMU
            // ==========================================
            KarakterleriSahneyeKur();

            // ==========================================
            // 8. NAVMESH SURFACE BAKE (OTOMATİK TARAMA)
            // ==========================================
            navSurface.BuildNavMesh();

            // Sahneyi kaydet
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            EditorUtility.DisplayDialog("Operasyon Haritası Hazır!", 
                "Taktiksel İstihbarat Operasyonu Yuvarlak Kale Haritası başarıyla inşa edildi!\n\n" +
                "✓ Yuvarlak dış taş surlar ve yıkık burçlar yerleştirildi.\n" +
                "✓ Güney giriş kapısı, siper hendekleri ve kum torbaları kuruldu.\n" +
                "✓ Kilitli Depo Kapısı ve Tehdit Tareti avluya yerleştirildi.\n" +
                "✓ Doğu surları boyunca Yoldaşın Gizli Yan Yolu ve Jeneratör Şalteri bağlandı.\n" +
                "✓ Kuzeydeki yarı yıkık kule, kademeli taş merdivenler ve 2. kat platformu inşa edildi.\n" +
                "✓ Rüşvetçi NPC Muhafız odası, çıkış kapısı ve tetikleyiciler hazırlandı.\n" +
                "✓ Oyuncu, Yoldaş ve NPC Muhafız yerleştirildi, animatör ve 3. şahıs kamera bağlandı.\n" +
                "✓ NavMesh tüm zemin, siperler ve merdivenler için başarıyla Bake edildi!\n\n" +
                "Şimdi Play (▶) tuşuna basarak taktiksel operasyona başlayabilirsiniz!", "Harika!");
        }

        [MenuItem("Tools/YazLab/2. Karakterleri ve Animasyonları Sahneye Ekle (Otomatik)")]
        public static void KarakterleriSahneyeKurMenu()
        {
            KarakterleriSahneyeKur();
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorUtility.DisplayDialog("Başarılı", 
                "Oyuncu, Yoldaş ve NPC Muhafız karakterleri animasyonlarıyla birlikte sahneye eklendi ve 3. şahıs kamera bağlandı!\n\nŞimdi Play (▶) tuşuna basıp test edebilirsiniz.", 
                "Tamam");
        }

        public static void KarakterleriSahneyeKur()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ToonSoldiers_WW2_demo/ToonSoldier_WW2_demo_Prefab.prefab");
            RuntimeAnimatorController animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/ToonSoldiers_WW2_demo/KarakterAnimator.controller");

            if (prefab == null || animCtrl == null)
            {
                Debug.LogError("[HaritaOlusturucu] Asker prefabı veya Animator Controller bulunamadı!");
                return;
            }

            // 1. OYUNCU (İSTİHBARAT AJANI / PİYADE) - Güney Girişinde (Z = -5)
            Vector3 oyuncuBaslangic = new Vector3(0f, 0.05f, -5.0f);
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
                Animator anim = oyuncuObj.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.runtimeAnimatorController = animCtrl;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
            }

            // 2. YOLDAŞ KARAKTER (TAKIM ARKADAŞI / ARAMA KURTARMA) - Oyuncunun yanında
            Vector3 yoldasBaslangic = new Vector3(2.2f, 0.05f, -5.0f);
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

                Undo.RegisterCreatedObjectUndo(yoldasObj, "Yoldas Olusturuldu");
            }
            else
            {
                yoldas.transform.position = yoldasBaslangic;
                yoldas.hedef = oyuncuObj.transform;
                Animator anim = yoldas.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.runtimeAnimatorController = animCtrl;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
            }

            // 3. RÜŞVETÇİ NPC MUHAFIZ (Kule 2. Kat Platformunda, Z = 38, Y = 2.4)
            Vector3 npcKonum = new Vector3(0f, 2.45f, 38.0f);
            GameObject npcMuhafiz = GameObject.Find("NPC_Muhafiz");
            if (npcMuhafiz == null)
            {
                npcMuhafiz = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                npcMuhafiz.name = "NPC_Muhafiz";
                npcMuhafiz.transform.position = npcKonum;
                npcMuhafiz.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Merdivenden gelen oyuncuya doğru bakar

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

            // 4. ANA KAMERA (3. ŞAHIS KAMERA TAKİBİ)
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

        private static void MolozYiginiOlustur(string isim, Vector3 pos, Material mat, Transform parent)
        {
            GameObject molozGrup = new GameObject(isim);
            molozGrup.transform.SetParent(parent);
            molozGrup.transform.position = pos;

            for (int k = 0; k < 4; k++)
            {
                GameObject tas = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tas.name = $"Tas_{k + 1}";
                tas.transform.SetParent(molozGrup.transform);
                tas.transform.position = pos + new Vector3((k - 1.5f) * 0.7f, 0.3f, (k % 2 - 0.5f) * 0.7f);
                tas.transform.localScale = new Vector3(0.7f, 0.5f, 0.6f);
                tas.transform.rotation = Quaternion.Euler(15f * k, 30f * k, 10f * k);
                tas.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
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
    }
}
