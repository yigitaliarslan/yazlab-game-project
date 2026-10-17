using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Yazlab.Editor
{
    /// <summary>
    /// Kamera, oyuncu kontrolleri ve haritanın eksiksiz kurulu olduğu
    /// asıl oyun sahnesini (SampleScene) varsayılan yapar ve kamera bozulmalarını önler.
    /// </summary>
    [InitializeOnLoad]
    public static class BaslangicSahneAyari
    {
        private const string AsilSahneYolu = "Assets/Scenes/SampleScene.unity";

        static BaslangicSahneAyari()
        {
            EditorApplication.delayCall += Ayarla;
        }

        private static void Ayarla()
        {
            // 1. PlayMode başlangıç kısıtlamasını kaldır (böylece asıl sahnemiz kamera ile birlikte çalışır)
            EditorSceneManager.playModeStartScene = null;

            // 2. Eğer editörde eksik sahne açıksa doğrudan harita ve kameranın kurulu olduğu asıl sahneye geç
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                var aktif = EditorSceneManager.GetActiveScene();
                if (aktif.path != AsilSahneYolu)
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        EditorSceneManager.OpenScene(AsilSahneYolu);
                        Debug.Log("[YazLab] Harita, kamera ve karakterlerin hazır olduğu asıl sahne yüklendi.");
                    }
                }
            }
        }
    }
}
