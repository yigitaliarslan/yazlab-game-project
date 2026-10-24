using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Projede Can Barı sistemini otomatik yapılandıran Editor scripti.
/// </summary>
[InitializeOnLoad]
public class DusmanKurulum
{
    static DusmanKurulum()
    {
        if (!Application.isBatchMode)
        {
            EditorApplication.delayCall += OtomatikKurulum;
        }
    }

    [MenuItem("Tools/Can Barı Sistemini Kur")]
    public static void KurulumMenusu()
    {
        OtomatikKurulum();
        EditorUtility.DisplayDialog("Başarılı", 
            "Oyuncu Can Barı sistemi başarıyla kuruldu!\n" +
            "- Sağ alt köşede oyuncu can barı aktif.", 
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

        // 2. Sahneyi Kaydet
        if (!Application.isPlaying && !Application.isBatchMode)
        {
            var aktifSahne = EditorSceneManager.GetActiveScene();
            if (aktifSahne.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(aktifSahne);
                EditorSceneManager.SaveScene(aktifSahne);
            }
        }
    }
}
