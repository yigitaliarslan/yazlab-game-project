using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Oyun çalıştığı anda ve sahne her yeniden yüklendiğinde oyuncu can sistemi eksikse
/// çalışma anında (Runtime) otomatik tamamlayan güvence yöneticisi.
/// </summary>
public class DusmanYoneticisi : MonoBehaviour
{
    private static bool _eventKayitli = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void SahneBaslangicKontrolu()
    {
        if (!_eventKayitli)
        {
            SceneManager.sceneLoaded -= OnSahneYuklendi;
            SceneManager.sceneLoaded += OnSahneYuklendi;
            _eventKayitli = true;
        }
        OyuncuCaniniGarantile();
    }

    private static void OnSahneYuklendi(Scene sahne, LoadSceneMode mod)
    {
        OyuncuCaniniGarantile();
    }

    private static void OyuncuCaniniGarantile()
    {
        // 1. Oyuncu Can Kontrolü
        KarakterHareket oyuncu = Object.FindAnyObjectByType<KarakterHareket>();
        if (oyuncu != null)
        {
            if (oyuncu.GetComponent<OyuncuCani>() == null)
            {
                oyuncu.gameObject.AddComponent<OyuncuCani>();
            }
        }
    }
}
