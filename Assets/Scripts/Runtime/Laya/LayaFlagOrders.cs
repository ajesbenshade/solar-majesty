namespace SolarMajesty
{
    /// <summary>
    /// Asks the local Laya model to read flag orders the built-in parser could not. Fire-and-forget:
    /// without a bridge, or on any failure, the flag simply keeps no rules.
    /// </summary>
    public static class LayaFlagOrders
    {
        public static void TryUnderstand(FlagHandle flag, GameLoop loop)
        {
            var bridge = LayaBridge.Instance;
            var orders = flag?.Orders;
            if (bridge == null || orders == null || string.IsNullOrWhiteSpace(orders.text)) return;

            string body = LayaProtocol.BuildRequest(
                LayaOrdersPolicy.BuildState(orders.text), LayaOrdersPolicy.BuildQuestions());
            bridge.AskRaw(body, reply =>
            {
                if (flag.Orders != orders || orders.HasRules) return; // replaced or already read
                if (!LayaProtocol.TryParseAnswers(reply, out var answers)) return;
                if (LayaOrdersPolicy.Apply(orders, answers) && loop != null)
                    loop.LogOverseer($"Orders understood: {orders.Summary()}");
            });
        }
    }
}
