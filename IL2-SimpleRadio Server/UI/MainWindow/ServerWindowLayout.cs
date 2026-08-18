namespace Ciribob.IL2.SimpleRadio.Standalone.Server.UI.MainWindow
{
    public static class ServerWindowLayout
    {
        public const double NormalWidth = 380.0;
        public const double ClientAdminPanelWidth = 680.0;
        public const double WindowHeight = 575.0;

        public static double GetWindowWidth(bool clientAdminVisible)
        {
            return NormalWidth + (clientAdminVisible ? ClientAdminPanelWidth : 0.0);
        }

        public static double GetClientAdminPanelWidth(bool clientAdminVisible)
        {
            return clientAdminVisible ? ClientAdminPanelWidth : 0.0;
        }
    }
}
