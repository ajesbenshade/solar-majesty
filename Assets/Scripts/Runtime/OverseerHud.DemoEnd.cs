using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// The Steam demo's end screen, drawn in place of the campaign verdict when Earth is won
    /// (<see cref="DemoEndOffer"/>). Kept out of OverseerHud.cs so the demo stays one file there.
    /// </summary>
    public partial class OverseerHud
    {
        private void DrawDemoEnd()
        {
            HudSkin.Vignette(new Rect(0, 0, _sw, _sh), new Color(0.02f, 0.05f, 0.03f), 0.5f);
            _hitRects.Add(new Rect(0f, 0f, _sw, _sh));

            var rect = VictoryOffer.PanelRect(_sw, _sh);
            rect.height = Mathf.Min(rect.height + 44f, Mathf.Max(rect.height, _sh - rect.y - 8f));
            var c = Panel(rect, null, false);
            HudSkin.ModalChrome(rect, Gold);

            var prev = _banner.normal.textColor;
            _banner.normal.textColor = HudSkin.GoldBright;
            GUI.Label(new Rect(c.x, c.y, c.width, 26f), DemoEndOffer.Headline, _banner);
            _banner.normal.textColor = prev;

            const float buttonH = 30f;
            const float gap = 8f;
            float lowerY = c.yMax - buttonH;
            float upperY = lowerY - gap - buttonH;
            float bodyTop = c.y + 30f;
            GUI.Label(new Rect(c.x, bodyTop, c.width, Mathf.Max(16f, upperY - 8f - bodyTop)),
                DemoEndOffer.Body, _wrap != null ? _wrap : _body);

            // Wishlist gets the full-width row: it is the button the demo exists for.
            if (GUI.Button(new Rect(c.x, upperY, c.width, buttonH),
                    DemoEndOffer.Label(DemoEndOffer.Choice.Wishlist), _chipOn))
            {
                SteamService.OpenStorePage(SteamConfig.FullGameAppId, SteamConfig.StorePageUrl);
                Notify(DemoEndOffer.WishlistOpened, 3f);
            }

            float w = (c.width - gap) * 0.5f;
            if (GUI.Button(new Rect(c.x, lowerY, w, buttonH),
                    DemoEndOffer.Label(DemoEndOffer.Choice.KeepPlaying), _chipOff))
                _loop.ContinueAfterDemoEnd(); // releases the hold; the screen draws only while held
            if (GUI.Button(new Rect(c.x + w + gap, lowerY, w, buttonH),
                    DemoEndOffer.Label(DemoEndOffer.Choice.Quit), _chipOff))
                _loop.QuitDemo();
        }
    }
}
