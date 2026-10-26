using UnityEngine;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// Kale etrafındaki Hendek (Moat) suyunun doku kaydırma (UV scroll) ve hafif
    /// dalga salınımı hareketini yöneten dinamik çevre bileşeni.
    /// </summary>
    public class HendekSuyuDalgalanma : MonoBehaviour
    {
        public Vector2 akisHizi = new Vector2(0.08f, 0.03f);
        public float dalgaGenligi = 0.04f;
        public float dalgaFrekansi = 1.6f;

        private MeshRenderer _rend;
        private Vector3 _baslangicPos;
        private Vector2 _uvOfset;

        private void Start()
        {
            _rend = GetComponent<MeshRenderer>();
            _baslangicPos = transform.localPosition;
        }

        private void Update()
        {
            float yDalga = Mathf.Sin(Time.time * dalgaFrekansi) * dalgaGenligi;
            transform.localPosition = _baslangicPos + new Vector3(0f, yDalga, 0f);

            if (_rend != null && _rend.material != null)
            {
                _uvOfset += akisHizi * Time.deltaTime;
                _rend.material.mainTextureOffset = _uvOfset;
                if (_rend.material.HasProperty("_BaseMap"))
                {
                    _rend.material.SetTextureOffset("_BaseMap", _uvOfset);
                }
            }
        }
    }
}
