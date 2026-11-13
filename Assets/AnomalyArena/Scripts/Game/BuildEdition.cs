using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// What a page serves, from one package. The Chinese page (/zh/) is always the whole, latest game: illustrated and with every sound on
    /// by default. The English pages are the course's two builds: the Alpha at the site's root (the open mode alone, whitebox only, silent,
    /// no settings) and the Beta at /beta/ (the whole game, whitebox by default, silent until the player turns the volume up, no story
    /// cutscenes). The page's address decides; outside a web page (the editor, the tests, a desktop build) the game is the whole one.
    /// </summary>
    public static class BuildEdition
    {
        public enum Edition { Full, EnglishBeta, EnglishAlpha }

        static Edition? current;

        /// <summary>What this page serves.</summary>
        public static Edition Current => current ??= Of(Application.platform == RuntimePlatform.WebGLPlayer, Application.absoluteURL);
        /// <summary>This page is the English Alpha.</summary>
        public static bool IsAlpha => Current == Edition.EnglishAlpha;
        /// <summary>This page is one of the course's English builds, the Alpha or the Beta: silent by default, no illustrated cutscenes.</summary>
        public static bool IsCourseBuild => Current != Edition.Full;
        /// <summary>This page plays the story's cutscenes (CutscenesOn).</summary>
        public static bool PlaysCutscenes => CutscenesOn(Current);
        /// <summary>Only the whole game plays the story's cutscenes: the English Beta's story goes on without them, and the Alpha has
        /// no story.</summary>
        public static bool CutscenesOn(Edition edition) => edition == Edition.Full;

        /// <summary>A web page whose path holds "/zh/" is the whole game; else "/beta/" makes it the English Beta, and anything else the
        /// English Alpha. Off the web the game is the whole one. Only the path counts, not the host, the query or the fragment, as for the
        /// start language (StartupLanguage.PagePath).</summary>
        public static Edition Of(bool webPage, string pageAddress)
        {
            if (!webPage) return Edition.Full;
            string path = StartupLanguage.PagePath(pageAddress);
            if (path.IndexOf("/zh/", System.StringComparison.OrdinalIgnoreCase) >= 0) return Edition.Full;
            return path.IndexOf("/beta/", System.StringComparison.OrdinalIgnoreCase) >= 0 ? Edition.EnglishBeta : Edition.EnglishAlpha;
        }
    }
}
