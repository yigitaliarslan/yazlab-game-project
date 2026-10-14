using UnityEngine;
using UnityEngine.Events;

namespace Yazlab.Etkilesim
{
    /// <summary>
    /// Harita üzerinde belirli alanlara (Örn: NPC konuşma alanı, gizli bölge, engel önü)
    /// oyuncu veya yoldaş girdiğinde olay tetikleyen bileşen.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AlanTetikleyici : MonoBehaviour
    {
        [Header("Tetikleme Koşulları")]
        [SerializeField] private string hedefEtiketi = "Player";
        [SerializeField] private bool sadeceBirKereTetikle = true;

        [Header("Olaylar")]
        public UnityEvent onAlanaGirildi;
        public UnityEvent onAlandanCikildi;

        private bool tetiklendiMi = false;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (sadeceBirKereTetikle && tetiklendiMi) return;

            if (other.CompareTag(hedefEtiketi) || (hedefEtiketi == "Player" && other.name.Contains("Player")))
            {
                tetiklendiMi = true;
                onAlanaGirildi?.Invoke();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(hedefEtiketi) || (hedefEtiketi == "Player" && other.name.Contains("Player")))
            {
                onAlandanCikildi?.Invoke();
            }
        }
    }
}
