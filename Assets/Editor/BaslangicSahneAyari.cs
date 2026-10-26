using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Yazlab.Editor
{
    /// <summary>
    /// Kamera, oyuncu kontrolleri ve 3 bölümlü kale haritasının eksiksiz kurulu olduğu
    /// asıl oyun sahnesini (SampleScene) varsayılan yapar ve yeni harita bileşenleri
    /// (Hendek, Asma Köprü, Kablo Odası, İç Kule) sahnede yoksa otomatik olarak inşa eder.
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

            // Eğer sahnede yeni 3 bölümlü kale yapıları (Asma Köprü ve Kablo Odası) henüz oluşmamışsa otomatik inşa et!
            if (GameObject.Find("Asma_Kopru_Sistemi") == null || GameObject.Find("Kablo_Ve_Salter_Odasi") == null)
            {
                Debug.Log("[YazLab] Yeni 3 Bölümlü Kale Haritası (Hendek, Asma Köprü, Kablo Odası, İç Kule) otomatik inşa ediliyor...");
                HaritaOlusturucu.HaritayiKurInternal();
                Debug.Log("[YazLab] 3 Bölümlü Kale Haritası otomatik olarak oluşturuldu ve kaydedildi!");
            }
        }
    }
}
