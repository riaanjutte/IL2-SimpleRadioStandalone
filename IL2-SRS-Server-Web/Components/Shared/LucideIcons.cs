using System.Collections.Generic;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Components.Shared
{
    /// <summary>
    /// SVG contents of the Lucide icons used by the admin UI (https://lucide.dev, lucide-static 1.50.0,
    /// ISC License, Copyright (c) Lucide Icons and Contributors). Bundled so the UI works without internet access.
    /// To add an icon, copy the inner elements of icons/&lt;name&gt;.svg from the lucide-static package.
    /// </summary>
    internal static class LucideIcons
    {
        public static readonly IReadOnlyDictionary<string, string> Paths = new Dictionary<string, string>
        {
            ["monitor"] = "<rect width=\"20\" height=\"14\" x=\"2\" y=\"3\" rx=\"2\" /> <line x1=\"8\" x2=\"16\" y1=\"21\" y2=\"21\" /> <line x1=\"12\" x2=\"12\" y1=\"17\" y2=\"21\" />",
            ["sun"] = "<circle cx=\"12\" cy=\"12\" r=\"4\" /> <path d=\"M12 2v2\" /> <path d=\"M12 20v2\" /> <path d=\"m4.93 4.93 1.41 1.41\" /> <path d=\"m17.66 17.66 1.41 1.41\" /> <path d=\"M2 12h2\" /> <path d=\"M20 12h2\" /> <path d=\"m6.34 17.66-1.41 1.41\" /> <path d=\"m19.07 4.93-1.41 1.41\" />",
            ["moon"] = "<path d=\"M20.985 12.486a9 9 0 1 1-9.473-9.472c.405-.022.617.46.402.803a6 6 0 0 0 8.268 8.268c.344-.215.825-.004.803.401\" />",
            ["radio"] = "<path d=\"M16.247 7.761a6 6 0 0 1 0 8.478\" /> <path d=\"M19.075 4.933a10 10 0 0 1 0 14.134\" /> <path d=\"M4.925 19.067a10 10 0 0 1 0-14.134\" /> <path d=\"M7.753 16.239a6 6 0 0 1 0-8.478\" /> <circle cx=\"12\" cy=\"12\" r=\"2\" />",
            ["x"] = "<path d=\"M18 6 6 18\" /> <path d=\"m6 6 12 12\" />",
            ["chevron-down"] = "<path d=\"m6 9 6 6 6-6\" />",
            ["funnel"] = "<path d=\"M10 20a1 1 0 0 0 .553.895l2 1A1 1 0 0 0 14 21v-7a2 2 0 0 1 .517-1.341L21.74 4.67A1 1 0 0 0 21 3H3a1 1 0 0 0-.742 1.67l7.225 7.989A2 2 0 0 1 10 14z\" />",
        };
    }
}
