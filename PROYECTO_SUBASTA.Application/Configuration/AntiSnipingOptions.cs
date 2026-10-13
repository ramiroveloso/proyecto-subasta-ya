namespace PROYECTO_SUBASTA.Application.Configuration
{
    public class AntiSnipingOptions
    {
        public const string SectionName = "Subastas:AntiSniping";

        public int UmbralSegundos { get; set; } = 60;
        public int ExtensionSegundos { get; set; } = 120;
    }
}
