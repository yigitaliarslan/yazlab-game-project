using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Yazlab.Yonetim;

namespace Yazlab.Editor
{
    /// <summary>
    /// Kamera, oyuncu kontrolleri ve 3 bölümlü kale haritasının eksiksiz kurulu olduğu
    /// asıl oyun sahnesini (SampleScene) varsayılan yapar ve yeni harita bileşenleri
    /// (Derin Hendek Kanyonu, 3B Dalgalı Su, Asma Köprü, Kablo Odası, Kapalı Merdiven Holü)
    /// sahnede yoksa otomatik olarak inşa eder.
    /// </summary>
    [InitializeOnLoad]
    public static class BaslangicSahneAyari
    {
        private const string AsilSahneYolu = "Assets/Scenes/SampleScene.unity";

        static BaslangicSahneAyari()
        {
            if (!Application.isBatchMode)
            {
                EditorApplication.delayCall += Ayarla;
                EditorApplication.playModeStateChanged += PlayModuDegisti;
            }
        }

        private static void PlayModuDegisti(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                HaritaEksikseKur();
            }
        }

        private static void Ayarla()
        {
            if (Application.isBatchMode) return;

            EditorSceneManager.playModeStartScene = null;

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                var aktif = EditorSceneManager.GetActiveScene();
                if (aktif.path != AsilSahneYolu)
                {
                    EditorSceneManager.OpenScene(AsilSahneYolu);
                    Debug.Log("[YazLab] Harita, kamera ve karakterlerin hazır olduğu asıl sahne yüklendi.");
                }

                HaritaEksikseKur();
            }
        }

        private static void HaritaEksikseKur()
        {
            if (EditorApplication.isPlaying) return;

            if (GameObject.Find("Asma_Kopru_Sistemi") == null ||
                GameObject.Find("Kablo_Ve_Salter_Odasi") == null ||
                GameObject.Find("MerdivenHolu_SolDuvar") == null)
            {
                HaritaOlusturucu.HaritayiKurInternal();
            }

            if (GameObject.Find("Hendek_DerinTaban") == null)
            {
                ProsedurelDokuUretici.DerinHendekVeCevreyiGarantile();
                ProsedurelDokuUretici.SahnedekiMateryalleriKapla();

                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(activeScene);
                    EditorSceneManager.SaveScene(activeScene);
                }
            }
        }
    }
}
