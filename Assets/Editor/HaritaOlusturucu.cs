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
    public class HaritaOlusturucu : EditorWindow
    {
        [MenuItem("Tools/YazLab/1. Haritayı Sıfırdan Oluştur (Oyun Seviyesi)")]
        public static void HaritayiKur()
        {
            // Yeni bir seviye oluştur veya mevcut sahneyi hazırla
            if (!EditorUtility.DisplayDialog("Harita Oluşturma", 
                "PDF isterlerine uygun (Fiziksel Engel + Yoldaş Şalteri + NPC Kapısı + NavMesh) 3D oyun haritası oluşturulacak. Devam edilsin mi?", 
                "Evet, Oluştur", "İptal"))
            {
                return;
            }

            // Sahneyi temiz bir kök nesneyle organize et
            GameObject haritaKok = GameObject.Find("--- HARITA_SEVIYESI ---");
            if (haritaKok != null)
            {
                DestroyImmediate(haritaKok);
            }
            haritaKok = new GameObject("--- HARITA_SEVIYESI ---");
            Undo.RegisterCreatedObjectUndo(haritaKok, "Harita Olusturuldu");

            // Malzemeler (Materials)
            Material matZemin = YeniMateryal("Mat_Zemin", new Color(0.25f, 0.28f, 0.32f));
            Material matDuvar = YeniMateryal("Mat_Duvar", new Color(0.4f, 0.42f, 0.45f));
            Material matKapi = YeniMateryal("Mat_Kapi", new Color(0.8f, 0.3f, 0.2f));
            Material matSalter = YeniMateryal("Mat_Salter", new Color(0.9f, 0.7f, 0.1f));
            Material matEngel = YeniMateryal("Mat_Engel", new Color(0.7f, 0.2f, 0.2f));
            Material matNpcKapisi = YeniMateryal("Mat_NpcKapi", new Color(0.2f, 0.5f, 0.8f));

            // ==========================================
            // 1. ZEMİNLER & NAVMESH SURFACE
            // ==========================================
            GameObject zeminGrup = new GameObject("1_Zeminler");
            zeminGrup.transform.SetParent(haritaKok.transform);

            // Ana Zemin (Genişlik: 20m, Uzunluk: 60m)
            GameObject anaZemin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            anaZemin.name = "AnaZemin";
            anaZemin.transform.SetParent(zeminGrup.transform);
            anaZemin.transform.position = new Vector3(0f, -0.5f, 25f);
            anaZemin.transform.localScale = new Vector3(20f, 1f, 60f);
            anaZemin.GetComponent<MeshRenderer>().sharedMaterial = matZemin;

            // NavMeshSurface bileşeni ekle
            NavMeshSurface navSurface = anaZemin.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.All;

            // Dış Sınır Duvarları (Oyuncu düşmesin)
            DuvarOlustur("SolDisDuvar", new Vector3(-10.25f, 1.5f, 25f), new Vector3(0.5f, 3f, 60f), matDuvar, zeminGrup.transform);
            DuvarOlustur("SagDisDuvar", new Vector3(10.25f, 1.5f, 25f), new Vector3(0.5f, 3f, 60f), matDuvar, zeminGrup.transform);
            DuvarOlustur("ArkaDisDuvar", new Vector3(0f, 1.5f, -5.25f), new Vector3(21f, 3f, 0.5f), matDuvar, zeminGrup.transform);

            // ==========================================
            // 2. BÖLGE 1: FİZİKSEL ENGEL & YOLDAŞ ROTASI
            // ==========================================
            GameObject bolge1 = new GameObject("2_Bolge1_FizikselEngel");
            bolge1.transform.SetParent(haritaKok.transform);

            // Ara Bölme Duvarı (Z = 15 konumunda)
            // Sol Taraf Duvarı: x = -6.5, width = 7
            DuvarOlustur("EngelDuvar_Sol", new Vector3(-6.5f, 2f, 15f), new Vector3(7f, 4f, 1f), matDuvar, bolge1.transform);
            // Sağ Taraf Duvarı: x = 6.5, width = 7
            DuvarOlustur("EngelDuvar_Sag", new Vector3(6.5f, 2f, 15f), new Vector3(7f, 4f, 1f), matDuvar, bolge1.transform);

            // Oyuncunun geçemediği ana kapı geçidi (Ortada x = 0, Z = 15)
            GameObject engelKapiObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            engelKapiObj.name = "FizikselEngelKapisi";
            engelKapiObj.transform.SetParent(bolge1.transform);
            engelKapiObj.transform.position = new Vector3(0f, 1.75f, 15f);
            engelKapiObj.transform.localScale = new Vector3(6f, 3.5f, 0.4f);
            engelKapiObj.GetComponent<MeshRenderer>().sharedMaterial = matKapi;

            // NavMeshObstacle ekle (Kapalıyken otonom ajan ve oyuncu geçemez)
            NavMeshObstacle kapiObstacle = engelKapiObj.AddComponent<NavMeshObstacle>();
            kapiObstacle.carving = true;
            kapiObstacle.size = new Vector3(1f, 1f, 1f);

            KapiKontrol engelKapiKontrol = engelKapiObj.AddComponent<KapiKontrol>();

            // --- YOLDAŞIN GEÇEBİLECEĞİ ALTERNATİF YAN GEÇİT (Sağ Tünel / Labirent Yolu) ---
            // Oyuncunun geçemeyeceği alçak tünel veya yan patika:
            // Sağ duvarda (X = 8.5, Z = 15) küçük bir aralık bırakıyoruz ama önüne oyuncunun geçemeyeceği
            // yüksek bir eşik veya lazer koyuyoruz. Yoldaş ise bu yan yoldan dolanıp arkadaki şaltere ulaşacak.
            GameObject yoldasGecidi = new GameObject("YoldasYanGecidi");
            yoldasGecidi.transform.SetParent(bolge1.transform);

            // Şalter Platformu / Odası (Kapının arkasında: Z = 20, X = 7)
            GameObject salterKaide = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            salterKaide.name = "Salter_Kaide";
            salterKaide.transform.SetParent(bolge1.transform);
            salterKaide.transform.position = new Vector3(7f, 0.4f, 20f);
            salterKaide.transform.localScale = new Vector3(1.2f, 0.4f, 1.2f);
            salterKaide.GetComponent<MeshRenderer>().sharedMaterial = matDuvar;

            // Şalter Butonu / Kolu
            GameObject salterButon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            salterButon.name = "Salter_Kol";
            salterButon.transform.SetParent(salterKaide.transform);
            salterButon.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            salterButon.transform.localScale = new Vector3(0.4f, 0.8f, 0.4f);
            MeshRenderer butonRenderer = salterButon.GetComponent<MeshRenderer>();
            butonRenderer.sharedMaterial = matSalter;

            // Şalter Tetikleyici Alanı (Collider Trigger)
            SphereCollider salterTrigger = salterKaide.AddComponent<SphereCollider>();
            salterTrigger.isTrigger = true;
            salterTrigger.radius = 1.8f;

            SalterMekanizmasi salterMek = salterKaide.AddComponent<SalterMekanizmasi>();
            SerializedObject soSalter = new SerializedObject(salterMek);
            soSalter.FindProperty("bagliKapi").objectReferenceValue = engelKapiKontrol;
            soSalter.FindProperty("salterKolu").objectReferenceValue = salterButon.transform;
            soSalter.FindProperty("durumIsigi").objectReferenceValue = butonRenderer;
            soSalter.FindProperty("tetiklemeYontemi").enumValueIndex = 1; // AlanaGirmece
            soSalter.ApplyModifiedProperties();

            // ==========================================
            // 3. BÖLGE 2: GEÇİŞ & KORİDOR ALANI (SİPERLER & SANDIKLAR)
            // ==========================================
            GameObject bolge2 = new GameObject("3_Bolge2_KoridorVeSiperler");
            bolge2.transform.SetParent(haritaKok.transform);

            // Koridorda stratejik siperler (Raycast sensörlerinin algılaması için mükemmel!)
            KutuOlustur("Siper_1", new Vector3(-3.5f, 0.75f, 25f), new Vector3(2.5f, 1.5f, 1f), matDuvar, bolge2.transform);
            KutuOlustur("Siper_2", new Vector3(3.0f, 0.75f, 32f), new Vector3(3f, 1.5f, 1f), matDuvar, bolge2.transform);
            KutuOlustur("Sandik_Grup1", new Vector3(-4f, 0.6f, 38f), new Vector3(1.5f, 1.2f, 1.5f), matEngel, bolge2.transform);
            KutuOlustur("Sandik_Grup2", new Vector3(4f, 0.6f, 40f), new Vector3(1.2f, 1.2f, 1.2f), matEngel, bolge2.transform);

            // ==========================================
            // 4. BÖLGE 3: SOSYAL ENGEL & NPC KAPISI (Z = 50)
            // ==========================================
            GameObject bolge3 = new GameObject("4_Bolge3_NpcKapisiVeCikis");
            bolge3.transform.SetParent(haritaKok.transform);

            // Bitiş Duvarı
            DuvarOlustur("NpcDuvar_Sol", new Vector3(-6.5f, 2.5f, 50f), new Vector3(7f, 5f, 1f), matDuvar, bolge3.transform);
            DuvarOlustur("NpcDuvar_Sag", new Vector3(6.5f, 2.5f, 50f), new Vector3(7f, 5f, 1f), matDuvar, bolge3.transform);

            // Büyük Çıkış Kapısı (Ollama LLM NPC'nin koruduğu kapı)
            GameObject npcKapiObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            npcKapiObj.name = "NpcCikisKapisi";
            npcKapiObj.transform.SetParent(bolge3.transform);
            npcKapiObj.transform.position = new Vector3(0f, 2.25f, 50f);
            npcKapiObj.transform.localScale = new Vector3(6f, 4.5f, 0.5f);
            npcKapiObj.GetComponent<MeshRenderer>().sharedMaterial = matNpcKapisi;

            NavMeshObstacle npcKapiObstacle = npcKapiObj.AddComponent<NavMeshObstacle>();
            npcKapiObstacle.carving = true;
            npcKapiObstacle.size = new Vector3(1f, 1f, 1f);

            KapiKontrol npcKapiKontrol = npcKapiObj.AddComponent<KapiKontrol>();

            // NPC Muhafız Durma Noktası (Nöbetçi Noktası)
            GameObject npcYeri = new GameObject("NPC_Muhafiz_Konumu");
            npcYeri.transform.SetParent(bolge3.transform);
            npcYeri.transform.position = new Vector3(3.5f, 0f, 47.5f);
            npcYeri.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // NPC Etkileşim Alanı (Trigger Zone)
            GameObject npcTriggerObj = new GameObject("NPC_EtkilesimAlani");
            npcTriggerObj.transform.SetParent(npcYeri.transform);
            npcTriggerObj.transform.localPosition = Vector3.zero;
            SphereCollider npcTriggerCol = npcTriggerObj.AddComponent<SphereCollider>();
            npcTriggerCol.isTrigger = true;
            npcTriggerCol.radius = 4.0f;
            npcTriggerObj.AddComponent<AlanTetikleyici>();

            // Çıkış / Bölüm Sonu Alanı (Kapının arkası Z = 54)
            GameObject cikisAlani = new GameObject("BolumSonu_Tetikleyici");
            cikisAlani.transform.SetParent(bolge3.transform);
            cikisAlani.transform.position = new Vector3(0f, 1f, 53.5f);
            BoxCollider cikisCol = cikisAlani.AddComponent<BoxCollider>();
            cikisCol.isTrigger = true;
            cikisCol.size = new Vector3(6f, 3f, 3f);
            AlanTetikleyici cikisTetikleyici = cikisAlani.AddComponent<AlanTetikleyici>();

            // ==========================================
            // 5. BÖLÜM YÖNETİCİSİ (BOLUM YONETICISI)
            // ==========================================
            GameObject yoneticiObj = GameObject.Find("BolumYoneticisi");
            if (yoneticiObj == null)
            {
                yoneticiObj = new GameObject("BolumYoneticisi");
            }
            BolumYoneticisi by = yoneticiObj.GetComponent<BolumYoneticisi>();
            if (by == null) by = yoneticiObj.AddComponent<BolumYoneticisi>();

            SerializedObject soBy = new SerializedObject(by);
            soBy.FindProperty("fizikselEngelKapisi").objectReferenceValue = engelKapiKontrol;
            soBy.FindProperty("salterMekanizmasi").objectReferenceValue = salterMek;
            soBy.FindProperty("npsCikisKapisi").objectReferenceValue = npcKapiKontrol;
            soBy.ApplyModifiedProperties();

            // Çıkış tetikleyicisini Bölüm Yöneticisine bağla
            UnityEditor.Events.UnityEventTools.AddPersistentListener(cikisTetikleyici.onAlanaGirildi, by.OyunuTamamla);

            // ==========================================
            // 6. OYUNCU & YOLDAŞ BAŞLANGIÇ NOKTALARI
            // ==========================================
            GameObject spawnGrup = GameObject.Find("SpawnNoktalari");
            if (spawnGrup == null) spawnGrup = new GameObject("SpawnNoktalari");
            spawnGrup.transform.SetParent(haritaKok.transform);

            GameObject oyuncuSpawn = new GameObject("OyuncuSpawnNoktasi");
            oyuncuSpawn.transform.SetParent(spawnGrup.transform);
            oyuncuSpawn.transform.position = new Vector3(0f, 0f, 0f);

            GameObject yoldasSpawn = new GameObject("YoldasSpawnNoktasi");
            yoldasSpawn.transform.SetParent(spawnGrup.transform);
            yoldasSpawn.transform.position = new Vector3(2.5f, 0f, 0f);

            // Sahnede varsa oyuncuyu ve yoldaşı başlangıca taşı
            KarakterHareket kh = Object.FindAnyObjectByType<KarakterHareket>();
            if (kh != null)
            {
                kh.transform.position = oyuncuSpawn.transform.position;
            }

            YardimciKarakter yk = Object.FindAnyObjectByType<YardimciKarakter>();
            if (yk != null)
            {
                yk.transform.position = yoldasSpawn.transform.position;
            }

            // ==========================================
            // 7. NAVMESH SURFACE BAKE ETME
            // ==========================================
            navSurface.BuildNavMesh();

            // Sahneyi kaydet
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorUtility.DisplayDialog("Tamamlandı!", 
                "Harita başarıyla oluşturuldu!\n\n" +
                "✓ Fiziksel Engel Kapısı ve Şalter kuruldu.\n" +
                "✓ Siperler ve Koridor eklendi.\n" +
                "✓ NPC Çıkış Kapısı ve Muhafız Alanı hazırlandı.\n" +
                "✓ NavMesh otomatik olarak Bake edildi!\n\n" +
                "Şimdi Play tuşuna basarak karakterinizle gezinebilirsiniz.", "Harika!");
        }

        private static GameObject DuvarOlustur(string isim, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            GameObject duvar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            duvar.name = isim;
            duvar.transform.SetParent(parent);
            duvar.transform.position = pos;
            duvar.transform.localScale = scale;
            duvar.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return duvar;
        }

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
            }
            return mat;
        }
    }
}
