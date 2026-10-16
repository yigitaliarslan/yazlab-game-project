using UnityEngine;

/// <summary>
/// Oyun çalıştığı anda sahnede düşman veya oyuncu can sistemi eksikse
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

        // 2. Düşman Kontrolü
        DusmanKontrol[] dusmanlar = Object.FindObjectsByType<DusmanKontrol>(FindObjectsInactive.Exclude);
        if (dusmanlar == null || dusmanlar.Length == 0)
        {
            // Sahnede hazır düşman yoksa prefab'tan oluştur
            GameObject prefab = Resources.Load<GameObject>("ToonSoldier_WW2_demo_Prefab");
            if (prefab == null)
            {
                // Resources'da yoksa sahnede mevcut oyuncu prefab kopyasından türet
                if (oyuncu != null)
                {
                    DusmanTuret(oyuncu.gameObject, "DusmanAsker_1", new Vector3(-5.5f, 0f, 9.5f), 170f);
                    DusmanTuret(oyuncu.gameObject, "DusmanAsker_2", new Vector3(6.5f, 0f, 11.0f), 190f);
                }
            }
        }
    }

    private static GameObject DusmanTuret(GameObject kaynak, string isim, Vector3 pozisyon, float yAci)
    {
        GameObject clone = Instantiate(kaynak, pozisyon, Quaternion.Euler(0f, yAci, 0f));
        clone.name = isim;

        // Oyuncu bileşenlerini sil
        KarakterHareket kh = clone.GetComponent<KarakterHareket>();
        if (kh != null) Destroy(kh);

        KarakterAtes ka = clone.GetComponent<KarakterAtes>();
        if (ka != null) Destroy(ka);

        OyuncuCani oc = clone.GetComponent<OyuncuCani>();
        if (oc != null) Destroy(oc);

        CharacterController cc = clone.GetComponent<CharacterController>();
        if (cc != null) Destroy(cc);

        // Klonlanan oyuncunun ateş alt objelerini temizle
        Transform eskiNamlu = clone.transform.Find("NamluNoktasi");
        if (eskiNamlu != null) Destroy(eskiNamlu.gameObject);
        Transform eskiMermiIzi = clone.transform.Find("MermiIzi");
        if (eskiMermiIzi != null) Destroy(eskiMermiIzi.gameObject);

        // Düşman bileşenlerini ekle
        CapsuleCollider col = clone.GetComponent<CapsuleCollider>();
        if (col == null) col = clone.AddComponent<CapsuleCollider>();
        col.height = 1.8f;
        col.radius = 0.38f;
        col.center = new Vector3(0f, 0.9f, 0f);

        DusmanKontrol dk = clone.AddComponent<DusmanKontrol>();
        dk.maksimumCan = 100f;
        dk.mevcutCan = 100f;
        dk.gorusMenzili = 30f;
        dk.atesAraligi = 1.0f;
        dk.atesHasari = 8f;
        dk.kiyafetRengi = new Color(0.95f, 0.18f, 0.18f, 1f);
        dk.RengiUygula();

        return clone;
    }
}
