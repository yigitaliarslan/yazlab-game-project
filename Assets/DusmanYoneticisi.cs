using UnityEngine;

/// <summary>
/// Oyun çalıştığı anda sahnede oyuncu can sistemi eksikse
/// çalışma anında (Runtime) otomatik tamamlayan güvence yöneticisi.
/// </summary>
public class DusmanYoneticisi : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void SahneBaslangicKontrolu()
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
