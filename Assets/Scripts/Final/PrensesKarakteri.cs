using UnityEngine;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// Prenses Betül — "Beyaz Kuzgun" kod adlı kraliyet casusu.
    /// Kalenin prosedürel/primitif sanat tarzına uygun olarak çalışma zamanında parçalardan oluşturulur
    /// (çan etekli elbise, pelerin, uzun saç, altın taç). Kapsülden kurtarıldığında oyuncunun
    /// yanına yürüyerek gelir ve teşekkür animasyonu yapar.
    /// </summary>
    public class PrensesKarakteri : MonoBehaviour
    {
        public float yurumeHizi = 3.8f;
        public Transform govde;      // Nefes alma ve selam verme animasyonu için üst gövde kökü
        public Transform pelerin;
        public Transform solKol, sagKol;

        private Vector3? _hedef;
        private bool _tekerlekCeviriyor;
        private bool _tesekkurEdiyor;
        private float _yurumeFazi;

        public bool HedefeUlasti => !_hedef.HasValue;

        public static PrensesKarakteri Olustur(Transform parent, Vector3 konum)
        {
            GameObject kok = new GameObject("Prenses_Betul");
            kok.transform.SetParent(parent);
            kok.transform.position = konum;
            kok.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            Material elbise = PrensesKulesiInsaci.LitMateryal(new Color(0.92f, 0.92f, 0.98f));
            Material elbiseMor = PrensesKulesiInsaci.LitMateryal(new Color(0.42f, 0.20f, 0.62f));
            Material ten = PrensesKulesiInsaci.LitMateryal(new Color(0.96f, 0.80f, 0.68f));
            Material sac = PrensesKulesiInsaci.LitMateryal(new Color(0.12f, 0.08f, 0.07f));
            Material altin = PrensesKulesiInsaci.LitMateryal(new Color(1.0f, 0.80f, 0.25f));
            Material pelerinMat = PrensesKulesiInsaci.LitMateryal(new Color(0.08f, 0.10f, 0.20f));

            Transform t = kok.transform;

            // Çan etek (aşağı doğru genişleyen katmanlar)
            Parca(PrimitiveType.Cylinder, "Etek_Alt", t, new Vector3(0f, 0.16f, 0f), new Vector3(0.95f, 0.16f, 0.95f), elbise);
            Parca(PrimitiveType.Cylinder, "Etek_Orta", t, new Vector3(0f, 0.46f, 0f), new Vector3(0.78f, 0.16f, 0.78f), elbise);
            Parca(PrimitiveType.Cylinder, "Etek_Ust", t, new Vector3(0f, 0.74f, 0f), new Vector3(0.58f, 0.14f, 0.58f), elbise);
            Parca(PrimitiveType.Cylinder, "Etek_MorKusak", t, new Vector3(0f, 0.31f, 0f), new Vector3(0.88f, 0.03f, 0.88f), elbiseMor);

            GameObject gv = new GameObject("UstGovde");
            gv.transform.SetParent(t, false);
            gv.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            Transform g = gv.transform;

            Parca(PrimitiveType.Cylinder, "Korse", g, new Vector3(0f, 0.2f, 0f), new Vector3(0.36f, 0.22f, 0.28f), elbiseMor);
            Parca(PrimitiveType.Cube, "Altin_Kemer", g, new Vector3(0f, 0.02f, 0f), new Vector3(0.40f, 0.05f, 0.31f), altin);
            Parca(PrimitiveType.Sphere, "Omuzlar", g, new Vector3(0f, 0.42f, 0f), new Vector3(0.44f, 0.16f, 0.26f), elbise);
            Parca(PrimitiveType.Cylinder, "Boyun", g, new Vector3(0f, 0.53f, 0f), new Vector3(0.09f, 0.06f, 0.09f), ten);
            Parca(PrimitiveType.Sphere, "Kafa", g, new Vector3(0f, 0.72f, 0f), new Vector3(0.27f, 0.31f, 0.27f), ten);
            Parca(PrimitiveType.Sphere, "Sac_Ust", g, new Vector3(0f, 0.77f, -0.03f), new Vector3(0.30f, 0.30f, 0.30f), sac);
            Parca(PrimitiveType.Cube, "Sac_Uzun", g, new Vector3(0f, 0.48f, -0.12f), new Vector3(0.28f, 0.52f, 0.07f), sac);
            Parca(PrimitiveType.Sphere, "Goz_Sol", g, new Vector3(-0.055f, 0.74f, 0.125f), Vector3.one * 0.035f, sac);
            Parca(PrimitiveType.Sphere, "Goz_Sag", g, new Vector3(0.055f, 0.74f, 0.125f), Vector3.one * 0.035f, sac);

            // Taç
            Parca(PrimitiveType.Cylinder, "Tac_Halka", g, new Vector3(0f, 0.9f, -0.01f), new Vector3(0.22f, 0.03f, 0.22f), altin);
            for (int i = 0; i < 5; i++)
            {
                float rad = i * Mathf.PI * 2f / 5f;
                Parca(PrimitiveType.Cube, $"Tac_Dis_{i}", g, new Vector3(Mathf.Sin(rad) * 0.1f, 0.96f, Mathf.Cos(rad) * 0.1f - 0.01f),
                    new Vector3(0.035f, 0.08f, 0.035f), altin);
            }
            Parca(PrimitiveType.Sphere, "Tac_Mucevher", g, new Vector3(0f, 0.93f, 0.105f), Vector3.one * 0.045f,
                PrensesKulesiInsaci.ParlakMateryal(new Color(0.25f, 1.0f, 0.55f, 1f)));

            // Kollar
            GameObject sol = Parca(PrimitiveType.Capsule, "Kol_Sol", g, new Vector3(-0.24f, 0.22f, 0f), new Vector3(0.09f, 0.22f, 0.09f), elbise);
            GameObject sag = Parca(PrimitiveType.Capsule, "Kol_Sag", g, new Vector3(0.24f, 0.22f, 0f), new Vector3(0.09f, 0.22f, 0.09f), elbise);

            // Pelerin (Beyaz Kuzgun'un gece mavisi pelerini)
            GameObject pel = Parca(PrimitiveType.Cube, "Pelerin", g, new Vector3(0f, 0.05f, -0.2f), new Vector3(0.46f, 0.85f, 0.03f), pelerinMat);
            pel.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);

            PrensesKarakteri pk = kok.AddComponent<PrensesKarakteri>();
            pk.govde = g;
            pk.pelerin = pel.transform;
            pk.solKol = sol.transform;
            pk.sagKol = sag.transform;
            return pk;
        }

        private static GameObject Parca(PrimitiveType tip, string ad, Transform parent, Vector3 lokal, Vector3 olcek, Material mat)
        {
            GameObject p = GameObject.CreatePrimitive(tip);
            p.name = ad;
            Object.Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(parent, false);
            p.transform.localPosition = lokal;
            p.transform.localScale = olcek;
            p.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return p;
        }

        public void HedefeGit(Vector3 nokta)
        {
            _hedef = nokta;
            _tekerlekCeviriyor = false;
            _tesekkurEdiyor = false;
        }

        public void Dur()
        {
            _hedef = null;
        }

        public void Isinla(Vector3 nokta)
        {
            _hedef = null;
            transform.position = nokta;
        }

        public void TekerlekCevir(bool aktif)
        {
            _tekerlekCeviriyor = aktif;
        }

        public void TesekkurEt(bool aktif)
        {
            _tesekkurEdiyor = aktif;
            if (aktif)
            {
                _hedef = null;
                _tekerlekCeviriyor = false;
            }
        }

        public void BakisYonu(Vector3 dunyaNoktasi)
        {
            Vector3 yon = dunyaNoktasi - transform.position;
            yon.y = 0f;
            if (yon.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(yon), Time.deltaTime * 8f);
            }
        }

        private void Update()
        {
            float t = Time.time;
            bool yuruyor = false;

            if (_hedef.HasValue)
            {
                Vector3 hedef = _hedef.Value;
                Vector3 duz = new Vector3(hedef.x - transform.position.x, 0f, hedef.z - transform.position.z);
                if (duz.magnitude < 0.14f)
                {
                    _hedef = null;
                }
                else
                {
                    yuruyor = true;
                    Vector3 adim = duz.normalized * Mathf.Min(yurumeHizi * Time.deltaTime, duz.magnitude);
                    Vector3 yeni = transform.position + adim;
                    yeni.y = ZeminYuksekligi(yeni, PrensesKulesiInsaci.ZeminY);
                    transform.position = yeni;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(duz), Time.deltaTime * 10f);
                }
            }

            // Nefes alma, yürüme sallanması ve teşekkür selamı
            if (govde != null)
            {
                _yurumeFazi += Time.deltaTime * (yuruyor ? 9.5f : 0f);
                float nefes = Mathf.Sin(t * 2.2f) * 0.012f;
                float zipla = yuruyor ? Mathf.Abs(Mathf.Sin(_yurumeFazi)) * 0.05f : 0f;
                float egilme = _tesekkurEdiyor ? (Mathf.Sin(t * 3.0f) * 0.5f + 0.5f) * 10f : (yuruyor ? 6f : 0f);
                govde.localPosition = new Vector3(0f, 0.9f + nefes + zipla, 0f);
                govde.localRotation = Quaternion.Euler(egilme, 0f, yuruyor ? Mathf.Sin(_yurumeFazi) * 3f : 0f);
            }

            if (solKol != null && sagKol != null)
            {
                if (_tesekkurEdiyor)
                {
                    // Sevinç ve minnetle el sallama / kalbine götürme hareketi
                    float s = Mathf.Sin(t * 5.5f) * 18f;
                    solKol.localRotation = Quaternion.Euler(-55f, 0f, -20f);
                    sagKol.localRotation = Quaternion.Euler(-115f + s, 0f, 18f);
                }
                else if (_tekerlekCeviriyor)
                {
                    float c = Mathf.Sin(t * 5f) * 35f;
                    solKol.localRotation = Quaternion.Euler(-70f + c, 0f, 10f);
                    sagKol.localRotation = Quaternion.Euler(-70f - c, 0f, -10f);
                }
                else if (yuruyor)
                {
                    float s = Mathf.Sin(_yurumeFazi) * 26f;
                    solKol.localRotation = Quaternion.Euler(s, 0f, 8f);
                    sagKol.localRotation = Quaternion.Euler(-s, 0f, -8f);
                }
                else
                {
                    // Kapsülde beklerken ellerini birleştirme
                    float e = Mathf.Sin(t * 1.4f) * 4f;
                    solKol.localRotation = Quaternion.Euler(-35f + e, 0f, -22f);
                    sagKol.localRotation = Quaternion.Euler(-35f + e, 0f, 22f);
                }
            }

            if (pelerin != null)
            {
                pelerin.localRotation = Quaternion.Euler(-8f - (yuruyor ? 14f : 0f) + Mathf.Sin(t * 3f) * 3f, 0f, 0f);
            }
        }

        private float ZeminYuksekligi(Vector3 p, float varsayilan)
        {
            RaycastHit[] vuruslar = Physics.RaycastAll(p + Vector3.up * 1.5f, Vector3.down, 3.8f, ~0, QueryTriggerInteraction.Ignore);
            float enYuksek = float.MinValue;
            foreach (var h in vuruslar)
            {
                if (h.collider == null || h.collider.isTrigger) continue;
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
                if (h.collider.GetComponentInParent<Animator>() != null) continue;
                if (h.point.y > enYuksek && h.point.y <= PrensesKulesiInsaci.ZeminY + 0.45f)
                {
                    enYuksek = h.point.y;
                }
            }
            return enYuksek > float.MinValue ? enYuksek : varsayilan;
        }
    }
}
