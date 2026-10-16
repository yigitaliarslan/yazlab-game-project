using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Projede Can Barı, Düşman Askerleri ve ilgili tüm sistemleri
/// otomatik yapılandıran ve menüye ekleyen Editor scripti.
/// </summary>
[InitializeOnLoad]
public class DusmanKurulum
{
    static DusmanKurulum()
    {
        EditorApplication.delayCall += OtomatikKurulum;
    }

    [MenuItem("Tools/Düşman ve Can Barı Sistemini Kur")]
    public static void KurulumMenusu()
    {
        OtomatikKurulum();
        EditorUtility.DisplayDialog("Başarılı", 
            "Düşman askerleri ve Oyuncu Can Barı sistemi başarıyla kuruldu!\n" +
            "- Sağ alt köşede oyuncu can barı aktif.\n" +
            "- Düşmanlar kırmızı üniformayla sahnede yer aldı.\n" +
            "- Düşmanlar sizi gördüğünde ateş açar ve üzerlerinde can barı görünür.", 
            "Tamam");
    }

    public static void OtomatikKurulum()
    {
        // 1. Ana Oyuncuyu Yapılandır (OyuncuCani Ekle)
        KarakterHareket oyuncu = Object.FindAnyObjectByType<KarakterHareket>();
        if (oyuncu != null)
        {
            if (oyuncu.GetComponent<OyuncuCani>() == null)
            {
                oyuncu.gameObject.AddComponent<OyuncuCani>();
                EditorUtility.SetDirty(oyuncu.gameObject);
            }

            if (!oyuncu.CompareTag("Player"))
            {
                try { oyuncu.tag = "Player"; } catch { }
            }
        }

        // 2. Sahnede Düşman Askerleri Kontrol Et ve Oluştur
        DusmanKontrol[] mevcutDusmanlar = Object.FindObjectsByType<DusmanKontrol>(FindObjectsInactive.Exclude);

        if (mevcutDusmanlar == null || mevcutDusmanlar.Length == 0)
        {
            // Ana prefab'ı yükle
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ToonSoldiers_WW2_demo/ToonSoldier_WW2_demo_Prefab.prefab");
            RuntimeAnimatorController animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/ToonSoldiers_WW2_demo/KarakterAnimator.controller");
            Material dusmanMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ToonSoldiers_WW2_demo/model/Materials/Enemy_soldier.mat");

            if (prefab != null)
            {
                // Düşman 1 (Ön Sol Bölge)
                DusmanOlustur(prefab, animCtrl, dusmanMat, "DusmanAsker_1", new Vector3(-5.5f, 0f, 9.5f), 170f);

                // Düşman 2 (Ön Sağ Bölge)
                DusmanOlustur(prefab, animCtrl, dusmanMat, "DusmanAsker_2", new Vector3(6.5f, 0f, 11.0f), 190f);
            }
        }

        // 3. Sahneyi Kaydet
        if (!Application.isPlaying)
        {
            var aktifSahne = EditorSceneManager.GetActiveScene();
            if (aktifSahne.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(aktifSahne);
                EditorSceneManager.SaveScene(aktifSahne);
            }
        }
    }

    private static GameObject DusmanOlustur(GameObject prefab, RuntimeAnimatorController animCtrl, Material mat, string isim, Vector3 pozisyon, float yAci)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = isim;
        instance.transform.position = pozisyon;
        instance.transform.rotation = Quaternion.Euler(0f, yAci, 0f);

        // İstenmeyen oyuncu scriptlerini temizle
        KarakterHareket kh = instance.GetComponent<KarakterHareket>();
        if (kh != null) Object.DestroyImmediate(kh);

        OyuncuHareket oh = instance.GetComponent<OyuncuHareket>();
        if (oh != null) Object.DestroyImmediate(oh);

        YardimciKarakter yk = instance.GetComponent<YardimciKarakter>();
        if (yk != null) Object.DestroyImmediate(yk);

        CharacterController cc = instance.GetComponent<CharacterController>();
        if (cc != null) Object.DestroyImmediate(cc);

        // Düşman Kontrolcüsünü ekle
        DusmanKontrol dk = instance.GetComponent<DusmanKontrol>();
        if (dk == null)
        {
            dk = instance.AddComponent<DusmanKontrol>();
        }

        dk.maksimumCan = 100f;
        dk.mevcutCan = 100f;
        dk.gorusMenzili = 30f;
        dk.atesAraligi = 1.0f;
        dk.atesHasari = 8f;
        dk.kiyafetRengi = new Color(0.95f, 0.18f, 0.18f, 1f);

        // Collider ayarla
        CapsuleCollider col = instance.GetComponent<CapsuleCollider>();
        if (col == null) col = instance.AddComponent<CapsuleCollider>();
        col.height = 1.8f;
        col.radius = 0.38f;
        col.center = new Vector3(0f, 0.9f, 0f);

        // Animator ayarla
        Animator anim = instance.GetComponent<Animator>();
        if (anim != null && animCtrl != null)
        {
            anim.runtimeAnimatorController = animCtrl;
        }

        // Materyali Kırmızı Düşman Üniforması yap
        if (mat != null)
        {
            SkinnedMeshRenderer smr = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null)
            {
                smr.sharedMaterial = mat;
            }
        }
        dk.RengiUygula();

        Undo.RegisterCreatedObjectUndo(instance, isim + " Olusturuldu");
        EditorUtility.SetDirty(instance);

        return instance;
    }
}
