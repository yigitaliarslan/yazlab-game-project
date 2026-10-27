using UnityEngine;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// Kale hendeği (Moat) için düz küp yerine gerçek 3B alt-bölünmüş (subdivided) dalgalı su ağı (Mesh)
    /// üreten ve her karede sinüs/Perlin dalgalarıyla su yüzeyini gerçekçi biçimde hareketlendiren bileşen.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GercekciSuYuzeyi : MonoBehaviour
    {
        [Header("Su Ağı (Mesh) Boyutları")]
        public float genislik = 46f;
        public float uzunluk = 6.4f;
        public int xBolum = 48;
        public int zBolum = 14;

        [Header("Dalga Ayarları")]
        public float dalgaYuksekligi = 0.14f;
        public float dalgaHizi = 2.2f;
        public float dalgaSikligi = 0.65f;

        private Mesh _suMesh;
        private Vector3[] _bazNoktalar;
        private Vector3[] _aktifNoktalar;
        private MeshRenderer _rend;
        private Vector2 _uvKayma;

        private void Awake()
        {
            SuMeshOlustur();
            _rend = GetComponent<MeshRenderer>();
        }

        public void SuMeshOlustur()
        {
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf == null) mf = gameObject.AddComponent<MeshFilter>();

            _suMesh = new Mesh
            {
                name = "GercekciHendekSuyuMesh"
            };

            int vSayisi = (xBolum + 1) * (zBolum + 1);
            _bazNoktalar = new Vector3[vSayisi];
            _aktifNoktalar = new Vector3[vSayisi];
            Vector2[] uvs = new Vector2[vSayisi];

            float yariG = genislik * 0.5f;
            float yariU = uzunluk * 0.5f;

            int idx = 0;
            for (int z = 0; z <= zBolum; z++)
            {
                float zOran = (float)z / zBolum;
                float zPos = Mathf.Lerp(-yariU, yariU, zOran);

                for (int x = 0; x <= xBolum; x++)
                {
                    float xOran = (float)x / xBolum;
                    float xPos = Mathf.Lerp(-yariG, yariG, xOran);

                    _bazNoktalar[idx] = new Vector3(xPos, 0f, zPos);
                    _aktifNoktalar[idx] = _bazNoktalar[idx];
                    uvs[idx] = new Vector2(xOran * 8f, zOran * 2f);
                    idx++;
                }
            }

            int[] tris = new int[xBolum * zBolum * 6];
            int t = 0;
            for (int z = 0; z < zBolum; z++)
            {
                for (int x = 0; x < xBolum; x++)
                {
                    int i = z * (xBolum + 1) + x;
                    tris[t++] = i;
                    tris[t++] = i + xBolum + 1;
                    tris[t++] = i + 1;

                    tris[t++] = i + 1;
                    tris[t++] = i + xBolum + 1;
                    tris[t++] = i + xBolum + 2;
                }
            }

            _suMesh.vertices = _bazNoktalar;
            _suMesh.uv = uvs;
            _suMesh.triangles = tris;
            _suMesh.RecalculateNormals();
            _suMesh.RecalculateBounds();

            mf.sharedMesh = _suMesh;
        }

        private void Update()
        {
            if (_suMesh == null || _bazNoktalar == null) return;

            float t = Time.time * dalgaHizi;
            for (int i = 0; i < _bazNoktalar.Length; i++)
            {
                Vector3 v = _bazNoktalar[i];
                float d1 = Mathf.Sin(v.x * dalgaSikligi + t) * Mathf.Cos(v.z * dalgaSikligi * 1.3f + t * 0.8f);
                float d2 = Mathf.Sin((v.x - v.z) * dalgaSikligi * 1.7f - t * 1.4f) * 0.45f;
                v.y = (d1 + d2) * dalgaYuksekligi;
                _aktifNoktalar[i] = v;
            }

            _suMesh.vertices = _aktifNoktalar;
            _suMesh.RecalculateNormals();

            if (_rend != null && _rend.material != null)
            {
                _uvKayma += new Vector2(0.06f, 0.025f) * Time.deltaTime;
                _rend.material.mainTextureOffset = _uvKayma;
                if (_rend.material.HasProperty("_BaseMap"))
                {
                    _rend.material.SetTextureOffset("_BaseMap", _uvKayma);
                }
            }
        }
    }
}
