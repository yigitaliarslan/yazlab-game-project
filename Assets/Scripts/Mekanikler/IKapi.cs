namespace Yazlab.Mekanikler
{
    /// <summary>
    /// Kapı mekanizmalarının uyması gereken soyutlama (OOP - Abstraction).
    /// </summary>
    public interface IKapi
    {
        bool AcikMi { get; }
        bool KilitliMi { get; }
        void KapiyiAc();
        void KapiyiKapat();
        void KilidiAc();
        void Kilitle();
    }
}
