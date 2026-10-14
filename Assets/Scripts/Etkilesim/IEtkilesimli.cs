using UnityEngine;

namespace Yazlab.Etkilesim
{
    /// <summary>
    /// Nesne yönelimli programlama (OOP - Interface Segregation) gereğince,
    /// sahnede etkileşime girilebilen tüm nesnelerin (şalter, kapı, NPC vb.)
    /// uyması gereken temel arayüz.
    /// </summary>
    public interface IEtkilesimli
    {
        /// <summary>
        /// Nesne ile etkileşime geçildiğinde çağrılır.
        /// </summary>
        /// <param name="etkilesimiBaslatan">Etkileşimi tetikleyen aktörün (Oyuncu veya Yoldaş Ajan) Transform'u.</param>
        void EtkilesimeGir(Transform etkilesimiBaslatan);

        /// <summary>
        /// Nesnenin şu anda etkileşime açık olup olmadığını belirtir.
        /// </summary>
        bool EtkilesimeHazirMi { get; }

        /// <summary>
        /// Arayüzde (UI) gösterilecek etkileşim ipucu metni (Örn: "Şalteri İndir [E]").
        /// </summary>
        string EtkilesimMetni { get; }
    }
}
