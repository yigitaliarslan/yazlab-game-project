using UnityEngine;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// Kule veya askeri yapılar üzerinde sürekli dönerek alanı tarayan
    /// radar, anten veya gözetleme cihazı hareketini sağlar.
    /// </summary>
    public class RadarDonus : MonoBehaviour
    {
        [Header("Dönüş Ayarları")]
        [Tooltip("Dönüş hızı (derece/saniye).")]
        [SerializeField] private float donusHizi = 45.0f;

        [Tooltip("Dönüş ekseni.")]
        [SerializeField] private Vector3 donusEkseni = Vector3.up;

        [Header("Salınım / Eğim (Opsiyonel)")]
        [Tooltip("Radar dönerken hafifçe yukarı-aşağı eğilsin mi?")]
        [SerializeField] private bool egimSalinimiOlsun = false;
        [SerializeField] private float egimAcisi = 10.0f;
        [SerializeField] private float salinimHizi = 1.5f;

        private float baslangicPitch = 0f;

        private void Start()
        {
            baslangicPitch = transform.localEulerAngles.x;
        }

        private void Update()
        {
            // Ana dönüş
            transform.Rotate(donusEkseni, donusHizi * Time.deltaTime, Space.Self);

            // Opsiyonel radar tıkırtısı / dikey salınım
            if (egimSalinimiOlsun)
            {
                float pitch = baslangicPitch + Mathf.Sin(Time.time * salinimHizi) * egimAcisi;
                Vector3 rot = transform.localEulerAngles;
                rot.x = pitch;
                transform.localEulerAngles = rot;
            }
        }
    }
}
